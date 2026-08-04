# Nagomi

Open-source patient transport coordination for small hospitals, clinics, nursing homes, and geriatric centers.

Nagomi records one-off and recurring transport requests, generates independently managed journeys, tracks operational status events, integrates contracted transport providers through minimal RabbitMQ notifications and authenticated REST APIs, and registers geolocated emergency transports with a map-driven incident point.

## Stack

- .NET 10, ASP.NET Core Minimal APIs, Entity Framework Core
- PostgreSQL
- RabbitMQ with transactional outbox delivery
- OpenIddict OAuth 2.0 client credentials
- React 19 and TypeScript
- Docker Compose and OpenTelemetry

## Run Locally

```sh
./nagomi.sh up
```

The first run creates `.env` from `.env.example` with random passwords, builds the
stack (postgres, rabbitmq, backend, frontend) and starts it. Other commands:
`./nagomi.sh down`, `./nagomi.sh status`, `./nagomi.sh logs`, `./nagomi.sh restart`.

Manual equivalent:

1. Create a local environment file:

   ```sh
   cp .env.example .env
   ```

2. Replace every `change-me` value in `.env`.

3. For a local development deployment, set:

   ```env
   ASPNETCORE_ENVIRONMENT=Development
   OAUTH_ISSUER=http://localhost:8080
   ```

4. Start the stack:

   ```sh
   docker compose up --build --wait
   ```

5. Open `http://localhost:8080`.

The backend exposes liveness at `/health`, readiness at `/ready`, and tokens at `/connect/token`.

## Authentication

- **Web users** authenticate with the OpenIddict password grant at `/connect/token`
  (`grant_type=password`). The initial administrator is created on startup from
  `NAGOMI_ADMIN_EMAIL` / `NAGOMI_ADMIN_PASSWORD` (defaults `admin@nagomi.local` /
  `change-me-admin-123`). Change it before exposing the service.
- **Roles**: `admin` manages users through `/api/admin/users` (list, create, update
  role/active/password, delete); `default` works with the web normally without user
  management access.
- **Provider systems** authenticate with OpenIddict OAuth 2.0 client credentials
  (see `Authentication__ProviderIntegration__*` in `.env.example`).

## Verify

```sh
dotnet test Nagomi.slnx
cd frontend
npm ci
npm audit
npm test -- --run
npm run lint
npm run build
```

## Documentation

- [`Product.md.md`](Product.md.md): product vision and scope
- [`docs/deployment.md`](docs/deployment.md): deployment, migrations, imports, backups, and recovery
- [`docs/provider-integration.md`](docs/provider-integration.md): OAuth, RabbitMQ, REST, idempotency, and failure handling
- [`openspec/specs/`](openspec/specs/): behavioral specifications

## Security

Nagomi processes personal and health-related transport information. Production deployments must use TLS, encrypted storage, managed secrets, restricted provider queues, and perimeter controls for the MVP's simulated human identity. RabbitMQ notifications intentionally contain no patient or clinical payload.
