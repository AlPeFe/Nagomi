## Purpose

Define the user-facing transport request and journey tracking views, filters, refresh indicators, privacy controls, and exports.

## Requirements

### Requirement: Request creation and navigation
The frontend SHALL provide a transport request form, request list and detail, journey tracking list and detail, navigation from a journey to its parent request, and filtering of a request's journeys by status with active journeys shown by default.

#### Scenario: Navigate from journey to request
- **WHEN** a user selects the parent request from a journey detail
- **THEN** the system displays that request and its active journeys

### Requirement: Default journey tracking view
The tracking list SHALL default to non-completed, non-cancelled journeys from yesterday through tomorrow and sort ascending by operational time, using outbound scheduled start and return scheduled pickup.

#### Scenario: Open tracking view
- **WHEN** a user opens journey tracking without saved preferences
- **THEN** the system displays active journeys from yesterday through tomorrow in operational order

### Requirement: Pending return-time presentation
When a return uses the 23:59 placeholder with `pickupTimePending`, the frontend SHALL present the time as pending rather than as an exact 23:59 pickup.

#### Scenario: Display unknown return time
- **WHEN** a listed return journey has `pickupTimePending`
- **THEN** its time column displays a pending-time label

### Requirement: Journey list content and privacy
The journey list SHALL show operational time, patient name, phone, origin, destination, direction, reason, requirements, current status, provider, and retrieval state. It SHALL NOT show national identifiers or health-card numbers.

#### Scenario: Display journey row
- **WHEN** a user views the tracking list
- **THEN** each row contains operational fields but excludes patient identity document numbers

### Requirement: Filtering and search
Users SHALL be able to filter journeys by date range, status, provider, contract, direction, reason, origin municipality, destination municipality, and retrieval state, and search by request identifier, journey identifier, provider reference, patient name, document number, or phone.

#### Scenario: Find provider journey
- **WHEN** a user searches with a provider journey reference
- **THEN** matching journeys within the installation are returned

### Requirement: Periodic refresh and update indicators
The tracking view SHALL periodically refresh and SHALL visually indicate external entity modification, provider cancellation, unreceived notification, and dead notification without implementing an incident workflow.

#### Scenario: Show provider cancellation
- **WHEN** periodic refresh observes a journey newly cancelled by its provider
- **THEN** the list visibly identifies the provider cancellation

### Requirement: Operational export
Users SHALL be able to export the current filtered journey result set in CSV or Excel-compatible form while respecting the list's exclusion of sensitive identifiers by default.

#### Scenario: Export filtered journeys
- **WHEN** a user exports a filtered tracking result
- **THEN** the downloaded file contains the filtered operational rows and excludes national and health-card identifiers
