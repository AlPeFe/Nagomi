# Capacidades del tenant y facturación

Nagomi soporta varios **roles operativos** que un mismo tenant (la empresa que lo despliega) puede habilitar en paralelo. Los roles **no son excluyentes**: una pyme de ambulancias suele ser a la vez publicadora, ejecutora y de urgencias. Todo se configura desde la pestaña **Configuración** del panel (solo administradores).

## Capacidades habilitables

- **Publicar solicitudes (`PublishesRequests`)** — el tenant actúa como cliente: crea traslados y los enruta a proveedores externos mediante contratos. Es el modo clásico descrito en `provider-integration.md`.
- **Ejecutar transportes (`ExecutesTransports`)** — el tenant se ejecuta sus propios traslados. Para ello se registra automáticamente como **auto-proveedor** (ver abajo) con su propio contrato y cola.
- **Gestionar urgencias (`HandlesEmergencies`)** — habilita el flujo de transporte urgente/emergencias.

Las tres están activas por defecto. Se activan/desactivan de forma independiente y surten efecto en tiempo de ejecución (no requieren desplegar nada).

### Endpoints de administración

- `GET  /api/admin/tenant/capabilities` — estado actual de las tres flags.
- `PUT  /api/admin/tenant/capabilities` — actualiza las flags (cuerpo: `{ publishesRequests, executesTransports, handlesEmergencies }`).
- `GET  /api/admin/tenant/clients` — lista de clientes facturables.
- `POST /api/admin/tenant/clients` — crea un cliente facturable.
- `PUT  /api/admin/tenant/clients/{id}` — actualiza un cliente.
- `DELETE /api/admin/tenant/clients/{id}` — desactiva un cliente.

## Auto-proveedor (self-execution)

Cuando la capacidad **Ejecutar transportes** está activa, el sistema garantiza la existencia de un proveedor propio:

- **Código**: `SELF`
- **Cola**: `nagomi.self` (configurable)
- **Contrato**: `SELF`, enrutado al auto-proveedor.

Un traslado que use el contrato `SELF` se publica en la cola propia del tenant, igual que se publicaría en la de un proveedor externo. El tenant puede entonces consumir su propia cola para ejecutar el traslado internamente, o ignorarlo y delegar a otra herramienta.

### Ejecución y facturación son independientes

Un traslado tiene dos dimensiones ortogonales:

- **`contractCode`** — quién lo **ejecuta** (el enrutamiento/publicación).
- **`clientId`** — a quién se le **factura** (el cliente).

Pueden combinarse libremente:

| Escenario | contractCode | clientId |
|---|---|---|
| Ejecutar yo, facturar al paciente | `SELF` | *(vacío → a sí mismo)* |
| Ejecutar yo, facturar a una mutua | `SELF` | `CLI-…` (Mutua Pepe) |
| Ejecutar un tercero, facturar a la mutua | contrato del tercero | `CLI-…` (Mutua Pepe) |
| Solo publicar a un tercero | contrato del tercero | *(vacío)* |

### `ContractCode` opcional

El contrato es **opcional** al crear un traslado. Es obligatorio solo cuando se va a **publicar a un proveedor externo** (sin contrato, el traslado se guarda y queda como "no publicado"). Bajo self-execution esto cubre el caso de un traslado interno que no necesita notificación.

## Clientes facturables (`TransportClient`)

Un cliente facturable es una **entidad pasiva de facturación**: organización (mutua, clínica, ayuntamiento) o particular al que se presta el transporte. **No tiene cola ni credenciales** de Nagomi — no se conecta al sistema, solo aparece en la factura.

Campos: `publicId` (generado, prefijo `CLI-`, p. ej. `CLI-2026-000001`), `name`, `taxId`, contacto, teléfono, email, dirección, y `isActive`.

El `clientId` de un traslado referencia a uno de estos clientes. Los clientes **inactivos** no aparecen por defecto en el selector del formulario.

## Ver lo que hay publicado en la cola de RabbitMQ

Desde la pestaña **Configuración → Colas** se puede inspeccionar qué hay publicado en cada cola de proveedor, **sin consumir mensajes**:

- **Snapshot por cola** (`GET /api/queue`): para cada proveedor activo muestra su cola, número de mensajes listos y consumidores conectados.
- **Peek de mensajes** (`GET /api/queue/{cola}/peek?limit=N`): devuelve hasta `N` mensajes **distintos** con su metadata (`entityPublicId`, `contractCode`, `messageType`, `redelivered`) y cuerpo completo. El peek es **no destructivo**: los mensajes se re-encolan y no se pierden.

Esta vista ayuda a operadores a confirmar que los traslados se están publicando (por ejemplo tras un `Submit`), a depurar redelivery, o a verificar que la cola del auto-proveedor recibe los `SELF`.

### Formato de la notificación (recordatorio)

La notificación en cola es un **aviso ligero**, nunca el traslado completo:

```json
{
  "messageId": "527439e7-6afc-4e10-8a20-321af9547e12",
  "messageType": "TransportRequestCreated",
  "entityPublicId": "REQ-2026-000001",
  "contractCode": "SELF",
  "timestamp": "2026-08-11T18:22:33.048045+00:00",
  "retrievalUrl": "/api/provider/requests/REQ-2026-000001"
}
```

Para ver el detalle completo se consulta `retrievalUrl` con el token del proveedor (ver `provider-integration.md`).
