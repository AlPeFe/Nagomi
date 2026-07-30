## Purpose

Define user-visible transport auditing with distinct operational histories, sensitive-data protection, and durable source attribution.

## Requirements

### Requirement: User-visible entity audit
The system SHALL record accepted creation, submission, update, cancellation, and deletion actions with entity, action, source, actor, Nagomi receipt timestamp, and changed fields. Users SHALL be able to inspect this history without reconstructing current state from it.

#### Scenario: Review provider modification
- **WHEN** a provider replaces a journey snapshot
- **THEN** a Nagomi user can see which permitted fields changed, when they were accepted, and the provider source

### Requirement: Separate operational histories
Audit entries, journey status events, and integration delivery records SHALL remain distinct even when presented together in a user interface.

#### Scenario: Record provider status
- **WHEN** a provider submits a journey status
- **THEN** the system records the operational event without treating it as an entity field-change audit entry

### Requirement: Sensitive audit representation
Audit history SHALL NOT retain prior full values for sensitive patient identifiers. Phone changes SHALL display masked values, while non-sensitive operational changes MAY display full before and after values.

#### Scenario: Audit document change
- **WHEN** an authorized Nagomi operation changes a patient document number
- **THEN** the audit states that the document changed without retaining or displaying its previous full value

### Requirement: Change source attribution
Audit records SHALL distinguish simulated Nagomi users from transport-provider integrations and SHALL retain actor identifiers even if credentials are later revoked.

#### Scenario: Revoke client after modification
- **WHEN** a provider client is revoked after making a change
- **THEN** the historical audit continues to identify that client and provider as the source
