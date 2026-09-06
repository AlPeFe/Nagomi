# Integración para empresas de transporte externas (onboarding)

Guía práctica para la **empresa de transporte que va a ejecutar traslados de una
instalación Nagomi** usando su propio sistema (que no es Nagomi). Nagomi actúa
como **prescriptor**: genera solicitudes y las publica para que tu sistema las
consuma, las ejecute y notifique los estados.

> El contrato técnico de referencia (formato de mensajes, outbox, idempotencia)
> está en `docs/provider-integration.md`. Esta guía es el recorrido de integración
> de principio a fin.

---

## 1. Conceptos

| Concepto | Qué es |
|---|---|
| **Proveedor** | Tu empresa. Tiene credenciales propias y acceso solo a sus contratos. |
| **Contrato** | Una ruta de publicación. Cada solicitud de Nagomi se publica en el contrato que le corresponda. |
| **Cliente M2M** | Credenciales OAuth 2.0 (`client_id` + `client_secret`) para llamar a la API REST. |
| **Cola** | RabbitMQ con una cola dedicada por proveedor. Nagomi publica ahí avisos ligeros. |
| **Retrieval** | El aviso de la cola no trae datos sensibles: apunta a una URL REST para obtener el detalle con tu token. |

## 2. Alta como proveedor (lo hace el administrador de Nagomi)

Un administrador de la instalación Nagomi debe:

1. Crear el proveedor (`POST /api/provider-administration/providers`, política
   `Administration`).
2. Crear el contrato (`POST /api/provider-administration/contracts`) y su ruta
   (`POST /api/provider-administration/contracts/{contractId}/route`).
3. Crear el **cliente M2M** asociado al proveedor y contrato en
   `/api/provider-authentication/clients` — se devuelve el `client_secret`
   **una sola vez**; guárdalo de forma segura (gestor de secretos, no en el
   repositorio ni en logs).

## 3. Obtener un token

```
POST {NAGOMI_BASE}/connect/token
Content-Type: application/x-www-form-urlencoded

grant_type=client_credentials
client_id=TU_CLIENT_ID
client_secret=TU_CLIENT_SECRET
```

Respuesta: `{"access_token": "...", "expires_in": ..., "token_type": "Bearer"}`.
Envía ese token como `Authorization: Bearer <token>` en todas las llamadas REST.
El token es de corta duración: renueva automáticamente antes de que expire
(`expires_in` suele ser del orden de minutos/horas según la configuración).

## 4. Recibir avisos (opción recomendada: cola RabbitMQ)

- La cola de tu proveedor es **durable y aislada** (TLS, credenciales propias).
- Conéctate con manual acknowledgements, heartbeats y reconexión con backoff.
- Un mensaje de aviso contiene **solo**:

```json
{
  "messageId": "uuid-global",
  "messageType": "request.changed",
  "entityId": "REQ-2026-000001",
  "contractCode": "TU-CONTRATO",
  "occurredAt": "2026-09-06T10:00:00Z",
  "retrievalUrl": "/api/provider/requests/REQ-2026-000001"
}
```

**Nunca** contiene paciente, direcciones, teléfonos ni datos clínicos.

Flujo de consumo:

1. Guarda `messageId` en tu **inbox durable** (constraint único).
2. Si ya lo tienes, haz ack del duplicado sin repetir efectos.
3. Recupera el detalle: `GET {NAGOMI_BASE}{retrievalUrl}` con tu Bearer token.
4. Aplica el snapshot en tu sistema.
5. Commitea tu inbox + cambios, y **entonces** haz ack de RabbitMQ.

El ack de RabbitMQ es solo de transporte; la **recuperación REST** es la
confirmación de negocio de Nagomi (marca la notificación como `Retrieved`).

## 5. Recuperar trabajo sin cola (opción B: polling REST)

Si no quieres consumir RabbitMQ:

```
GET {NAGOMI_BASE}/api/provider/requests/{publicId}   # detalle de una solicitud
GET {NAGOMI_BASE}/api/provider/journeys?vehicleId=... # trayectos del proveedor
```

Solo ves lo de **tus contratos**: una solicitud fuera de ellos devuelve 404 sin
exponer datos.

## 6. Endpoints REST del proveedor

| Método | Ruta | Qué hace |
|---|---|---|
| GET | `/api/provider/requests/{publicId}` | Detalle actual de la solicitud (snapshot). |
| GET | `/api/provider/journeys/{publicId}` | Detalle actual de un trayecto. |
| GET | `/api/provider/journeys` | Lista trayectos no terminales del proveedor (opcional `?vehicleId=`). |
| PUT | `/api/provider/requests/{publicId}` | Sustituye el snapshot completo permitido de la solicitud. |
| PUT | `/api/provider/journeys/{publicId}` | Sustituye el snapshot completo permitido del trayecto. |
| POST | `/api/provider/requests/{publicId}/journeys` | Añade un trayecto excepcional. |
| POST | `/api/provider/journeys/{publicId}/status` | Registra un estado con fecha, ubicación y recurso. |
| POST | `/api/provider/requests/{publicId}/cancel` | Cancela la solicitud (y sus trayectos no completados). |
| POST | `/api/provider/journeys/{publicId}/cancel` | Cancela un trayecto. |
| PUT | `/api/provider/journeys/{publicId}/vehicle` | Asigna vehículo a un trayecto. |

Reglas de escritura:

- **Snapshots completos**: cada PUT envía el estado completo; el último aceptado
  gana (por orden de aceptación, no de envío).
- **No puedes**: borrar trayectos, cambiar identidad del paciente, el motivo del
  traslado ni la recurrencia.
- **Idempotencia**: toda escritura lleva `Idempotency-Key`. Genera una clave por
  comando lógico y reutilízala en reintentos (un timeout es resultado
  desconocido: reintenta con la misma clave).
- **Estados**: requieren `occurredAt` y pueden llegar desordenados. `Completed`
  es terminal; un estado no terminal posterior puede reabrir `Cancelled`.

### Ejemplo: marcar estado con ubicación

```
POST {NAGOMI_BASE}/api/provider/journeys/JRN-2026-000042/status
Authorization: Bearer <token>
Idempotency-Key: 3f2a8c1e-...   # estable por comando
Content-Type: application/json

{
  "status": "EnRouteToDestination",
  "occurredAt": "2026-09-06T10:42:00Z",
  "latitude": 41.3874,
  "longitude": 2.1686,
  "externalResourceCode": "AMB-42"
}
```

`externalResourceCode` es el puente con el vehículo que ve la parte prescriptora
(su "vista testimonial" del mapa). Usa el mismo código que tu empresa asigna al
recurso.

## 7. Estados posibles (journey)

`Scheduled`, `Activated`, `EnRouteToOrigin`, `ArrivedAtOrigin`,
`PatientOnBoard`, `EnRouteToDestination`, `ArrivedAtDestination`,
`Completed`, `Cancelled`.

## 8. Recuperación y fallos

- Nagomi persiste los cambios publicables en un **outbox transaccional**: si
  RabbitMQ está caído, la solicitud se guarda igual y el aviso se publica luego.
- Publicación con reintento: **5 veces a 1 minuto**; después, la notificación
  pasa a `Dead`. Un operador puede re-publicarla (nuevo `messageId`) desde
  `/api/provider-integration/operations/notifications` (política `Operations`).
- Si la recuperación REST falla, deja el mensaje de Rabbit sin ack o reintenta
  según la política de tu cola.
- 401/403 repetidos = problema de credenciales/contrato, no reintentes a ciegas.

## 9. Ejemplo completo (curl)

```bash
# 1. Token
TOKEN=$(curl -s -X POST $NAGOMI_BASE/connect/token \
  -d grant_type=client_credentials -d client_id=TU_CLIENT_ID -d client_secret=TU_SECRET \
  | sed -n 's/.*"access_token":"\([^"]*\)".*/\1/p')

# 2. Detalle de la solicitud avisada
curl -s -H "Authorization: Bearer $TOKEN" $NAGOMI_BASE/api/provider/requests/REQ-2026-000001

# 3. Estado con ubicación (idempotente)
curl -s -X POST $NAGOMI_BASE/api/provider/journeys/JRN-2026-000042/status \
  -H "Authorization: Bearer $TOKEN" -H "Idempotency-Key: 3f2a8c1e-0001" \
  -H "Content-Type: application/json" \
  -d '{"status":"PatientOnBoard","occurredAt":"2026-09-06T10:40:00Z","latitude":41.3874,"longitude":2.1686,"externalResourceCode":"AMB-42"}'
```

## 10. Lista de verificación antes de producción

- [ ] Credenciales M2M guardadas en gestor de secretos, nunca en código ni logs.
- [ ] Inbox durable con constraint único por `messageId`.
- [ ] Ack de Rabbit **después** de commitear inbox + cambios.
- [ ] `Idempotency-Key` estable por comando lógico.
- [ ] Manejo de duplicados (entrega at-least-once).
- [ ] Renovación automática del token antes de expirar.
- [ ] Reintento de recuperación REST con backoff; no reintentos a ciegas en 401.
- [ ] `externalResourceCode` estable por vehículo/recurso.
