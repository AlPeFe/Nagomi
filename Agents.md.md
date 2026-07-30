# AGENTS.md

## Purpose

This document defines the engineering rules and conventions that AI agents must follow when contributing to this repository.

The goal is to maintain a clean, maintainable, testable, and scalable codebase.

When multiple solutions are possible, always choose the simplest solution that follows these guidelines.

---

# Project Overview

## Technology Stack

Backend:

- .NET 10
    
- ASP.NET Core Minimal APIs
    
- Entity Framework Core
    

Database:

- SQLite as the initial database provider
    
- Designed to migrate to PostgreSQL or another relational database in the future
    

Frontend:

- Separate application from backend
    
- Frontend and backend must remain independently deployable
    

Infrastructure:

- Docker compatible
    
- OpenTelemetry enabled by default
    

---

# General Engineering Principles

Always prioritize:

- Clean Code principles
    
- Readability
    
- Maintainability
    
- Testability
    
- Simplicity
    
- Explicit code over clever code
    
- Small focused components
    
- SOLID principles
    

Avoid:

- Overengineering
    
- Premature abstractions
    
- Complex patterns without a real requirement
    
- Large classes with multiple responsibilities
    

---

# Repository Structure

The repository must keep frontend and backend separated.

Example:

```
/
├── backend/
│   └── src/
│
├── frontend/
│
├── tests/
│
├── docker/
│
└── docs/
```

The backend must not contain frontend code or assets.

---

# Architecture

## Vertical Slice Architecture

Use Vertical Slice Architecture as the main organization pattern.

Organize code by business capability and feature, not by technical layer.

Avoid global folders such as:

```
Controllers/
Services/
Repositories/
Validators/
```

that grow independently over time.

Prefer:

```
Features/

Customers/

    Create/
        Endpoint.cs
        Request.cs
        Response.cs
        Validator.cs
        Handler.cs
        Tests.cs

    Get/
        Endpoint.cs
        Response.cs
        Handler.cs
        Tests.cs
```

Each feature should own everything required for its use case.

A feature should contain:

- API endpoint
    
- Request models
    
- Response models
    
- Validation
    
- Business logic
    
- Mapping
    
- Tests
    

Features should have minimal dependencies between each other.

---

# API Development

Use ASP.NET Core Minimal APIs.

Endpoints must:

- Be small
    
- Validate input
    
- Delegate business logic
    
- Return appropriate HTTP responses
    
- Use typed results when possible
    

Do not:

- Put business logic inside endpoints
    
- Create MVC Controllers
    
- Create large endpoint files
    

---

# Commands and Queries

Separate write operations from read operations when it improves clarity.

Commands:

- Modify application state
    
- Execute business actions
    

Queries:

- Read application state
    
- Must not modify data
    

Do not introduce CQRS complexity for simple CRUD operations unless it provides clear value.

---

# Business Logic

Business rules must not live inside:

- API endpoints
    
- Database entities
    
- Infrastructure code
    

Business logic should be:

- Explicit
    
- Testable
    
- Independent from external technologies
    

---

# Database

Current database:

- SQLite
    

Future target:

- PostgreSQL or equivalent relational database
    

Because migration is expected:

- Avoid SQLite-specific features
    
- Keep database access portable
    
- Avoid vendor lock-in
    
- Do not rely on undocumented database behavior
    

---

# Entity Framework Core

Use Entity Framework Core for persistence.

Rules:

- Use explicit entity configurations
    
- Keep DbContext clean
    
- Prefer specific repositories only when they provide clear business value
    
- Avoid generic repositories
    
- Use asynchronous APIs
    
- Always propagate CancellationToken
    

---

# Dependency Injection

Use the built-in .NET dependency injection system.

Rules:

- Register dependencies explicitly
    
- Avoid service locator patterns
    
- Avoid hidden dependencies
    

---

# Validation

Validate all external input.

Preferred approach:

- FluentValidation
    

Rules:

- Input validation belongs close to the feature
    
- Business validation belongs in application logic
    
- Validation failures must return consistent responses
    

---

# Error Handling

Use consistent error handling across the API.

Rules:

- Avoid unhandled exceptions
    
- Return meaningful HTTP status codes
    
- Do not expose internal implementation details
    
- Do not leak sensitive information
    

---

# Observability

OpenTelemetry must be enabled by default.

All new functionality should consider observability.

Include:

- HTTP telemetry
    
- Database telemetry
    
- Application metrics where relevant
    
- Distributed tracing where relevant
    

Use structured logging.

Never use:

```
Console.WriteLine()
```

Use:

```
ILogger
```

Never log:

- Passwords
    
- Tokens
    
- Secrets
    
- Sensitive user data
    

---

# Configuration

Never hardcode:

- Connection strings
    
- API keys
    
- Credentials
    
- Environment-specific values
    

Use:

- appsettings.json
    
- appsettings.Environment.json
    
- Environment variables
    

Prefer the Options pattern for configuration.

---

# Docker

The application must always remain Docker compatible.

Rules:

- Do not require local machine dependencies
    
- Use environment-based configuration
    
- Keep containers reproducible
    
- New dependencies must consider container deployment
    

---

# Async Programming

Always prefer asynchronous APIs.

Rules:

- Avoid blocking calls
    
- Avoid synchronous I/O
    
- Use CancellationToken in async flows
    

---

# Testing

All business logic must be testable.

Prefer:

- Unit tests for business rules
    
- Integration tests for API behaviour
    
- Tests close to the feature they validate
    

Avoid:

- Testing implementation details
    
- Excessive mocking
    
- Tests coupled to internal structure
    

---

# Code Style

Rules:

- Use meaningful names
    
- Avoid abbreviations
    
- Keep methods small
    
- Keep classes focused
    
- Prefer composition over inheritance
    
- Avoid duplicated logic
    

Before creating:

- Base classes
    
- Generic services
    
- Helper libraries
    
- Shared abstractions
    

Verify that the duplication or complexity justifies the abstraction.

---

# AI Development Rules

When modifying the codebase:

1. Understand existing architecture before creating new code.
    
2. Follow existing patterns.
    
3. Prefer extending existing features.
    
4. Avoid introducing new architectural concepts without approval.
    
5. Keep changes focused and minimal.
    
6. Do not refactor unrelated code.
    
7. Generate production-ready code.
    

When uncertain:

- Choose the simplest maintainable solution.
    
- Ask for clarification if a decision has architectural impact.
    

---

# OpenSpec Integration

All significant features must be developed through OpenSpec.

Expected workflow:

1. Explore the requirement.
    
2. Create a proposal.
    
3. Define the design.
    
4. Generate implementation tasks.
    
5. Implement the tasks.
    
6. Verify the implementation.
    

The implementation must always match the approved specification.