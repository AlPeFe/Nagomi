## 1. Solution and Infrastructure

- [x] 1.1 Create the .NET 10 backend solution, vertical-slice project structure, test projects, and separately deployable frontend workspace
- [x] 1.2 Add Docker Compose services for backend, frontend, PostgreSQL, and RabbitMQ with environment-based secrets and health checks
- [x] 1.3 Configure EF Core PostgreSQL persistence, migrations, cancellation-token propagation, consistent API errors, and simulated human/requesting-organization identity
- [x] 1.4 Configure OpenTelemetry for HTTP, PostgreSQL, RabbitMQ, and background workers with structured sensitive-data-safe logging

## 2. Reference Data

- [x] 2.1 Implement INE autonomous-community, province, and municipality entities, seed/import process, and lookup endpoints
- [x] 2.2 Implement transport reason master data management with active-state rules and historical request snapshots
- [x] 2.3 Implement healthcare facility entities, official/manual source handling, structured addresses, one phone, notes, coordinates, and public identifiers
- [x] 2.4 Build an idempotent importer for the 2025 National Hospital Catalogue retaining CCN, CODCNH, official address text, geographic codes, source year, and active state
- [x] 2.5 Add healthcare facility search, official-code resolution, and manual facility creation APIs with integration tests

## 3. Transport Request Domain

- [x] 3.1 Implement transport request, patient details, notes, location snapshots, provider references, and fixed transport-requirement persistence/configuration
- [x] 3.2 Implement draft create/read/update/delete features with incomplete-data support and audit recording
- [x] 3.3 Implement one-off submission validation, public request/journey identifiers, outbound-only and round-trip journey generation, and no-delete rules
- [x] 3.4 Implement scheduling calculations and validation for appointment time, one-hour default start, return pickup pending marker, overnight journeys, and 24-hour maximum
- [x] 3.5 Implement request and journey snapshot update features, protected provider fields, complete-snapshot last-write-wins behavior, and parent/child independence
- [x] 3.6 Implement dedicated request and journey cancellation features with fixed reasons, cancelling party, actor, and affected-journey behavior
- [x] 3.7 Add unit and integration tests for lifecycle, locations, scheduling, requirements, identifiers, snapshot updates, and cancellation rules

## 4. Recurrence

- [x] 4.1 Implement recurrence patterns with inclusive dates, six-month maximum, selected weekdays, and per-weekday outbound/return configuration
- [x] 4.2 Generate all recurrence journeys idempotently on submission while enforcing one active outbound and optional return per date
- [x] 4.3 Implement whole-journey exception tracking for individually edited and manually added journeys
- [x] 4.4 Implement recurrence impact preview and confirmed application for weekday/schedule changes, additions, cancellations, and exception overwrite choice
- [x] 4.5 Prevent provider recurrence updates and prevent propagation into completed or cancelled journeys
- [x] 4.6 Add recurrence generation, boundary, duplicate-prevention, propagation, and exception integration tests

## 5. Journey Status Tracking

- [x] 5.1 Implement journey status event persistence with occurred/recorded timestamps, source, actor, external resource code, optional coordinates, and idempotency key
- [x] 5.2 Implement unordered status ingestion and materialized current status based on greatest occurred timestamp
- [x] 5.3 Materialize actual activation, origin arrival, pickup, destination arrival, and completion timestamps from latest corresponding events
- [x] 5.4 Implement cancellation-status metadata, cancelled journey reopening, and completed journey terminal behavior
- [x] 5.5 Add status history queries and tests for out-of-order, repeated, skipped, reopened, cancelled, and completed event flows

## 6. Audit

- [x] 6.1 Implement append-only entity audit records with entity/action/source/actor/receipt-time/change metadata independent from statuses and integration delivery
- [x] 6.2 Implement changed-field comparison for complete snapshots with masked phone history and no previous full sensitive identifier values
- [x] 6.3 Add user-visible audit query endpoints and tests for simulated-user and provider attribution after credential revocation

## 7. Provider Contracts and Authentication

- [x] 7.1 Implement transport provider, contract, allowed-route, and one-active-provider-per-contract persistence and administration APIs
- [x] 7.2 Configure OpenIddict client-credentials flow and associate revocable integration clients with providers and allowed contracts
- [x] 7.3 Enforce provider-contract authorization on every provider retrieval and write endpoint with security integration tests

## 8. Durable Provider Integration

- [x] 8.1 Implement transactional outbox records and create publishable notifications atomically with submitted Nagomi-originated domain changes
- [x] 8.2 Implement minimal RabbitMQ notification contracts without patient or clinical payloads and provision one isolated queue per provider
- [x] 8.3 Implement background publication, five one-minute retries, dead-letter handling, correlation, and delivery-state persistence
- [x] 8.4 Implement authenticated REST retrieval of current request and journey snapshots and mark corresponding notifications retrieved
- [x] 8.5 Implement provider request/journey complete-snapshot endpoints, exceptional-journey addition, dedicated cancellation endpoints, and status endpoints
- [x] 8.6 Implement idempotency handling for provider commands and notification/message tracing
- [x] 8.7 Implement operator queries for pending/dead/unretrieved notifications and manual republish with a new message identifier and current snapshot target
- [x] 8.8 Add integration tests for broker outage during submission, retries, dead letters, duplicate delivery, retrieval confirmation, authorization, and no-echo provider updates

## 9. Operations Frontend

- [x] 9.1 Implement request draft/submission form covering patient details, reason, locations, requirements, notes, contract, one-off scheduling, and recurrence
- [x] 9.2 Implement request list/detail with journey filtering, recurrence impact preview/confirmation, cancellation, audit, and integration delivery views
- [x] 9.3 Implement journey tracking list defaulting to active journeys from yesterday through tomorrow and sorting by outbound start or return pickup
- [x] 9.4 Add tracking columns, pending-time rendering, provider/retrieval state, filters, and searches while excluding sensitive identifiers from rows
- [x] 9.5 Implement journey detail, parent-request navigation, editable snapshots, status history, cancellation, external modification indicators, and periodic refresh
- [x] 9.6 Implement CSV and Excel-compatible export of the current filtered operational result without sensitive identifiers
- [x] 9.7 Add responsive frontend tests for core request creation, recurrence, tracking, filtering, navigation, privacy, and provider-update flows

## 10. Verification and Deployment

- [x] 10.1 Add end-to-end tests from draft submission through Rabbit notification, REST retrieval, provider update, status completion, audit, and operational display
- [x] 10.2 Verify TLS-ready configuration, storage protection assumptions, secret rotation, log redaction, and absence of sensitive RabbitMQ payloads
- [x] 10.3 Verify PostgreSQL migrations, INE/CNH imports, backup guidance, container startup ordering, health checks, and clean Docker deployment
- [x] 10.4 Document provider OAuth setup, queue consumption, idempotency, retrieval confirmation, status/cancellation APIs, retry behavior, and manual recovery
- [x] 10.5 Run backend, frontend, integration, and end-to-end test suites and validate implemented behavior against all change specifications
