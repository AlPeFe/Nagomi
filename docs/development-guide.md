# Nagomi — Guía técnica de desarrollo

Guía para quien vaya a **trabajar sobre el código** de Nagomi: arquitectura, cómo
levantar el stack, y en detalle la integración con RabbitMQ (topología, outbox,
consumo como proveedor, herramientas de desarrollo).

## 1. Stack y arquitectura

| Capa | Tecnología |
|---|---|
| Backend | ASP.NET Core minimal APIs (.NET 10), EF Core 10 + Npgsql, OpenIddict |
| Frontend | React 19 + TypeScript, Vite, mini-router propio (`src/router.tsx`, sin react-router) |
| BD | PostgreSQL 18 (`postgres:18-alpine`) |
| Mensajería | RabbitMQ 4.2 (`rabbitmq:4.2-management-alpine`) |
| Auth | Un solo OpenIddict server: *client credentials* para proveedores + *password grant* para usuarios web |

Comportamiento gobernado por specs OpenSpec en `openspec/specs/`. Los cambios
significativos se escriben primero como spec, se mapean a tests y luego se
implementan.

### Servicios y redes (docker compose)

- `postgres` — red `internal` (sin salida)
- `rabbitmq` — red `internal` (sin salida; el management 15672 NO está publicado por defecto)
- `backend` — redes `edge` + `internal`, `expose: 8080`, healthcheck `/ready`
- `frontend` — red `edge`, publicado en `${FRONTEND_BIND_ADDRESS:-127.0.0.1}:8080`

```
┌──────────┐     ┌──────────┐     ┌──────────┐
│ frontend │────▶│ backend  │────▶│ postgres │  (internal)
│  :8080   │edge │  :8080   │edge └──────────┘
└──────────┘     │   │      │
                 │   ▼      │
                 │  outbox  │────▶│ rabbitmq │  (internal)
                 └──────────┘     └──────────┘
```

## 2. Estructura del repo

```
backend/src/Nagomi.Api/
  Features/          # un directorio por feature (TransportRequests, Journeys,
                     # Operations, EmergencyTransports, ProviderIntegration, ...)
  Infrastructure/    # persistencia, auth OpenIddict, PublicIds, auditoría
frontend/src/
  pages/ components/ api.ts router.tsx auth.ts
tests/
  Nagomi.UnitTests/        # sin infraestructura, rápidos
  Nagomi.IntegrationTests/ # Testcontainers (Postgres+Rabbit) + WebApplicationFactory in-memory
openspec/specs/            # specs activas por área
docs/                      # deployment.md, provider-integration.md, tenant-capabilities.md, este fichero
tools/rabbit-consume/      # cliente web de consumo de colas (dev)
nagomi.sh                  # up|down|status|logs|restart del stack
```

## 3. Levantar el stack

```sh
cd /c/Users/alexlocal/projects/Nagomi
./nagomi.sh up          # build + up (postgres + rabbitmq + backend + frontend)
./nagomi.sh down        # ./nagomi.sh status | logs | restart
```

- El primer arranque copia `.env.example` → `.env` e inyecta passwords aleatorios.
- `.env` está en `.gitignore` (NUNCA commitearlo). La configuración llega al
  backend como variables de entorno `Nombre__Subclave` (formato de binding de
  .NET).
- **Admin web**: `Authentication__Users__AdminEmail` / `AdminPassword`
  (compose: `NAGOMI_ADMIN_EMAIL` / `NAGOMI_ADMIN_PASSWORD`), seed automático al
  arrancar con `Database__MigrateOnStartup=true`. Defecto dev:
  `admin@nagomi.local` / `change-me-admin-123`.
- URL: `http://localhost:8080` (frontend). En este host el stack real está
  levantado con `FRONTEND_BIND_ADDRESS=0.0.0.0` → accesible en la LAN.

> Si se pierde el `.env`, se reconstruye con `docker inspect nagomi-*-1` (ver
> skill `nagomi-development`). Mantener las mismas credenciales de BD/RabbitMQ
> para no perder los volúmenes.

## 4. RabbitMQ — integración con proveedores (el corazón)

RabbitMQ se usa **solo como señal de cambio at-least-once**. El contrato
canónico es REST autenticado: el mensaje dice "ha cambiado algo", el proveedor
viene a buscar el snapshot actual con su token.

### 4.1 Topología (creada por el publisher)

```
Exchange  nagomi.provider.notifications          (direct, durable)
Exchange  nagomi.provider.notifications.dead     (direct, durable, DLX)

Cola      <QueueName>        durable  | x-dead-letter-exchange → .dead
          binding: nagomi.provider.notifications  routing key = <QueueName>

Cola      <QueueName>.dead   durable  | binding: .dead  routing key = <QueueName>
```

- `<QueueName>` es el `QueueName` de cada `TransportProvider` (p. ej.
  `prov.test.ambulancias`). Cada proveedor consume **solo su cola**.
- Las colas se declaran de forma **idempotente** en cada publicación
  (`RabbitMqProviderNotificationPublisher.PublishAsync`): exchange, cola,
  binding, cola muerta y binding muerto. No hay un paso de "setup de colas".
- Vhost por defecto: `nagomi`.

### 4.2 Contrato del mensaje

Payload (JSON, `ProviderNotificationMessage`):

```json
{
  "messageId": "68807a55-0000-0000-0000-000000000001",
  "messageType": "TransportRequestCreated",
  "entityPublicId": "REQ-2026-000041",
  "contractCode": "CTR-MAD-01",
  "timestamp": "2026-08-10T11:15:00+00:00",
  "retrievalUrl": "/api/provider/requests/REQ-2026-000041"
}
```

Propiedades AMQP del mensaje:

| Propiedad | Valor |
|---|---|
| `delivery_mode` | 2 (persistente) |
| `content_type` | `application/json` |
| `message_id` | `MessageId` (GUID) |
| `correlation_id` | `CorrelationId` (GUID) |
| `type` | `MessageType` (mismo valor que el campo JSON) |

**Solo eso.** Nunca datos del paciente, direcciones, teléfonos, requisitos ni
datos clínicos dentro del mensaje. El detalle está en el snapshot REST.

### 4.3 Tipos de mensaje

| `messageType` | Entidad | Cuándo |
|---|---|---|
| `TransportRequestCreated` | request | submit one-off / recurring |
| `TransportRequestUpdated` | request | edición de request |
| `TransportRequestCancelled` | request | cancelación |
| `JourneyUpdated` | journey | cambio de snapshot / excepción de recurrencia |
| `JourneyStatusChanged` | journey | nuevo evento de estado |
| `JourneyCancelled` | journey | cancelación de journey |

### 4.4 Flujo outbox (at-least-once, transaccional)

```
Endpoint (submit, update, cancel, status...)
   │  escribe entidad + ProviderOutbox.AddAsync(...)   ← mismo SaveChanges (transaccional)
   ▼
ProviderNotification { State=Pending, NextAttemptAt=now }
   │  ProviderOutboxWorker (BackgroundService, poll cada 10s, batch 50)
   ▼
¿proveedor activo con QueueName?  ──no──▶ MarkFailure("provider-inactive")
   │sí
RabbitMqProviderNotificationPublisher.PublishAsync   (publisher confirms + mandatory)
   │éxito                         │fallo
   ▼                              ▼
State=Published             FailedPublishAttempts++
                             NextAttemptAt = now + 1 min
                             > 5 intentos → State=Dead
   │
   ▼  (el proveedor consume y hace GET del retrievalUrl con su token)
NotificationRetrievalTracker.MarkRetrievedAsync
   ▼
State=Retrieved (receipt de negocio; el ack de Rabbit es solo de transporte)
```

Detalles:

- **Publicación**: canal con `publisherConfirmationsEnabled: true` (no se
  pierde nada en el publish), `BasicPublishAsync(..., mandatory: true)`.
- **Reintentos**: `ProviderOutboxWorker` con `PeriodicTimer(PollInterval=10s)`,
  lee `Pending AND (NextAttemptAt IS NULL OR NextAttemptAt <= now)`, `BatchSize=50`.
- **Máximo 5 intentos** a 1 min de separación (`MaximumRetries = 5`,
  `RetryDelay = 1m`); después `Dead`.
- **DLQ**: una notificación `Dead` NO se reenvía sola a la cola `.dead` — el
  estado `Dead` es del outbox en BD. La cola `.dead` del broker recibe mensajes
  rechazados/expirados a nivel AMQP (si un proveedor hace `reject` sin
  requeue, o TTL). Son dos mecanismos distintos.
- **Republish manual**: `POST /api/provider-integration/operations/notifications/{id}/republish`
  (solo `Dead` o `Published` sin `RetrievedAt`). Crea una **copia** con nuevo
  `MessageId`, mismo `CorrelationId`, y `ReplacesNotificationId = <origen>`.
  Apunta al snapshot REST actual — nunca se reenvía el payload antiguo.

### 4.5 Configuración

Sección `ProviderIntegration:RabbitMq` (appsettings o env `ProviderIntegration__RabbitMq__*`):

| Clave | Default |
|---|---|
| `Uri` | `amqp://guest:guest@localhost:5672` (compose: `RABBITMQ_URI`) |
| `Exchange` | `nagomi.provider.notifications` |
| `DeadLetterExchange` | `nagomi.provider.notifications.dead` |
| `RetryDelay` | `00:01:00` |
| `PollInterval` | `00:00:10` |
| `BatchSize` | `50` |

### 4.6 Consumir como proveedor (qué hay que implementar)

Patrón recomendado (está documentado en `docs/provider-integration.md` y es el
que los tests de integración validan). El modo auto-proveedor (el tenant se
ejecuta sus propios traslados con contrato/cola `SELF`) y la inspección de colas
desde el panel se explican en `docs/tenant-capabilities.md`:

1. Conexión durable, **manual ack**, heartbeat, reconnect con backoff
   exponencial; credenciales dedicadas por proveedor restringidas a su vhost/cola.
2. **Inbox con unique constraint sobre `messageId`**: si ya está registrado,
   ack del duplicado sin repetir efectos (la entrega es at-least-once, los
   duplicados son normales).
3. `GET {retrievalUrl}` con `Authorization: Bearer <token client_credentials>`.
4. Aplicar el snapshot de forma transaccional en el sistema del proveedor,
   guardar el inbox, y **entonces** ack de Rabbit.
5. El ack de Rabbit es solo transporte; el *receipt* de negocio es el GET que
   marca la notificación como `Retrieved` en Nagomi.

Escrituras del proveedor: todo con `Idempotency-Key` estable por comando (un
timeout = resultado desconocido → reintentar con la misma key). Eventos de
estado con `occurredAt` con offset (pueden llegar desordenados); `Completed` es
terminal, pero un evento no-terminal posterior puede reabrir `Cancelled`.

## 5. Consumir / inspeccionar colas en desarrollo

### 5.1 Exponer el management API (solo loopback)

RabbitMQ vive en la red `internal` y Docker Desktop **no** enruta puertos
publicados desde redes internal. Se necesita el override:

```sh
docker compose -f docker-compose.yml -f rabbit-local-override.yml up -d rabbitmq
# 127.0.0.1:15672 queda publicado y rabbitmq se une también a edge

# para revertir (quitar el puerto):
docker compose -f docker-compose.yml up -d rabbitmq
```

> RabbitMQ tarda ~3 min en arrancar tras un recreate (173s observado). El
> management API rechaza conexiones hasta que el healthcheck pasa a healthy.
> Si "port refused" → esperar, no es un problema de routing.

### 5.2 Herramienta web `tools/rabbit-consume` (recomendada)

Mini cliente stdlib de Python (cero dependencias) en `http://127.0.0.1:8095`,
proxy del management API (sin CORS, credenciales nunca llegan al navegador).

```sh
# Windows: lee credenciales del .env automáticamente
tools\rabbit-consume\start.cmd
# o manual:
export RABBIT_USER=... RABBIT_PASSWORD=... RABBIT_VHOST=nagomi
python tools/rabbit-consume/rabbit_consume.py
```

- Selector de vhost + cola (carga colas reales vía management API).
- **Consumir**: `GET` de N mensajes con `ack` (se eliminan).
- **Reenqueue**: los lee y los devuelve a la cola (`reject_requeue_true`).
- Muestra el raw completo: payload formateado, exchange, routing key,
  redelivered, message_count, properties.
- Accesible desde la LAN en `http://192.168.31.223:8095` (abre puerto en
  firewall con UAC la primera vez; `BIND_ADDRESS=127.0.0.1` para loopback).

### 5.3 Curl directo al management API

```sh
USER=$(grep '^RABBITMQ_USER=' .env | cut -d= -f2)
PASS=$(grep '^RABBITMQ_PASSWORD=' .env | cut -d= -f2)
VHOST=$(grep '^RABBITMQ_VHOST=' .env | cut -d= -f2)

# listar colas y su estado
curl -s -u "$USER:$PASS" "http://127.0.0.1:15672/api/queues/$VHOST?columns=name,messages,messages_ready,messages_unacknowledged,consumers"

# publicar un mensaje de prueba en el exchange real (routing key = nombre de cola)
curl -s -u "$USER:$PASS" -H "content-type: application/json" \
  -d '{
    "properties": {
      "delivery_mode": 2,
      "content_type": "application/json",
      "message_id": "68807a55-0000-0000-0000-000000000001",
      "correlation_id": "9c1f7e3d-0000-0000-0000-000000000001",
      "type": "TransportRequestCreated"
    },
    "routing_key": "prov.test.ambulancias",
    "payload": "{\"messageId\":\"68807a55-0000-0000-0000-000000000001\",\"messageType\":\"TransportRequestCreated\",\"entityPublicId\":\"REQ-2026-000041\",\"contractCode\":\"CTR-MAD-01\",\"timestamp\":\"2026-08-10T11:15:00Z\",\"retrievalUrl\":\"/api/provider/requests/REQ-2026-000041\"}",
    "payload_encoding": "string"
  }' \
  "http://127.0.0.1:15672/api/exchanges/$VHOST/nagomi.provider.notifications/publish"

# consumir raw sin ack (leer y dejar en cola)
curl -s -u "$USER:$PASS" -H "content-type: application/json" \
  -d '{"count":5,"ackmode":"ack_requeue_true","encoding":"auto"}' \
  "http://127.0.0.1:15672/api/queues/$VHOST/prov.test.ambulancias/get"

# consumir con ack (elimina de la cola)
#   ackmode: "ack_requeue_false"
```

> Los mensajes publicados a mano **no marcan `Retrieved`**: ese estado solo
> cambia cuando un cliente autenticado hace el GET del `retrievalUrl`. Son
> perfectos para probar el consumidor del proveedor sin tocar el stack.

## 6. Generar notificaciones reales (flujo completo en dev)

1. **Proveedor + contrato + ruta**: por API admin
   (`/api/provider-administration`) o, si `OAUTH_BOOTSTRAP_ENABLED=true`, se
   crea un cliente bootstrap al arrancar (client_id/secret en `.env`).
2. El proveedor debe tener `QueueName` y `IsActive=true`; el contrato y la ruta
   activos. Sin ruta activa, `ProviderOutbox.AddAsync` devuelve `null` y **no**
   se crea notificación.
3. Crear y **submit** una petición de transporte (`/api/transport-requests`
   → `POST /{id}/submit/one-off`). El endpoint persiste la entidad + la
   notificación Pending en el mismo SaveChanges.
4. En ≤10 s el worker publica en la cola del proveedor. Ver con la tool de
   §5.2 o el management API.
5. El proveedor hace `GET /api/provider/requests/{publicId}` con su token →
   la notificación pasa a `Retrieved`.

## 7. Endpoints clave (integración)

| Grupo | Endpoints | Auth |
|---|---|---|
| `/api/provider` | `GET/PUT /requests/{publicId}`, `GET/PUT /journeys/{publicId}`, `POST /journeys/{publicId}/status`, `POST /journeys/{publicId}/cancel`, `POST /requests/{publicId}/cancel`, `POST /requests/{publicId}/journeys` (journey excepcional) | client credentials del proveedor + claims de contrato |
| `/api/provider-administration` | providers/contracts/routes CRUD, clients create/rotate-secret/revoke | policy `Administration` |
| `/api/provider-integration/operations` | `GET /notifications?state=&unretrieved=&limit=`, `POST /notifications/{id}/republish` | policy `Operations` (operator/admin) |
| `/connect/token` | token endpoint OAuth (`grant_type=client_credentials`) | — |

Estado de notificación: `Pending → Published → Retrieved`, o `Dead` tras 5
fallos. Operativa: `GET /api/provider-integration/operations/notifications?state=Dead`
y `?unretrieved=true` (publicadas pero no recuperadas) para monitorizar y
republish manual.

## 8. Auth de proveedores

- Cada proveedor tiene su **propio cliente OpenIddict** y secreto, revocable.
- Token: `POST /connect/token` con `grant_type=client_credentials` → `Bearer`
  en cada llamada REST. Nunca secretos en fuentes, imágenes, mensajes, URLs o logs.
- Cada petición se valida contra el proveedor y contrato del cliente
  autenticado (`OpenIddictClaimsProviderAuthorizer`). Fuera del contrato = 404/403.
- Rotación: crear reemplazo → actualizar proveedor → verificar adquisición →
  revocar el antiguo.

## 9. Verificación (antes de dar algo por hecho)

```sh
cd /c/Users/alexlocal/projects/Nagomi
dotnet build Nagomi.slnx -v q --nologo        # 0 warnings / 0 errors
dotnet test tests/Nagomi.UnitTests/Nagomi.UnitTests.csproj --nologo
dotnet test tests/Nagomi.IntegrationTests/Nagomi.IntegrationTests.csproj --nologo  # ~2.5 min, necesita Docker
cd frontend && npm ci && npm run build && npm run lint && npm test -- --run
```

- Tests de integración con Testcontainers: un fallo de readiness del contenedor
  es **transitorio** — reejecutar antes de depurar código.
- Tests frontend con timers/polling pueden ser flaky: reejecutar una vez antes
  de asumir regresión.

## 10. Workflow de cambio (checklist)

1. Leer la spec (`openspec/specs/<área>/spec.md`) y localizar el escenario sin cubrir.
2. Dominio + store en `backend/src/Nagomi.Api/Features/<Feature>/` (entidades
   con private setters + factory `Create(...)` que lanza `DomainValidationException`).
3. PublicIds secuenciales legibles (`REQ-2026-000001`, `JRN-...`, `EMG-...`)
   vía `IPublicIdGenerator` (tabla `public_id_counters`, UPSERT atómico).
4. Si la entidad va por `ITransportDb`/`INagomiDb`/`IProviderIntegrationDb`,
   actualizar TODOS los implementadores (DbContext, `FakeTransportDb`,
   `TestTransportDb`) — añadir un miembro rompe todos.
5. Config EF en `Nagomi.Api.Infrastructure.Persistence` (el
   `ApplyConfigurationsFromAssembly` filtra por ese namespace exacto).
6. Migración con `dotnet ef migrations add <Name>` (dotnet-ef NO está global;
   `dotnet tool install --global dotnet-ef`, `$HOME/.dotnet/tools` en PATH).
7. Endpoints `Map*Endpoints` + wiring en `Program.cs` + auditoría
   (`TransportAuditRecord`).
8. Tests unit (validación de dominio) + integración (factory in-memory, sin Docker).
9. Frontend: `src/types.ts`, `src/api.ts`, página en `src/pages/`, ruta+nav en
   `src/App.tsx` (mini-router propio).

## 11. Pitfalls conocidos (dev)

- **Puerto de RabbitMQ no accesible desde el host**: red `internal: true` +
  Docker Desktop → usar `rabbit-local-override.yml` (networks `edge` +
  `ports`), nunca tocar el compose base.
- **Boot de RabbitMQ lento**: hasta ~3 min tras recreate. Poll del healthcheck.
- **`IProviderOutbox` es NoOp en la factory in-memory**: no assert de
  persistencia de notificaciones vía `/api/provider-integration/operations/*`
  (requiere policy `Operations` que el cliente anónimo no tiene). Assert el
  comportamiento observable por endpoints sin auth.
- **Mensajes de prueba manuales no cambian el estado outbox** (siguen
  `Published`). Para probar `Retrieved` hace falta el GET autenticado.
- **`ProviderOutbox.AddAsync` con ruta inactiva → null**: sin proveedor+contrato+ruta
  activos no se genera notificación, sin error.
- **Tool Python**: `self.wfile.write()` necesita `bytes` (los JSON van
  `.encode()`, la página HTML también — bug conocido si se pasa `str`).
- **Enums en JSON = números** en el backend; los tests API los leen con `GetInt32()`.
- **Tests de integración con tipos ambiguos** (`TransportReasonSnapshot` existe
  en Domain y Features.ReferenceData) → calificar con namespace completo.

## 12. IA: MCP server y asistente con tools

Nagomi expone su dominio por **MCP streamable HTTP** en `/mcp` y el chat de
ayuda (HelpChat) usa las **mismas tools** vía function calling.

- **MCP** (`Features/Mcp/NagomiMcpTools.cs`): 6 tools read-only en español
  (`buscar_pacientes`, `consultar_paciente`, `buscar_solicitudes`,
  `consultar_solicitud`, `listar_coordinacion`, `buscar_vehiculos`).
  Autenticación: Bearer de OpenIddict con rol web (cualquier usuario
  autenticado). Stack: `ModelContextProtocol.AspNetCore` 2.2.0 →
  `AddMcpServer().WithHttpTransport().WithTools<NagomiMcpTools>()` +
  `MapMcp("/mcp")`. nginx proxya `/mcp` con `proxy_buffering off` y timeouts
  largos (SSE). Probe: `scripts/nagomi_e2e_mcp.py`.
- **Asistente** (`Features/HelpChat/HelpChatTools.cs`): envía las mismas tools
  al proveedor OpenAI-compatible; si el modelo pide una tool se ejecuta en el
  backend y el resultado vuelve al modelo (loop, máx 4). Si el proveedor
  rechaza `tools` (400), degrada a chat plano. Config: `HelpChatOptions.EnableTools`.
- **Pitfall**: no usar un `JsonElement` fuera del `using` de su
  `JsonDocument` (ObjectDisposedException intermitente) — `.Clone()` dentro.
