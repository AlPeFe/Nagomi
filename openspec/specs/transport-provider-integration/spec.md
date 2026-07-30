## Purpose

Define secure, durable provider routing, notifications, retrieval, writes, delivery recovery, and machine authentication.

## Requirements

### Requirement: Contract-based provider routing
Nagomi SHALL maintain transport providers and predefined transport contracts. Each active contract SHALL route to exactly one provider, and provider credentials SHALL authorize access only to requests within that provider's contracts. A request SHALL remain valid if no publishable contract is available.

#### Scenario: Request without publishable contract
- **WHEN** a submitted request has no active provider route
- **THEN** the system stores the active request and exposes it as not published

### Requirement: Durable independent publication
Committing a submitted request or Nagomi-originated publishable change SHALL persist its integration notification atomically with domain changes. Failure to publish SHALL NOT roll back or reject the domain operation.

#### Scenario: Submit while broker unavailable
- **WHEN** RabbitMQ is unavailable during request submission
- **THEN** the request remains active and its notification remains pending for later publication

### Requirement: Minimal RabbitMQ notifications
The system SHALL use one provider queue and publish notifications containing message identifier, message type, entity public identifier, contract code, timestamp, and retrieval URL without patient, address, phone, clinical, or transport-requirement payloads.

#### Scenario: Publish request notification
- **WHEN** a request is ready for provider publication
- **THEN** RabbitMQ receives only the minimal notification metadata

### Requirement: Retry and dead-message recovery
Failed RabbitMQ delivery SHALL retry five times at one-minute intervals and then become dead. An operator SHALL be able to republish unreceived work using a new message identifier, and retrieval SHALL return the current entity snapshot.

#### Scenario: Republish dead notification
- **WHEN** an operator republishes a dead notification
- **THEN** the system creates a new traceable notification for the current snapshot

### Requirement: REST retrieval confirmation
An authenticated provider SHALL retrieve current request and journey snapshots through REST. Retrieval of the notified resource SHALL mark that notification retrieved; RabbitMQ publication or acknowledgement alone SHALL not mark the business payload retrieved.

#### Scenario: Confirm receipt by retrieval
- **WHEN** the authorized provider retrieves a notified request
- **THEN** the system records the provider and retrieval timestamp for that notification

### Requirement: Provider write contracts
The REST API SHALL provide independent complete-snapshot updates for requests and journeys plus dedicated status and cancellation endpoints. Providers SHALL be able to update permitted general request data, journeys, journey requirements, provider references, and add exceptional journeys, but SHALL NOT delete journeys, change patient identity, transport reason, or recurrence.

#### Scenario: Update one journey snapshot
- **WHEN** an authorized provider submits a valid complete snapshot for one journey
- **THEN** the system replaces that journey's permitted current data, audits the change, and does not echo a notification to the same provider

### Requirement: Last accepted snapshot wins
Nagomi SHALL assign the authoritative acceptance timestamp and SHALL apply complete snapshots in acceptance order, with the last accepted snapshot replacing prior permitted entity data.

#### Scenario: Accept stale complete snapshot last
- **WHEN** a provider snapshot based on older data is accepted after a Nagomi update
- **THEN** its permitted values become current and the overwritten values remain visible through audit

### Requirement: Message and command idempotency
Notifications SHALL have globally unique message identifiers, and provider write commands SHALL require idempotency keys. Repeated delivery or submission SHALL not duplicate domain effects.

#### Scenario: Redeliver Rabbit notification
- **WHEN** RabbitMQ delivers the same message identifier more than once
- **THEN** the provider can identify the duplicate and Nagomi retains one notification record

### Requirement: Provider machine authentication
Provider systems SHALL authenticate through OpenIddict OAuth 2.0 client credentials. Each revocable client SHALL be associated with a provider and its allowed contracts.

#### Scenario: Revoke provider client
- **WHEN** an administrator revokes a provider integration client
- **THEN** that client can no longer obtain tokens or access provider endpoints

### Requirement: Protected integration data
The system SHALL encrypt external traffic, keep sensitive payloads out of notification messages and logs, and authorize every retrieval and update against the caller's provider contracts.

#### Scenario: Access request outside contract
- **WHEN** a provider requests an entity outside its authorized contracts
- **THEN** the system denies access without exposing the entity data
