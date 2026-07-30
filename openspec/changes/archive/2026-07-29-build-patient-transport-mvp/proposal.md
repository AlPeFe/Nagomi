## Why

Small hospitals, clinics, nursing homes, and geriatric centers need a reliable system of record for creating and tracking patient transport requests fulfilled by contracted external transport providers. The MVP must replace informal coordination with traceable requests, operational journey statuses, resilient provider notification, and a clear daily view without introducing fleet planning or complex authorization.

## What Changes

- Introduce draft, one-off, and recurring transport requests containing patient details, routing defaults, reasons, requirements, private notes, and provider-visible notes.
- Generate independently managed outbound and return journeys, including up to six months of recurring journeys and explicit per-journey exceptions.
- Track current journey status and append-only operational status events, including cancellation reasons, external resource codes, timestamps, actors, and optional coordinates.
- Provide configurable transport reasons, Spanish INE geography, and a healthcare facility catalog seeded from the 2025 National Hospital Catalogue while allowing manually maintained facilities.
- Integrate contracted transport providers through RabbitMQ notification and authenticated REST retrieval and update endpoints, with durable delivery tracking, retries, dead-message recovery, and idempotency.
- Provide request management, journey tracking, filtering, search, audit history, provider update indicators, and CSV/Excel export.
- Use simulated human identity for the MVP and OpenIddict client credentials for provider systems. RBAC remains outside scope.
- Use PostgreSQL and Docker-based deployment with OpenTelemetry-enabled observability.
- Exclude internal vehicle assignment, driver management, route optimization, provider acceptance workflows, advanced alerts, geocoding, and multiple integration mechanisms.

## Capabilities

### New Capabilities
- `transport-request-management`: Draft, submission, one-off and recurring request generation, patient snapshots, scheduling, requirements, recurrence propagation, cancellation, and identifiers.
- `journey-status-tracking`: Independent journey updates, current status, status history, reopening, actual timestamps, external resources, and cancellation metadata.
- `transport-reference-data`: Transport reason master data, Spanish geographic reference data, healthcare facility catalog import, manual facilities, and location snapshots.
- `transport-provider-integration`: Contract-based provider routing, RabbitMQ notifications, REST snapshots and updates, delivery confirmation, retries, dead-message recovery, authentication, and idempotency.
- `transport-audit`: User-visible audit history for entity changes with actor/source attribution and protection of sensitive previous values.
- `transport-operations-view`: Request and journey navigation, default operational filters, search, update indicators, periodic refresh, and export.

### Modified Capabilities

None.

## Impact

- Introduces a .NET 10 ASP.NET Core Minimal API backend organized by vertical slices and a separately deployable frontend.
- Introduces PostgreSQL, Entity Framework Core, RabbitMQ, OpenIddict, OpenTelemetry, and Docker infrastructure.
- Defines public REST contracts for transport providers and a minimal RabbitMQ notification schema containing no clinical payload.
- Stores personal and health-related transport data, requiring encryption in transit, protected storage, redacted logging, and careful audit representation.
- Replaces SQLite as the initial database choice documented in the repository guidance.
