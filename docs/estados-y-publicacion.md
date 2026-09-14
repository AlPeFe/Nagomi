# Estados, motivos y publicación (modelo acordado)

Decisión de producto (2026-09). Sustituye al modelo de "auto-proveedor SELF".

## Estados del traslado

Las fases ya existen en `Domain/TransportEnums.cs` (`JourneyStatus`) y **no se migran**:

| # | Enum | UI |
|---|------|----|
| 0 | `Scheduled` | Programado |
| 1 | `Activated` | Activado (el vehículo lo ha recibido) |
| 2 | `EnRouteToOrigin` | Hacia origen |
| 3 | `ArrivedAtOrigin` | En origen |
| 4 | `PatientOnBoard` | Paciente recogido |
| 5 | `EnRouteToDestination` | En traslado |
| 6 | `ArrivedAtDestination` | En destino |
| 7 | `Completed` | Completado |
| 8 | `Cancelled` | Cancelado |

**Pendiente NO es un estado del enum: es derivado.** Un traslado `Scheduled` **con**
vehículo asignado es *Programado*; **sin** vehículo es *Pendiente*. Se calcula en
lectura (`VehicleId is null`), así que no hay verdad duplicada, no cambia el contrato
con el proveedor y la app Android sigue igual.

### Cancelación

Un traslado puede cancelarse desde cualquier fase no terminal y **exige motivo**
(`CancellationReason`, ya existente) y parte (`CancellingParty`: `Requester` /
`TransportProvider`). Motivos fijos actuales: `NoLongerRequired`, `PatientUnavailable`,
`MedicalReason`, `SchedulingConflict`, `ProviderUnavailable`, `Other`. A futuro serán
un maestro; hoy el enum es suficiente y no requiere migración.

## Motivo del traslado (catálogo por defecto)

Tres valores fijos, en el selector del formulario de solicitud:

- **Alta**
- **Tratamiento Programado**
- **Interhospitalario**

Se guardan como `ReasonCode`/descripción en la solicitud y se propagan a sus
traslados. (Antes: "Consulta externa / Alta hospitalaria / Tratamiento programado /
Traslado entre centros".)

## Cliente/proveedor y destino de publicación

El concepto `SELF` (auto-proveedor con cola `nagomi.self`) **se elimina**. Siempre hay
una parte cliente/proveedor, y el modelo separa dos campos con propósitos distintos:

1. **Parte (informativo, siempre presente)** — cliente o proveedor del servicio: quién
   encarga y en nombre de quién se ejecuta. Es un dato de negocio, no de transporte.
   Nunca determina a qué cola se publica.
2. **Destino de publicación (routing)** — dónde se publica el mensaje. Se **rellena
   automáticamente** desde la relación configurada para esa parte en el menú de
   Clientes/Proveedores.

La relación parte → destino se define en el menú **Clientes y proveedores**
(primer nivel, ya no dentro de Configuración): cada parte puede tener una relación con
un proveedor y una **cola** asociada.

### Según la capacidad operativa del tenant

- **Solo publicador** (`PublishesRequests`): **una cola por contrato/cliente**. El
  cliente ES el criterio de enrutado.
- **Empresa de ambulancias** (`ExecutesTransports`): el cliente **no** define la cola —
  se publica para tus propias ambulancias. La parte cliente/proveedor queda como campo
  informativo y el destino de publicación es el propio (o nulo si no se publica).

Es decir: el mismo campo "parte" sirve en ambos modos, y solo cambia de dónde sale el
**destino de publicación** (del cliente en modo publicador; fijo/propio en modo flota).

## Publicación: qué se queda y qué se corrige

Hoy: outbox transaccional → `ProviderOutboxWorker` → RabbitMQ (exchange + una cola por
proveedor, con dead-letter) → el proveedor hace `GET` a la `RetrievalUrl` y se marca
`Retrieved`. Estados reales de la notificación: `Pending`, `Published`, `Retrieved`,
`Dead` (`ProviderNotification.State`).

Se mantiene entero, con dos correcciones:

1. **`JourneyRecord.RetrievalState` es un campo muerto**: nadie lo escribe en el
   backend y la UI lo pinta siempre como "Sin publicar". El estado de entrega que ve el
   usuario debe **derivarse de la notificación** (`ProviderNotification.State`), no de
   ese campo. Mientras no exista la derivación, la UI no debe mostrar un estado falso.
2. Con `SELF` fuera, "sin publicar" deja de ser un estado ambiguo y pasa a ser explícito:
   **No aplica** (ejecución propia sin proveedor externo) o el estado real de la
   notificación (Publicado / Recibido / Error).

## Impacto por capa

- **Dominio/enum**: sin cambios (Pendiente derivado, motivos fijos ya existen).
- **Backend**: fila de operaciones ampliada (vehículo, conductor, observaciones) para
  que Operación no dependa de Coordinación; CRUD de partes/relaciones con cola;
  retirada del provisionado `SELF`; derivación del estado de entrega.
- **Frontend**: Operación (trabajo diario, sin filtros de estado ni dirección),
  Histórico (filtros avanzados, **vacío hasta aplicar filtros**), menú de Clientes y
  proveedores, mapa de vehículos, marcado manual de estados en el detalle.
- **Android**: sin cambios de contrato de estados; el pareado y `/hubs/dispatch` siguen
  igual.
