## Purpose

Define the lifecycle, content, scheduling, recurrence, cancellation, and operational requirements of patient transport requests and journeys.

## Requirements

### Requirement: Draft and submission lifecycle
The system SHALL allow an incomplete transport request to be saved as a draft, audited, and physically deleted before submission. On explicit submission, the system SHALL validate the request, assign public identifiers, generate journeys, set the request active, and prevent physical deletion or return to draft.

#### Scenario: Submit a valid draft
- **WHEN** a user submits a valid draft
- **THEN** the system creates its journeys, assigns request and journey public identifiers, marks it active, and schedules provider notification

#### Scenario: Delete a draft
- **WHEN** a user deletes a request that has not been submitted
- **THEN** the system physically removes the draft and does not notify a provider

### Requirement: Request content
The system SHALL store optional basic patient details directly on each request, exactly one required active transport reason, default origin and destination, transport requirements, a contract/provider, private notes, and provider-visible notes. Requests for the same patient SHALL remain independent.

#### Scenario: Create request without patient identity
- **WHEN** a user provides a reason and valid operational transport data but no patient identity fields
- **THEN** the system accepts the request

#### Scenario: Protect reason from provider changes
- **WHEN** a provider snapshot attempts to change the transport reason
- **THEN** the system leaves the request reason unchanged

### Requirement: One-off journey generation
The system SHALL generate either one outbound journey or one outbound and one return journey for a one-off request. It SHALL NOT create a return-only one-off request.

#### Scenario: Generate round trip
- **WHEN** a user submits a one-off round-trip request
- **THEN** the system generates one outbound and one return journey linked to the request

### Requirement: Scheduling rules
The system SHALL require an appointment timestamp for an outbound journey to a healthcare facility, default its scheduled start to one hour before the appointment, and allow an optional pickup timestamp. A return journey SHALL have a pickup timestamp and MAY mark a 23:59 value as pending. The interval from outbound scheduled start to return pickup SHALL NOT exceed 24 hours and MAY cross midnight.

#### Scenario: Overnight return within limit
- **WHEN** an outbound starts at 22:00 and its return pickup is 01:30 the following day
- **THEN** the system accepts the schedule

#### Scenario: Reject excessive round trip
- **WHEN** a return pickup is more than 24 hours after the outbound scheduled start
- **THEN** the system rejects the schedule

### Requirement: Recurring request generation
The system SHALL support an inclusive recurrence interval of at most six months, selected weekdays, and distinct outbound/return schedules per weekday. A submitted recurring request SHALL contain and publish all generated journeys.

#### Scenario: Generate weekday recurrence
- **WHEN** a user submits a recurrence selecting Monday and Wednesday across a valid interval
- **THEN** the system generates the configured journeys for every matching date including matching boundary dates

### Requirement: Recurrence updates and exceptions
Only Nagomi users SHALL modify recurrence. The system SHALL preview additions and cancellations and require confirmation before applying them. Individual or manually added journeys SHALL be whole-journey exceptions; propagation SHALL ask whether to overwrite exceptions and SHALL never modify completed or cancelled journeys. Reapplying a pattern SHALL NOT create duplicate occurrences.

#### Scenario: Remove recurring weekday
- **WHEN** a user confirms removal of a weekday from a recurrence
- **THEN** the system cancels its non-terminal future journeys and notifies the provider of the resulting changes

#### Scenario: Preserve exception
- **WHEN** a header update is applied without permission to overwrite exceptions
- **THEN** an individually edited journey retains all of its current values

### Requirement: Request and journey cancellation
The system SHALL support dedicated cancellation operations. Cancelling a request SHALL cancel all non-completed journeys; cancelling one journey SHALL not cancel its siblings. Submitted records SHALL remain stored.

#### Scenario: Cancel one occurrence
- **WHEN** a user or provider cancels one journey
- **THEN** only that journey is cancelled and cancellation metadata is recorded

### Requirement: Transport requirements
The system SHALL support mobility values `Autonomous`, `Wheelchair`, and `Stretcher`, defaulting to `Autonomous`; optional oxygen with numeric concentration and flow liters per minute; and boolean companion, medical staff, isolation, bariatric, and stairs-assistance requirements. Wheelchair and stretcher SHALL be mutually exclusive. Header requirements SHALL seed journeys and both parties MAY update request or journey requirements.

#### Scenario: Reject incompatible mobility
- **WHEN** an update selects both wheelchair and stretcher
- **THEN** the system rejects the update

#### Scenario: Propagate request requirements
- **WHEN** a user confirms propagation of changed request requirements
- **THEN** the system copies them to eligible non-terminal journeys according to the selected exception policy
