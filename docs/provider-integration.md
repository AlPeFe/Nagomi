# Provider integration

Nagomi uses RabbitMQ only as an at-least-once change signal. Authenticated REST is the canonical contract for reading current data and submitting changes. Provider access is limited to the contracts assigned to that provider.

## OAuth 2.0 client credentials

Each provider receives a separate, revocable OpenIddict client and secret. Obtain a short-lived bearer token from the deployment's token endpoint using `grant_type=client_credentials`; send it as `Authorization: Bearer <token>` on every provider REST call. Never place client secrets in source, images, RabbitMQ messages, URLs, or logs. Rotate a secret by issuing a replacement, updating the provider, verifying token acquisition, then revoking the old credential.

The token endpoint is `/connect/token`. Administrators manage credentials through `/api/provider-authentication/clients`, including create, rotate, and revoke operations. OAuth does not replace authorization: Nagomi verifies the authenticated client's provider and allowed contract for every retrieval and command. A request outside those contracts is inaccessible.

## RabbitMQ notifications

Each provider consumes only its isolated durable queue over TLS. Credentials should be unique to that provider and restricted to its virtual host and queue. Use manual acknowledgements, durable connections, heartbeats, and reconnect with exponential backoff.

A notification contains only:

```json
{
  "messageId": "globally-unique-id",
  "messageType": "request.changed",
  "entityId": "public-request-or-journey-id",
  "contractCode": "CONTRACT-CODE",
  "occurredAt": "2026-07-29T10:00:00Z",
  "retrievalUrl": "/provider/requests/public-id"
}
```

Patient identity, addresses, phone numbers, clinical data, notes, and transport requirements must never appear in a notification. Validate the deployed message schema before production because message type names and URL paths may evolve with the API implementation.

Consume as follows:

1. Record `messageId` in a durable inbox with a unique constraint.
2. If already recorded, acknowledge the duplicate without repeating side effects.
3. Retrieve `retrievalUrl` with the provider bearer token.
4. Apply the returned current snapshot transactionally in the provider system.
5. Commit the inbox record and provider-side changes, then acknowledge RabbitMQ.

Rabbit acknowledgement is transport-level only. The authenticated REST retrieval is Nagomi's business receipt confirmation and marks the corresponding notification `Retrieved`.

## REST reads and writes

Always follow the notification retrieval URL rather than constructing it. Retrieval returns current state, not a historical snapshot from notification time. Request and journey updates are independent complete-snapshot operations; the last snapshot accepted by Nagomi wins. Dedicated endpoints are used for status events and request or journey cancellations. Providers may add exceptional journeys but cannot delete journeys or change patient identity, transport reason, or recurrence.

Every provider write requires a stable idempotency key, normally sent in `Idempotency-Key`. Generate one key per logical command, persist it before sending, and reuse it for every retry. A timeout is an unknown outcome: retry the same command and key rather than generating a new key. Status events require offset-bearing `occurredAt` values and may arrive out of order; `Completed` is terminal while a later non-terminal event can reopen `Cancelled`.

Propagate the Nagomi correlation/message identifier in the API's supported correlation header and retain it with the provider command and response. Do not log bearer tokens or sensitive request/response bodies.

## Failure and recovery

Nagomi persists publishable changes in a transactional outbox, so application changes remain committed while RabbitMQ is unavailable. Publication is retried five times at one-minute intervals before the notification becomes dead. Delivery is at least once, so duplicate notifications are normal and must be harmless.

Operators monitor pending, dead, and published-but-unretrieved notifications. For dead or unreceived work, an operator manually republishes a new notification with a new `messageId`; it points to the current REST snapshot. Providers must not replay an old cached payload. If REST is temporarily unavailable, leave the Rabbit delivery unacknowledged or retry retrieval according to the agreed queue policy. Repeated authorization failure requires credential/contract correction, not blind retries.
