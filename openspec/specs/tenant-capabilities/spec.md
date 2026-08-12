## Purpose

Define how a Nagomi installation (the tenant) configures its operating role — requesting transports from external providers, executing transports with its own resources, handling emergencies, or any combination — and how billable clients (entities that are invoiced but not integrated) attach to transports.

## Requirements

### Requirement: Tenant operating capabilities
Nagomi SHALL maintain a single global tenant configuration that enables or disables one or more operating capabilities simultaneously. The capabilities SHALL be `PublishesRequests`, `ExecutesTransports`, and `HandlesEmergencies`. An administrator SHALL be able to change the enabled set at runtime, and the change SHALL immediately affect which features and endpoints are available and required.

#### Scenario: Enable multiple capabilities
- **WHEN** an administrator enables both `PublishesRequests` and `ExecutesTransports`
- **THEN** the tenant can both publish transports to external providers and execute transports with its own resources

#### Scenario: Disable publishing
- **WHEN** an administrator disables `PublishesRequests`
- **THEN** the tenant no longer exposes or requires provider contracts for transport execution

### Requirement: Publishing capability
With `PublishesRequests` enabled, Nagomi SHALL publish submitted transports to external transport providers through contracts, exactly as defined by the transport-provider-integration spec. A submitted transport routed to an external provider SHALL be durable and atomically persisted with its notification regardless of broker availability.

#### Scenario: Publish to external provider
- **WHEN** a tenant with `PublishesRequests` enabled submits a transport routed to an active external contract
- **THEN** the transport is published to that provider's queue and retrievable by the authorized provider

### Requirement: Self-execution capability
With `ExecutesTransports` enabled, Nagomi SHALL allow the tenant to register as its own transport provider (an "auto-provider") with its own active contract and queue. Submitting a transport routed to that own contract SHALL publish the notification to the tenant's own queue, so the same installation both originates and consumes the transport. The tenant SHALL be able to execute transports for itself, for a third party on whose behalf it acts, or for an external client it invoices.

#### Scenario: Execute own transport with own contract
- **WHEN** a tenant with `ExecutesTransports` enabled submits a transport routed to its own contract
- **THEN** the notification is published to the tenant's own queue and the tenant executes the transport

### Requirement: Emergency capability
With `HandlesEmergencies` enabled, Nagomi SHALL expose the emergency transport module. The emergency capability SHALL be combinable with publishing and self-execution, so an emergency operator may route a saturated emergency transport to an external provider or to its own resources.

#### Scenario: Combine emergency with publishing
- **WHEN** a tenant with `HandlesEmergencies` and `PublishesRequests` enabled handles an emergency it cannot cover
- **THEN** the emergency transport can be routed to an external provider through a contract

### Requirement: Billable client entity
Nagomi SHALL maintain billable clients that are passive billing entities — an organization or an individual that requests or is invoiced for a transport but is not integrated into Nagomi and has no queue or credentials. Each billable client SHALL carry a stable public identifier, name, tax identifier, contact, and address. A billable client SHALL NOT imply any provider or integration.

#### Scenario: Create an individual client
- **WHEN** an operator records an individual who requests a transport
- **THEN** the system stores the individual as a billable client with public identifier and contact data

#### Scenario: Client has no integration
- **WHEN** a transport is invoiced to a billable client
- **THEN** the system does not publish any notification to the client and does not require credentials for it

### Requirement: Transport execution and invoicing are independent
Every submitted transport SHALL have exactly one execution target (its routing contract) and MAY have an optional billable client to invoice. The execution target and the billable client SHALL be independent: a transport executed by the tenant's own resources MAY be invoiced to an external client, and a transport executed by an external provider MAY be invoiced to a billable client other than the tenant.

#### Scenario: Self-execute for a third party
- **WHEN** a tenant with `ExecutesTransports` enabled creates a transport executed by its own contract and invoices it to a billable client
- **THEN** the transport routes to the tenant's own queue and records the billable client as the invoiced party

#### Scenario: Execute own transport without client
- **WHEN** a tenant executes a transport with its own contract and no billable client
- **THEN** the tenant invoices itself and no client is recorded

### Requirement: Contract code optional under self-execution
A submitted transport SHALL NOT require a contract code when the tenant executes the transport with its own resources. The contract code SHALL only be required when routing to an external provider under `PublishesRequests`. A billable client SHALL be optional in all cases.

#### Scenario: Submit internal transport without contract
- **WHEN** a tenant with `ExecutesTransports` enabled submits an internal transport with no contract code but a billable client
- **THEN** the transport is accepted and routed to the tenant's own execution resources
