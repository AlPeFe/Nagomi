# Deployment

The root Compose file provides an MVP stack with PostgreSQL, RabbitMQ, the ASP.NET Core backend, and an Nginx-served frontend. PostgreSQL and RabbitMQ are reachable only on the internal Docker network. The frontend is the sole published service and proxies `/api/` to the backend.

## Prerequisites and configuration

Use a supported Docker Engine with Compose v2. Create `.env` from `.env.example`, replace every `change-me` value with independently generated high-entropy secrets, and restrict file access to deployment operators. `.env`, certificates, dumps, and import data are ignored by Git.

Validate and start the stack:

```sh
docker compose config --quiet
docker compose build --pull
docker compose up -d
docker compose ps
```

Compose waits for PostgreSQL and RabbitMQ health before starting the backend, then waits for backend `/ready` before starting the frontend. The frontend exposes `/healthz` for container health.

The stack binds to `127.0.0.1:8080` by default. Put a production reverse proxy or ingress in front of it; do not set `FRONTEND_BIND_ADDRESS=0.0.0.0` without network controls and TLS termination.

## TLS, secrets, and storage

Terminate TLS 1.2 or newer at a trusted reverse proxy/load balancer and forward only to the private frontend listener. Preserve `X-Forwarded-*` headers and set `OAUTH_ISSUER` to the exact external HTTPS origin. Provider RabbitMQ connections require TLS in production; the Compose broker port is intentionally not published. Use platform-managed certificates for OAuth signing and mount them through an environment-specific Compose override rather than storing them in the repository or image.

Compose encryption is not storage encryption. Place Docker's PostgreSQL and RabbitMQ volumes on encrypted disks or use managed services with encryption at rest, protected snapshots, access logging, and tested key recovery. Restrict host and Docker daemon access because both volumes contain operational data. Rabbit notifications must remain minimal and contain no patient, address, phone, clinical, notes, or transport-requirement data. Redact connection strings, secrets, tokens, patient data, and payloads from logs and telemetry.

Rotate database, broker, OAuth client, and signing secrets using overlapping credentials where supported. Take and verify backups before rotations or upgrades. Restart affected services after changing `.env`; changing a database or broker password also requires updating the account in the service, not only editing Compose variables.

## Database migrations and imports

Compose applies migrations at backend startup. Production orchestrators may set `Database__MigrateOnStartup=false` and run migrations as a controlled release job. Before releasing application containers:

1. Back up PostgreSQL and verify the backup artifact.
2. Apply migrations through startup or `dotnet ef database update` from a controlled release job.
3. Run the idempotent INE geography import with `POST /api/reference-data/imports/ine` using bounded JSON or NDJSON.
4. Convert the trusted 2025 CNH workbook to UTF-8 CSV and send it as `text/csv` to `POST /api/reference-data/imports/cnh`.
5. Verify counts, relationships, duplicate handling, and application health before exposing traffic.

Import files may contain controlled operational data: validate checksums and source provenance, scan files, use read-only staging, and remove temporary copies after verification. The API rejects XLSX input and never executes formulas, macros, or links.

## Backups and restore

Back up PostgreSQL with a consistent logical `pg_dump` custom-format backup or platform-native snapshot. Encrypt backups, separate backup credentials from runtime credentials, apply retention and off-site copies, and test restores on a schedule. Back up RabbitMQ definitions (users, virtual hosts, policies, exchanges, queues, and bindings), but treat PostgreSQL integration/outbox records as the recovery source of truth rather than relying on queued-message backups.

Example logical backup and restore, run from an access-controlled operator shell:

```sh
docker compose exec -T postgres sh -c 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Fc' > backups/nagomi.dump
docker compose exec -T postgres sh -c 'pg_restore -U "$POSTGRES_USER" -d "$POSTGRES_DB" --clean --if-exists' < backups/nagomi.dump
```

Stop application writes before an in-place restore. Prefer restoring into a new database, validating migrations, record counts, public identifiers, audit/outbox continuity, and provider routing, then switching traffic. A database restore may reintroduce already published outbox work; rely on message IDs and command idempotency and coordinate provider reconciliation.

## Operations and recovery

Monitor container health, disk capacity, PostgreSQL connections/replication, Rabbit queue depth, outbox age, dead notifications, published-but-unretrieved notifications, and OAuth failures. Export OpenTelemetry to the configured collector without sensitive attributes.

Nagomi-originated changes are committed with outbox records even during broker outages. Publication retries five times at one-minute intervals, then records dead delivery. After correcting the broker or route, operators republish pending/dead/unreceived work through the application recovery operation. Recovery creates a new message identifier pointing to current REST state; do not manually inject clinical payloads into RabbitMQ or mutate outbox/database records.

For upgrades, back up first, pull/build immutable image versions, run migrations/imports, start the dependencies, verify health, and then expose the frontend. Pin images by digest and use a production orchestrator or Compose override for resource limits, centralized secrets, TLS certificates, and external managed persistence.
