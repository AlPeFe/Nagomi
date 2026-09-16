# API de Nagomi

Guía para saber **qué datos hay, dónde están y cómo pedirlos**. Todo lo que figura aquí está
sacado del código (`MapGroup` / `MapGet` / `MapPost`…), no de la memoria de nadie: si algo no
coincide, manda el código.

## Dos formas de leer la API

1. **Esta guía** — pensada para entender el modelo y encontrar el endpoint que necesitas.
2. **OpenAPI generado** — el backend sirve el documento **OpenAPI 3.1** en
   **`GET /openapi/v1.json`** (sin autenticación), y nginx lo publica en la misma URL que la API.
   Se genera desde el código, así que no se queda viejo: hoy son **87 rutas / 107 operaciones /
   98 esquemas**. Con ese JSON cargas Postman, Insomnia, Bruno o cualquier visor de OpenAPI y
   tienes todas las rutas con sus cuerpos de ejemplo.

```bash
# El contrato vivo, en la instancia local (y en http://192.168.31.223:8080/openapi/v1.json)
curl -s http://localhost:8080/openapi/v1.json -o nagomi-openapi.json

# Cargarlo en Postman: Import → File → nagomi-openapi.json
# (o Import → Link → http://192.168.31.223:8080/openapi/v1.json, y se refresca solo)
```

> Nota de contrato: el desplazamiento UTC de la recurrencia (`utcOffset`) viaja como **texto**
> (`"+02:00"`), no como duración binaria: es lo que ya enviaba la web y además permite documentarlo.

Otros documentos: [`estados-y-publicacion.md`](estados-y-publicacion.md) (estados y publicación),
[`security.md`](security.md) (auth y endurecimiento), [`provider-onboarding.md`](provider-onboarding.md)
(integrar un proveedor).

## Autenticación

El servidor de tokens es **OpenIddict**, en `POST /connect/token`. Hay dos mundos:

| Quién | Grant | Cómo | Para qué |
|---|---|---|---|
| Personal de la instalación (web) | `password` | usuario + contraseña | Todo lo que hace la aplicación web |
| Empresa de transporte externa (proveedor) | `client_credentials` | client_id + client_secret | Recibir y gestionar sus traslados |

```bash
TOKEN=$(curl -s -X POST http://localhost:8080/connect/token \
  -H 'Content-Type: application/x-www-form-urlencoded' \
  -d 'grant_type=password&username=TU_USUARIO&password=TU_CLAVE' \
  | python -c "import sys,json;print(json.load(sys.stdin)['access_token'])")

curl -s http://localhost:8080/api/operations/journeys -H "Authorization: Bearer $TOKEN"
```

El token es un bearer normal: se envía en `Authorization: Bearer <token>`. Los endpoints
`/api/admin/**` exigen rol **admin**; el resto de la web, rol autenticado. Los endpoints
`/api/provider/**` exigen identidad de **proveedor** (su `client_credentials`) y sólo ven lo suyo.

## Regla clave: cuándo un traslado sale por Rabbit y cuándo sólo por la API

Esto es lo que más despista al buscar datos, así que va primero:

1. **Asignar un vehículo no publica nada.** Es un *placeholder*: propone un vehículo.
   `POST /api/journeys/{id}/assign-vehicle`.
2. **Adjudicar un vehículo es lo que compromete el servicio** y genera la solicitud al proveedor:
   `POST /api/journeys/{id}/adjudicate-vehicle`. Hasta ese momento el traslado **no se publica** y
   el proveedor **no puede recuperarlo**.
3. **Si el cliente tiene cola de Rabbit asignada** (modo publicador), el mensaje se publica en
   **esa** cola. Se configura en Configuración → Clientes facturables.
4. **Si el cliente no tiene cola** —o la instalación ejecuta su propia flota— **no se publica en
   ninguna cola**. No es un error ni falta nada: el traslado sigue existiendo y **se expone por la
   API con normalidad** (`GET /api/provider/journeys`, `GET /api/operations/journeys`, detalle por
   identificador). La cola es un canal de *aviso*, no la fuente de verdad.
5. **Desadjudicar** (`/unadjudicate-vehicle`) libera el vehículo para poder cambiarlo y retira el
   aviso: borra del outbox lo que aún no había salido y, si el proveedor ya lo había recibido,
   emite un `JourneyUnassigned`.

Los estados del traslado (`JourneyStatus`) y las fases están en
[`estados-y-publicacion.md`](estados-y-publicacion.md).

## ¿Dónde obtengo…? (recetas)

| Quiero… | Endpoint |
|---|---|
| El trabajo del día (mesa de operaciones) | `GET /api/operations/journeys?from=AAAA-MM-DD&to=AAAA-MM-DD` |
| Lo mismo en CSV para Excel | `GET /api/operations/journeys/export.csv` (mismos filtros) |
| Todas las solicitudes (cabeceras) | `GET /api/operations/requests` |
| Un traslado concreto | `GET /api/journeys/{id}` |
| Los traslados de una solicitud | `GET /api/transport-requests/{id}` → campo `journeyRecords` |
| El histórico con filtros finos | `GET /api/operations/journeys` con `status`, `from`, `to`, `search`… |
| Pacientes (directorio) | `GET /api/admin/patients`, búsqueda rápida `GET /api/patients/search?q=` |
| Vehículos de mi flota | `GET /api/vehicles` (web) / `GET /api/admin/vehicles` (gestión) |
| Clientes y su cola | `GET /api/admin/tenant/clients` |
| Estado de las colas de Rabbit | `GET /api/queue` y vista previa `GET /api/queue/{cola}/peek` |
| Qué se ha avisado al proveedor | `GET /api/provider-integration/operations/notifications` |
| Datos maestros (INE, hospitales, motivos) | `GET /api/reference-data/*` |
| Rutas colectivas | `GET /api/routes?date=` |
| Urgencias | `GET /api/emergency-transports` |
| Config del asistente de IA | `GET /api/admin/tenant/ai` |

### Filtros de la mesa de operaciones

`GET /api/operations/journeys` acepta: `from`, `to` (fechas), `status`, `direction`,
`providerId`, `contractCode`, `reasonCode`, `originMunicipalityCode`, `destinationMunicipalityCode`,
`retrievalState` y `search` (paciente, documento, teléfono, referencia, identificador). Sin
`from`/`to` devuelve la ventana por defecto y sólo el trabajo vivo (sin completados ni cancelados).

## Inventario de endpoints

Convención: todos cuelgan de `/api` salvo el token (`/connect/token`). «Auth» indica el mínimo
necesario: **admin** (rol admin), **web** (usuario autenticado), **proveedor** (identidad de
proveedor) o **público**.

### Operación (lo que se consulta a diario) — auth web

| Método | Ruta | Para qué |
|---|---|---|
| GET | `/api/operations/journeys` | Listado operativo con filtros (la mesa diaria) |
| GET | `/api/operations/journeys/export.csv` | El mismo listado en CSV |
| GET | `/api/operations/requests` | Solicitudes (cabeceras) con sus hijos y periodicidad |

### Traslados — auth web

| Método | Ruta | Para qué |
|---|---|---|
| GET | `/api/journeys/{id}` | Detalle completo de un traslado |
| PUT | `/api/journeys/{id}/snapshot` | Editar la instantánea (origen, destino, requisitos, horario) |
| POST | `/api/journeys/{id}/statuses` | Añadir una fase/estado (activado, en origen, completado…) |
| GET | `/api/journeys/{id}/statuses` | Historial de estados |
| POST | `/api/journeys/{id}/assign-vehicle` | **Asignar** vehículo (placeholder, no publica) |
| POST | `/api/journeys/{id}/adjudicate-vehicle` | **Adjudicar** vehículo (compromete y publica) |
| POST | `/api/journeys/{id}/unadjudicate-vehicle` | **Desadjudicar** (liberar para cambiar de vehículo) |
| POST | `/api/journeys/{id}/cancel` | Anular (exige `reason` y `cancellingParty`) |
| POST | `/api/journeys/{id}/reset` | Volver a `Scheduled` limpiando el historial |

### Solicitudes y recurrencia — auth web

| Método | Ruta | Para qué |
|---|---|---|
| POST | `/api/transport-requests/drafts` | Crear borrador |
| GET | `/api/transport-requests/{id}` | Detalle (incluye `journeyRecords`) |
| PUT / DELETE | `/api/transport-requests/{id}/draft` | Editar / borrar borrador |
| POST | `/api/transport-requests/{id}/submit/one-off` | Enviar como traslado puntual (ida y/o vuelta) |
| POST | `/api/transport-requests/{id}/submit/recurring` | Enviar con periodicidad |
| POST | `/api/transport-requests/{id}/recurrence/preview` | Simular el impacto de un cambio de recurrencia |
| POST | `/api/transport-requests/{id}/recurrence/apply` | Aplicarlo |
| PUT | `/api/transport-requests/{id}/snapshot` | Editar la solicitud (y propagar a sus traslados) |
| POST | `/api/transport-requests/{id}/cancel` | Cancelar la solicitud y sus traslados vivos |

### Proveedor (la empresa de transporte externa) — auth proveedor

| Método | Ruta | Para qué |
|---|---|---|
| GET | `/api/provider/journeys` | **Sus** traslados (funciona aunque no haya cola ni aviso) |
| GET | `/api/provider/journeys/{publicId}?messageId={guid}` | Recuperar (retrieve) un traslado avisado y marcar la notificación como recibida |
| GET | `/api/provider/requests/{publicId}` | Su solicitud |
| PUT | `/api/provider/journeys/{publicId}` | Actualizar el traslado (requiere `Idempotency-Key`) |
| PUT | `/api/provider/journeys/{publicId}/vehicle` | Indicar el vehículo con el que lo ejecuta |
| POST | `/api/provider/journeys/{publicId}/status` | Marcar estados (requiere `Idempotency-Key`) |
| POST | `/api/provider/journeys/{publicId}/cancel` | Anular el traslado |
| PUT | `/api/provider/requests/{publicId}` | Actualizar la solicitud |
| POST | `/api/provider/requests/{publicId}/journeys` | Añadir un traslado excepcional |
| POST | `/api/provider/requests/{publicId}/cancel` | Anular la solicitud |

> El mensaje de Rabbit sólo lleva la **ruta de recuperación** y el identificador: los datos del
> paciente no viajan por el broker. El proveedor pide el detalle a la API con su token.

### Administración — auth admin

| Grupo | Rutas | Para qué |
|---|---|---|
| Clientes | `GET/POST /api/admin/tenant/clients`, `PUT /api/admin/tenant/clients/{id}` | Mantenimiento de clientes, incluida su **cola de Rabbit (opcional)** |
| Capacidades | `GET/PUT /api/admin/tenant/capabilities` | Publicar solicitudes / ejecutar traslados / urgencias |
| Asistente IA | `GET/PUT /api/admin/tenant/ai`, `POST /api/admin/tenant/ai/test` | Configurar el gateway de IA y probar la conexión (la contraseña nunca se devuelve) |
| Vehículos | `GET/POST /api/admin/vehicles`, `PUT/DELETE /api/admin/vehicles/{id}` | Flota: nombre, tipo, código, matrícula, plazas, notas |
| Pacientes | `GET/POST /api/admin/patients`, `PUT/DELETE /api/admin/patients/{id}` | Directorio (dirección, fecha de nacimiento, tarjeta, documento) |
| Usuarios | `GET/POST /api/admin/users`, `PUT/DELETE /api/admin/users/{id}` | Altas, roles y reseteo de contraseña |
| Clientes OAuth | `GET/POST /api/admin/identity/clients`, `POST /api/admin/identity/clients/{clientId}/rotate-secret`, `DELETE /api/admin/identity/clients/{clientId}` | Credenciales de los proveedores (client_credentials) |
| Proveedores/contratos | `GET/POST /api/provider-administration/providers`, `PUT /api/provider-administration/providers/{id}`, `GET/POST /api/provider-administration/contracts`, `PUT /…/contracts/{id}`, `POST /…/contracts/{contractId}/route` | Quién ejecuta y por qué contrato, y a qué cola se publica |
| Datos maestros | `GET /api/reference-data/*`, `POST/PUT /api/admin/reference-data/*`, `POST /api/admin/reference-data/imports/ine`, `POST /…/imports/cnh` | Municipios (INE), hospitales (CNH), motivos de traslado |

### Integración y diagnóstico — auth admin/web

| Método | Ruta | Para qué |
|---|---|---|
| GET | `/api/provider-integration/operations/notifications` | Bandeja de salida: qué se ha publicado, a quién y en qué estado |
| POST | `/api/provider-integration/operations/notifications/{id}/republish` | Reintentar una publicación |
| GET | `/api/queue` | Estado de las colas (mensajes, consumidores) |
| GET | `/api/queue/{cola}/peek` | Ver mensajes sin consumirlos |
| GET | `/api/coordination` | Panel de coordinación (vehículos y trabajo asignado) |
| PUT | `/api/journeys/{id}/vehicle`, `/api/journeys/{id}/driver` | Asignación rápida desde coordinación |

### Autenticación y auxiliares

| Método | Ruta | Auth | Para qué |
|---|---|---|---|
| GET | `/api/auth/me` | web | Quién soy (nombre, rol) |
| POST | `/api/auth/onboarding` | web | Alta inicial del administrador |
| POST | `/api/auth/logout` | web | Cerrar sesión |
| POST | `/api/patients/ensure` | web | Crear o reutilizar un paciente sin duplicar |
| GET | `/api/help-chat/status` | web | Si el asistente está disponible |
| POST | `/api/help-chat/messages` | web | Preguntar al asistente |
| GET | `/api/routes` · `POST /api/routes` · `PUT/DELETE /api/routes/{id}` · `POST /api/routes/{id}/{complete,cancel}` · `PUT /api/routes/{id}/{vehicle,driver}` | web | Rutas colectivas |
| GET | `/api/emergency-transports` · `GET /{id}` · `POST /` · `POST /{id}/cancel` | web | Urgencias |
| WS | `/hubs/dispatch` | web/proveedor | Avisos en vivo al vehículo (ver skill de dispatch) |

## Convenciones y errores

- **Validación** → `400` con un `ValidationProblem` por campo (`{"campo": ["mensaje"]}`).
- **Conflicto de estado** → `409` (p. ej. adjudicar un traslado anulado, o cambiar de vehículo
  estando adjudicado: hay que desadjudicar antes).
- **Sin permiso** → `401` sin token, `403` con token de rol insuficiente.
- **Idempotencia**: los comandos del proveedor aceptan la cabecera `Idempotency-Key`; repetir la
  misma clave no duplica el efecto.
- **Datos sensibles**: documento, tarjeta sanitaria, teléfono y notas no aparecen en los listados
  operativos ni viajan por Rabbit; sólo en el detalle y en los endpoints de gestión.
- **Fechas**: ISO-8601 UTC (`2026-09-15T18:02:21+00:00`); los filtros de día son `AAAA-MM-DD` y se
  interpretan en la zona local de la instalación.

## Cómo se ha generado esta lista

El inventario de rutas se extrae del código:

```bash
grep -rn 'MapGroup("' backend/src/Nagomi.Api/Features/            # prefijos
grep -rn 'MapGet(\|MapPost(\|MapPut(\|MapDelete(' backend/src/Nagomi.Api/Features/
```

Y el contrato vivo, siempre actualizado, en `GET /openapi/v1.json`.