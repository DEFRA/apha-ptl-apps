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
- PTLIMS HLD
- Existing source code
- Existing solution structure

The HLD is the source of truth for the target architecture.

Do not introduce architecture decisions that conflict with the HLD.

Preserve all business behaviour, validation rules, workflows, role restrictions, and domain rules identified during analysis.

---

# Target Solution Structure

Use the existing project layout.

src

PTL.Api

PTL.ApiClient

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
- Entity Framework Core
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

Avoid introducing additional complexity unless the migration documents require it.

Prefer the simplest maintainable solution.

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
Entity Framework Core
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

If EF Core mapping becomes difficult for a stored procedure result set, explain why and propose the simplest alternative.

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

---

# Implementation Scope

Implement only the scope required by the migration document.

If the migration document specifies Phase 1:

Implement:

- Read-only functionality
- Browse
- Search
- Details pages

Do NOT automatically implement:

- Create
- Update
- Delete
- Approval workflows
- Complex workflow engines

unless explicitly requested.

---

# Generate

## Step 1 – File Inventory

Before generating any code produce:

### Files To Create

### Files To Modify

### Dependencies

### Project Boundaries

Wait for verification in the output before generating code.

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

### Repository Interfaces

### Repository Implementations

Requirements:

- REST conventions
- Proper status codes
- Error handling
- Logging hooks
- Authorization attributes

Implement only the endpoints required by the migration document.

Example:

GET /api/customers

GET /api/customers/{id}

---

## Step 3 – API Client

Project:

PTL.ApiClient

Generate:

### Typed HttpClient

### Request Models

### Response Models

### Interface

Example:

CustomerApiClient

GetCustomersAsync()

GetCustomerAsync(Guid id)

---

## Step 4 – Internal Web

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

Example:

Features/Customer

CustomerController.cs

CustomerViewModel.cs

Views/Index.cshtml

Views/Details.cshtml

---

## Step 5 – External Web

Project:

PTL.ExternalWeb

If this domain is externally accessible:

Generate:

Features/[DOMAIN]

### Controller

### ViewModel

### Views

### Validation

### API Client Integration

If the domain is internal-only:

Explain why no ExternalWeb implementation is required.

---

## Step 6 – Validation

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

## Step 7 – Testing

Projects:

PTL.Api.Tests

PTL.InternalWeb.Tests

PTL.ExternalWeb.Tests

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

## Build Instructions

## Test Instructions

## Assumptions

For anything missing:

[NEEDS INVESTIGATION]

Preserve all existing PTLIMS business behaviour.

Do not generate mock business rules.

Follow the migration documents as the source of truth.