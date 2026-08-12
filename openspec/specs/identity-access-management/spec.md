# Identity & Access Management (OpenIddict central)

## Purpose

Nagomi embeds a central identity and access-management system built on OpenIddict.
It is the single place an administrator manages who and what can use the platform:

- **Users** — human accounts (the tenant is always single-tenant; there is exactly one
  tenant, the deployment's own organization). Administrators can list, create, edit,
  deactivate and delete users and assign the `admin` / `default` roles.
- **API clients** — machine-to-machine (M2M) credentials used by external consumers
  to call Nagomi's API with the OAuth 2.0 **client credentials** flow. A consumer
  creates a confidential client, receives a `client_id` + `client_secret`, and obtains
  short-lived bearer tokens from `/connect/token`.
- Every endpoint of this system is **administrator-only**.

## Requirements

### Req: Identity system is administrator-only
- **SHALL** require the `admin` role for every identity-management endpoint.
- **WHEN** a non-admin authenticated user calls an identity endpoint
- **THEN** the request is rejected with `403 Forbidden`.

### Req: Single-tenant user management
- **SHALL** expose CRUD over the platform's user accounts (list, create, update
  including role/active/password, deactivate/delete).
- **SHALL** keep a single logical tenant: there is no multi-tenancy; all users belong
  to the deployment.
- **WHEN** a user is created
- **THEN** the account is assigned `default` unless an explicit role is supplied.
- **WHEN** a user is deleted or deactivated
- **THEN** their existing tokens and sessions no longer authenticate.

### Req: API clients for consumers (M2M)
- **SHALL** let an administrator create a confidential OpenIddict application for an
  external consumer (machine-to-machine).
- **SHALL** grant the `client_credentials` grant type and the token endpoint to the
  created client.
- **SHALL** return the generated `client_secret` exactly once at creation time.
- **SHALL** allow listing clients, rotating a client secret, and revoking/deleting a
  client.
- **WHEN** a client secret is rotated or the client revoked
- **THEN** every token previously issued to that client is revoked immediately.
- **SHALL** never expose a stored `client_secret` again after creation (rotation
  issues a new value; storage is one-way hashed by OpenIddict).

### Req: Tokens are short-lived
- **SHALL** issue access tokens with a bounded lifetime (access tokens, not
  long-lived refresh tokens, for M2M).
- **WHEN** a consumer calls an API with a bearer token obtained via client credentials
- **THEN** the request is authorized if the client is valid and unrevoked.

## Scenarios

#### Scenario: Administrator creates a consumer API client
- **WHEN** an admin posts a new API client with a display name and client ID
- **THEN** the response contains the `client_id` and a freshly generated
  `client_secret`, and the consumer can obtain a token with
  `grant_type=client_credentials`.

#### Scenario: Non-admin is blocked
- **WHEN** an authenticated non-admin user calls any identity endpoint
- **THEN** the API returns `403`.

#### Scenario: Rotating a secret invalidates old tokens
- **WHEN** an admin rotates a client's secret
- **THEN** the old tokens are revoked and a new secret is returned.

#### Scenario: Managing users stays single-tenant
- **WHEN** an admin lists, creates, updates or deactivates users
- **THEN** operations apply to the deployment's own accounts without any tenant
  discriminator (single-tenant by design).
