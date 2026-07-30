## Context

Nagomi will be the system of record used by one requesting organization, such as a hospital or clinic, while contracted transport providers continue using their own systems. A submitted transport request can contain one-off or up to six months of recurring journeys. Providers must receive changes reliably and report operational data without using Nagomi's human interface.

The repository currently contains product guidance but no application implementation or existing OpenSpec capabilities. Human identity is simulated in this MVP. Provider machine identity, sensitive health data, durable integration, and user-visible traceability are required from the beginning.

## Goals / Non-Goals

**Goals:**

- Establish a portable vertical-slice .NET backend and separately deployable frontend.
- Model requests, independently mutable journeys, recurrence, status events, reference data, contracts, integration delivery, and audit history.
- Keep request persistence independent from external-system availability.
- Protect personal and health-related data in storage, transport, messages, logs, and audit views.
- Keep business status, entity audit, and integration delivery as separate concerns.

**Non-Goals:**

- Internal vehicle, driver, crew, dispatch, route planning, or optimization functionality.
- RBAC, provider-facing human screens, provider acceptance/rejection, or contract reassignment.
- Multiple integration transports, real-time UI push, geocoding, advanced alerts, or event sourcing.
- A patient master record or cross-request patient deduplication.

## Decisions

### Architecture and persistence

Use .NET 10 ASP.NET Core Minimal APIs, Entity Framework Core, PostgreSQL, and vertical slices grouped by business capability. PostgreSQL replaces the earlier SQLite assumption because the MVP already requires server-side Docker infrastructure and durable concurrent integration processing. Persist current entity state directly; audit and journey events are append-only histories but are not used to rehydrate entities.

Use a transactional outbox so submitting or changing a request commits the domain state and pending integration notification atomically. A background publisher sends outbox notifications independently, ensuring user operations succeed even when RabbitMQ is unavailable.

### Domain boundaries and lifecycles

`TransportRequest` owns patient details, reason, default locations and requirements, contract/provider routing, notes, optional recurrence, and generated `Journey` records. Journeys own operational copies of locations, contacts, requirements, schedules, current status, actual timestamps, and status events.

Keep three independent lifecycles:

```text
Request:      Draft -> Active -> Completed | Cancelled
Journey:      current status derived from latest occurred status event
Integration: Pending -> Published -> Retrieved | Dead
```

Drafts can be incomplete and physically deleted. Submission generates public identifiers and journeys, makes the request active, and creates a notification. Submitted data is never physically deleted.

### Recurrence and exceptions

A recurring request remains one aggregate containing all generated journeys. The pattern selects weekdays and per-day outbound/return schedules across an inclusive interval of at most six months. All journeys are generated and published on submission.

An individually edited or manually added journey is a whole-journey exception. Header or recurrence updates preview their impact and ask the human user whether to overwrite exceptions. Completed and cancelled journeys are never changed by propagation. Applying the same pattern repeatedly must not duplicate an occurrence. The provider can change normal request data and journeys but cannot change recurrence.

### Scheduling

Outbound journeys use an appointment timestamp as the expected destination time and default `scheduledStartAt` to one hour earlier; pickup may also be supplied. Return journeys require a pickup timestamp. A placeholder return pickup of 23:59 is accompanied by `pickupTimePending` so the UI does not present it as an exact time. A request may cross midnight but the interval from calculated outbound start to return pickup must not exceed 24 hours.

### Status model

Store journey status events independently from audit entries. Initial statuses are `Scheduled`, `Activated`, `EnRouteToOrigin`, `ArrivedAtOrigin`, `PatientOnBoard`, `EnRouteToDestination`, `ArrivedAtDestination`, `Completed`, and `Cancelled`. Events have no required sequence and may arrive late. Each contains provider-supplied `occurredAt`, Nagomi-generated `recordedAt`, source/actor, optional external resource code and coordinates, and cancellation metadata where applicable.

`CurrentStatus` is materialized from the event with the greatest `occurredAt`; actual journey timestamps are materialized from the latest corresponding event. `Completed` cannot reopen in the MVP; `Cancelled` can. Duplicate commands are prevented by an idempotency key.

### Provider integration ADR

Use RabbitMQ for minimal notifications and REST as the canonical data and command contract. RabbitMQ was selected over webhooks because it natively buffers provider downtime, supports acknowledgement, retry topology, and dead-letter queues, and avoids polling as the normal flow. REST-only polling was rejected because it delays delivery; full clinical payloads in RabbitMQ were rejected to minimize sensitive-data persistence and duplicated contracts.

One queue per provider receives notifications routed by Nagomi's predefined `TransportContract`. Messages contain `messageId`, type, public entity identifier, contract code, timestamp, and retrieval URL, but no patient or clinical details. REST returns current snapshots and independently updates requests, journeys, cancellations, and status events.

Rabbit publishing retries five times at one-minute intervals before dead-lettering. Rabbit acknowledgement marks only the notification as published/consumed. An authenticated REST retrieval marks its corresponding notification `Retrieved`. Operators recover unreceived/dead work manually by publishing a new message identifier that points to the current snapshot.

Delivery is at least once, so consumers and Nagomi write endpoints use idempotency keys. One active provider is authorized per contract, and OpenIddict OAuth 2.0 client credentials link each revocable integration client to its provider and contracts.

### Concurrency and updates

REST updates use complete snapshots for the selected request or journey. Nagomi assigns the authoritative receipt timestamp, and the last accepted complete update wins. This intentionally allows a stale provider snapshot to overwrite newer fields in the MVP; every accepted change is recorded in user-visible audit history. Header and journey write operations remain independent even when read snapshots include both.

Provider-originated changes are persisted without echoing them back to the same provider. Nagomi-originated submitted changes create notifications. Cancellation and status commands use dedicated endpoints rather than general snapshot replacement.

### Reference data and snapshots

Import hospitals from the 2025 Spanish National Hospital Catalogue, retaining CCN, CODCNH, name, official address text, structured address where available, phone, INE municipality/province/autonomous-community codes, source year, and active state. Users can create manual healthcare facilities. Official codes resolve existing official records rather than creating duplicates.

Locations are `PrivateAddress` or `HealthcareFacility`; private-to-private journeys are invalid. Selecting a facility copies its reference, official codes, name, address, phone, and coordinates into request/journey snapshots. Operational edits do not mutate the master record.

### Security, audit, and observability

Use TLS for all external communication, PostgreSQL/storage encryption supplied by the deployment platform, secret-based configuration, redacted structured logging, and no clinical data in RabbitMQ notifications. Audit entries identify entity, action, source, actor, receipt time, and changed fields. Ordinary values can show before/after data; sensitive identifiers record only that they changed, and phone values are masked.

Enable OpenTelemetry for HTTP, EF Core/PostgreSQL, RabbitMQ publishing/consumption, and background processing. Include correlation identifiers across outbox messages, Rabbit notifications, REST retrievals, and provider commands.

### Deployment

Use Docker Compose for local/MVP deployment with backend, frontend, PostgreSQL, and RabbitMQ services. Database migrations and hospital/INE seed imports are explicit deployment steps. Simulated human identity supplies a stable actor and requesting organization until interactive authentication is introduced.

## Risks / Trade-offs

- [Complete-snapshot last-write-wins can lose concurrent field updates] -> Preserve user-visible audit history and keep field-level concurrency as a post-MVP enhancement.
- [One request can contain hundreds of journeys and produce large snapshots] -> Enforce the six-month limit and provide journey-specific retrieval/update endpoints.
- [RabbitMQ access increases provider operational complexity] -> Keep messages minimal, provide REST recovery, and document queue/client setup.
- [Rabbit ACK does not prove provider persistence] -> Treat authenticated REST retrieval as the functional receipt confirmation.
- [23:59 is an artificial pending return time] -> Persist `pickupTimePending` and render it as unknown in the UI.
- [Current status based on provider event time trusts external clocks] -> Retain `recordedAt` for forensic comparison and require offset-bearing timestamps.
- [Simulated human identity gives all users equivalent access] -> Limit deployment access and defer RBAC and interactive identity to a dedicated change.
- [Audit history can duplicate sensitive information] -> Mask phone values and do not store previous sensitive identifier values.
