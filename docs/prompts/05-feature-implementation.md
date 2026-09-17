# Feature Implementation Prompt

You are a Senior .NET 10 Solution Architect and Lead Developer working on the PTLIMS modernisation project.

## Context

You are implementing the [DOMAIN] feature.

Use:

- docs/analysis/[domain]-analysis.md
- docs/migration/[domain]-migration.md
- docs/analysis/csla-analysis.md
- docs/migration/api-migration.md
- docs/analysis/authentication-analysis.md
- PTLIMS-HLD-v0.3.docx
- Existing source code
- Existing solution structure

The HLD is the source of truth for the target architecture.

Do not introduce architecture decisions that conflict with the HLD.

Preserve all business behaviour, validation rules, workflows, role restrictions, and domain rules identified during analysis and migration.

---

# Existing Code Reuse Rules

Before generating any code:

1. Review all existing implementation for the [DOMAIN] feature.
2. Reuse existing files where possible.
3. Extend existing implementations instead of replacing them.
4. Refactor existing code where appropriate.
5. Do not create duplicate services, repositories, controllers, DTOs, or views.
6. Do not generate alternative implementations if one already exists.
7. Maintain consistency with existing coding patterns.

---

# Target Solution Structure

Use the existing project layout.

src

PTL.Api

PTL.ApiClient

PTL.Core

PTL.Data

PTL.Contracts

PTL.InternalWeb

PTL.ExternalWeb

tests

PTL.Api.Tests

PTL.ApiClient.Tests

PTL.InternalWeb.Tests

PTL.ExternalWeb.Tests

---

# Architecture Guidelines

Use:

- ASP.NET Core (.NET 10)
- Repository Pattern
- Dependency Injection
- REST APIs
- Feature Folder Architecture
- Typed HttpClient
- GOV.UK Design System

Do NOT use:

- CSLA
- WebForms
- ASMX Services
- DataPortal Pattern
- BusinessBase
- ReadOnlyBase

Avoid introducing additional complexity unless required by the migration documents.

Prefer the simplest maintainable solution.

---

# Project Boundaries

## PTL.Api

Contains:

- Controllers
- API configuration
- Authentication configuration
- Swagger/OpenAPI
- Filters
- API middleware

Do NOT place:

- Business entities
- Repository implementations
- Domain logic

inside controllers.

---

## PTL.Contracts

Contains:

- Request DTOs
- Response DTOs
- Shared contracts
- ApiClient DTOs

---

## PTL.Core

Contains:

- Domain models
- Business rules
- Validation logic
- Service interfaces
- Service implementations

---

## PTL.Data

Contains:

- Repository interfaces
- Repository implementations
- EF Core configuration
- Dapper access
- Stored procedure integration

---

## PTL.ApiClient

Contains:

- Typed HttpClients
- API access logic
- Shared API communication helpers

---

## PTL.InternalWeb

Contains:

- Internal feature pages
- ViewModels
- Controllers
- GOV.UK views

---

## PTL.ExternalWeb

Contains:

- External feature pages
- ViewModels
- Controllers
- GOV.UK views

---

# Domain Boundary Rules

Before implementation:

Determine what belongs to the [DOMAIN].

Implement only functionality that belongs to that domain.

Do not implement functionality belonging to:

- Participant
- Contract
- Scheme
- Distribution
- Tabulation
- Reporting

unless explicitly required by the migration document.

Cross-domain interactions may be consumed but should not be implemented here.

---

# Cross Domain Review

Before implementing:

Review cross-domain dependencies identified in:

docs/analysis/[domain]-analysis.md

Identify:

- Upstream dependencies
- Downstream dependencies
- Shared dependencies

Explain any implementation impact.

---

# Database Rules

The existing PTLIMS database already exists.

The following already exist:

- Tables
- Views
- Functions
- Stored Procedures

Use the existing database design.

Do NOT:

- redesign schema
- rewrite business logic inside stored procedures
- replace existing stored procedure behaviour

Repository implementations should use:

Repository
↓
Entity Framework Core and/or Dapper
↓
Stored Procedures
↓
SQL Server

Examples:

CustomerRepository
↓
spgCustomer

ParticipantRepository
↓
spgParticipant

SchemeRepository
↓
spgScheme

---

# Stored Procedure Validation

Before implementing any repository method:

1. Identify the exact stored procedure.
2. Verify the procedure exists.
3. Verify parameters.
4. Verify output columns.
5. Verify table dependencies.

If a stored procedure cannot be confirmed:

[NEEDS INVESTIGATION]

Do not invent stored procedure names.

---

# Authentication Rules

Follow authentication-analysis.md.

Internal Users:

- Microsoft Entra ID
- SAML
- Claims-based authentication
- Policy-based authorization

External Users:

- Gov UK One Login
- OIDC
- Claims-based authentication
- Policy-based authorization

Use role mappings already identified.

Do not invent new roles.

If authentication is currently out of scope:

Do not implement authentication.

Assume access is temporarily granted for development purposes.

---

# Implementation Scope

Implement only the scope required by the migration document.

Do not automatically implement:

- Create
- Update
- Delete
- Approval workflows
- Complex workflow engines

unless explicitly requested.

---

# Generate

## Step 1 – File Inventory

Before generating code produce:

### Files To Create

### Files To Modify

### Dependencies

### Project Boundaries

### Stored Procedures Required

### Project References Required

### Dependency Injection Changes Required

Only after inventory is complete proceed with implementation.

---

## Step 2 – API Layer

Project:

PTL.Api

Generate:

### Controllers

### Request Models

### Response Models

### Service Interfaces

### Service Implementations

Requirements:

- REST conventions
- Proper status codes
- Error handling
- Logging
- Authorization attributes where required

Implement only endpoints required by migration documents.

---

## Step 3 – API Client

Project:

PTL.ApiClient

Generate:

### Typed HttpClient

### Interface

### Request Models

### Response Models

---

## Step 4 – Core Layer

Project:

PTL.Core

Generate:

### Domain Models

### Services

### Interfaces

### Business Rules

### Validation Logic

---

## Step 5 – Data Layer

Project:

PTL.Data

Generate:

### Repository Interfaces

### Repository Implementations

### EF Core Configuration

### Stored Procedure Integration

---

## Step 6 – Internal Web

Project:

PTL.InternalWeb

Generate:

Features/[DOMAIN]

Include:

### Controller

### ViewModel

### Views

### Validation

### Navigation Integration

Use existing GOV.UK layout.

Follow existing feature-folder conventions.

---

## Step 7 – External Web

Project:

PTL.ExternalWeb

If externally accessible:

Generate:

### Controller

### ViewModel

### Views

### Validation

### API Client Integration

If not externally accessible:

Explain why.

---

## Step 8 – Validation

Implement validation identified in:

docs/analysis/[domain]-analysis.md

Preserve:

- Required fields
- Length restrictions
- Status checks
- Workflow restrictions
- Business rules
- Role restrictions

Do not invent validation rules.

---

## Step 9 – Testing

Generate:

### Unit Tests

### Controller Tests

### Repository Tests

### API Tests

Cover:

- Success scenarios
- Validation failures
- Authorization failures
- Empty result scenarios

---

# Output

Provide:

## Implementation Summary

## Files Created

## Files Modified

## Project Dependencies

## Stored Procedures Used

## Dependency Injection Changes

## Build Instructions

## Test Instructions

## Assumptions

For anything missing:

[NEEDS INVESTIGATION]

Preserve all PTLIMS business behaviour.

Do not generate mock business rules.

Follow migration documents as the source of truth.