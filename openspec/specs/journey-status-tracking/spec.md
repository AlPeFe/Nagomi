## Purpose

Define independent journey operations, status history, actual timestamps, cancellation metadata, and idempotent provider status updates.

## Requirements

### Requirement: Independent journey operation
Each journey SHALL have its own public identifier, direction, location snapshots, contacts, requirements, schedule, notes, provider reference, current status, and actual timestamps. Updating one journey SHALL not modify its parent request or sibling journeys unless an explicit request propagation operation is used.

#### Scenario: Change return pickup
- **WHEN** a provider changes the return journey pickup time
- **THEN** the system updates and audits only that return journey

### Requirement: Status event history
The system SHALL append journey status events without requiring a transition order. Supported statuses SHALL include `Scheduled`, `Activated`, `EnRouteToOrigin`, `ArrivedAtOrigin`, `PatientOnBoard`, `EnRouteToDestination`, `ArrivedAtDestination`, `Completed`, and `Cancelled`.

#### Scenario: Complete without intermediate statuses
- **WHEN** a provider submits `Completed` as the first status after `Scheduled`
- **THEN** the system records it and marks the journey completed

#### Scenario: Restart after cancellation
- **WHEN** a provider submits a non-terminal status later than a `Cancelled` event
- **THEN** the system records the event and reopens the journey

### Requirement: Event metadata and current status
Every status event SHALL contain provider-supplied `occurredAt`, Nagomi-generated `recordedAt`, source, actor, and optional external resource code, latitude, and longitude. The journey current status SHALL equal the status event with the greatest `occurredAt`, including when events arrive out of order.

#### Scenario: Receive delayed event
- **WHEN** an older status event is recorded after a newer event
- **THEN** the system retains the older event without replacing the newer current status

### Requirement: Actual journey timestamps
The system SHALL reflect actual activation, origin arrival, patient pickup, destination arrival, and completion timestamps on the journey from the latest corresponding status event by `occurredAt`.

#### Scenario: Replace destination arrival time
- **WHEN** a second `ArrivedAtDestination` event has a later `occurredAt`
- **THEN** the journey reflects the later event as its actual destination arrival

### Requirement: Cancellation metadata
A `Cancelled` event SHALL require a fixed cancellation reason, cancelling party, actor, and effective timestamp. The system SHALL distinguish cancellation initiated by Nagomi/requester from provider cancellation.

#### Scenario: Reject cancellation without reason
- **WHEN** a cancellation command omits its reason
- **THEN** the system rejects the command and leaves the current status unchanged

### Requirement: Status idempotency
External status commands SHALL require an idempotency key and repeated submission of the same key SHALL not create duplicate events.

#### Scenario: Repeat status command
- **WHEN** a provider repeats an already accepted status command with the same idempotency key
- **THEN** the system returns the prior successful result and stores one event
