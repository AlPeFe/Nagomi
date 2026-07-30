## ADDED Requirements

### Requirement: Transport reason master
The system SHALL provide configurable transport reasons with code, description, and active state. A request SHALL select exactly one active reason and preserve its code and description snapshot when the master changes or is deactivated.

#### Scenario: Deactivate used reason
- **WHEN** an administrator deactivates a reason used by historical requests
- **THEN** those requests retain their recorded reason while new requests cannot select it

### Requirement: Spanish geographic reference data
The system SHALL maintain INE municipality, province, and autonomous-community codes and relationships for Spanish addresses. Address fields SHALL remain minimally constrained and optional unless required by another business rule.

#### Scenario: Select INE municipality
- **WHEN** a user selects a municipality from the INE catalog
- **THEN** the system associates its province and autonomous community

### Requirement: National hospital catalog
The system SHALL import hospitals from the 2025 National Hospital Catalogue, including CCN, CODCNH, name, official address text, available structured address, phone, geographic codes, source year, and active state. Official identifiers SHALL resolve an existing official facility rather than create duplicates.

#### Scenario: Resolve official hospital
- **WHEN** a request supplies a known official hospital code
- **THEN** the system resolves the matching healthcare facility

### Requirement: Manual healthcare facilities
Users SHALL be able to create manual healthcare facilities for centers absent from the official hospital catalog. A manual facility SHALL have a name and MAY have one phone, partial structured address, coordinates, and notes.

#### Scenario: Add non-catalog clinic
- **WHEN** a user supplies a name for a clinic absent from the official catalog
- **THEN** the system creates a manual healthcare facility with its own Nagomi public identifier

### Requirement: Location types and snapshots
Origins and destinations SHALL be either `PrivateAddress` or `HealthcareFacility`. A private-to-private journey SHALL be invalid. Selecting a facility SHALL copy its reference, official identifiers, name, address, phone, and coordinates into editable request and journey snapshots; editing a snapshot SHALL not change the facility master.

#### Scenario: Preserve historical facility data
- **WHEN** a facility master changes after a journey was submitted
- **THEN** the submitted journey retains its prior location snapshot

#### Scenario: Reject private-to-private journey
- **WHEN** both journey endpoints are private addresses
- **THEN** the system rejects the journey

### Requirement: Structured operational addresses
Location snapshots SHALL support structured street, number, block, staircase, floor, door, additional details, postal code, municipality, province, optional latitude and longitude, and separate origin/destination operational observations.

#### Scenario: Record pickup instructions
- **WHEN** a user adds origin observations to a journey
- **THEN** the provider-visible journey snapshot includes those origin observations
