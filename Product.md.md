# PRODUCT.md

# Product Overview

## Vision

Open source software (FOSS) focused on helping small patient transportation companies, clinics, nursing homes, and geriatric centers manage their own patient transport operations.

The product allows organizations to plan, coordinate, and track patient transportation using their own vehicles and operational resources.

The initial goal is to provide a simple and reliable management system for small organizations that need control over their daily patient transport activity without depending on external transportation platforms.

---

# Target Users

## Primary Users

### Patient Transportation Companies

Small companies operating their own ambulances and requiring tools to manage:

- Patients
    
- Vehicles
    
- Transport planning
    
- Daily operations
    
- Transport history
    

---

### Clinics

Organizations that need to request and coordinate patient transportation for:

- Medical appointments
    
- Transfers between facilities
    
- Recurring treatments
    

---

### Nursing Homes and Geriatric Centers

Organizations requiring frequent patient transport management for:

- Medical visits
    
- Treatments
    
- Scheduled recurring transfers
    

---

# User Identity

## Authentication

The MVP must provide basic user authentication.

The purpose is only to identify users and provide controlled access to the system.

The MVP does not require role-based access control (RBAC).

Initial authentication capabilities:

- User registration or provisioning
    
- User login
    
- User identity management
    
- Session management
    

Authorization rules and advanced permissions will be introduced in future versions.

---

# Core Domain Concepts

## Patient

A patient represents the person receiving transportation services.

A patient should contain basic information required to manage transport operations.

Examples:

- Personal identification data
    
- Contact information
    
- Operational notes
    

The patient entity should support being associated with multiple transports over time.

---

## Vehicle

A vehicle represents an ambulance or transport vehicle owned or managed by the organization.

The MVP should maintain basic vehicle information.

Examples:

- Identifier
    
- Registration information
    
- Description
    
- Operational status
    

Advanced fleet management is outside the MVP scope.

---

## Transport

A transport represents the operational request to move a patient.

A transport connects:

- A patient
    
- An origin
    
- A destination
    
- A planned schedule
    
- One or more journeys
    

A transport may represent:

- A single event
    
- A recurring scheduled activity
    

---

# Journey Model

A transport can contain one or more journeys.

A journey represents an individual movement operation.

Examples:

- Going from home to hospital
    
- Returning from hospital to home
    

Journeys are independent operational units.

Although journeys may belong to the same transport, each journey can have its own:

- Date and time
    
- Origin
    
- Destination
    
- Status
    
- Vehicle assignment
    
- Notes
    

This allows real operational scenarios.

Example:

A patient has a medical appointment:

Journey 1:

Home → Hospital

Status:

Completed

Journey 2:

Hospital → Home

Status:

Cancelled

Reason:

Patient returned by other means.

---

# Transport Creation

Users can create transport requests.

A transport contains:

- Patient
    
- One or more journeys
    
- Scheduling information
    
- Additional notes
    

---

## Single Transport

A transport scheduled for one specific occurrence.

Example:

A patient appointment on a specific date.

---

## Recurring Transport

A transport that generates future journeys according to a recurrence pattern.

Examples:

- Weekly dialysis
    
- Regular medical treatments
    
- Rehabilitation sessions
    

The MVP should support recurrence without introducing unnecessary scheduling complexity.

---

# Transport Management

## Coordinator View

The system must provide an operational dashboard for coordinators.

The purpose is to quickly understand current and upcoming activity.

Information should include:

- Active transports
    
- Pending journeys
    
- Scheduled activity
    
- Current statuses
    
- Important operational information
    

---

## Transport History

The system must provide historical visibility.

Users should be able to review:

- Completed journeys
    
- Cancelled journeys
    
- Future scheduled activity
    
- Previous transport activity
    

Filtering and searching should be supported.

---

## Transport Detail

Each transport should provide a complete overview.

Information includes:

- Patient information
    
- Journeys
    
- Origin and destination
    
- Scheduling
    
- Status
    
- Notes
    
- Operational details
    

---

# Status Management

The MVP should use a simple status lifecycle.

Avoid complex workflow engines.

Initial journey statuses:

- Scheduled
    
- In Progress
    
- Completed
    
- Cancelled
    

Statuses may evolve based on real operational requirements.

---

# Future Expansion

Future versions may introduce:

- External organizations requesting transports
    
- External users tracking requests
    
- Multi-company collaboration
    
- Advanced permissions and RBAC
    
- Advanced fleet management
    
- Driver management
    
- Optimization and route planning
    

These capabilities are not part of the MVP.

---

# Product Principles

The product should prioritize:

- Simplicity
    
- Operational clarity
    
- Reliability
    
- Easy adoption
    
- Real-world workflow support
    

Avoid:

- Complex authorization models before needed
    
- Over-engineered workflows
    
- Features without operational value
    

---

# MVP Success Criteria

The MVP should allow an organization to:

1. Authenticate users.
    
2. Manage patients.
    
3. Manage basic vehicles.
    
4. Create transports.
    
5. Generate single and recurring transports.
    
6. Manage independent journeys.
    
7. Monitor operational activity.
    
8. Review transport history.
    
9. Track basic transport lifecycle.