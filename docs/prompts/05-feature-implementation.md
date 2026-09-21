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

# Legacy User Experience Preservation

PTLIMS is a business-critical operational application used by long-term users.

This is a technology modernisation project, not a business process redesign project.

The primary objective is:

- Modernise technology
- Modernise architecture
- Modernise maintainability

while preserving existing user experience and business behaviour.

Existing PTLIMS users should be able to move from the legacy application to the modern application with minimal retraining.

The target system should feel like:

"PTLIMS on a modern platform"

and not:

"a completely new application."

---

## Preserve Existing Business Terminology

Preserve all existing:

- Field Labels
- Display Names
- Page Titles
- Page Headings
- Menu Names
- Section Names
- Tab Names
- Button Labels
- Grid Column Names
- Lookup Names
- Business Terminology

Do not rename business concepts unless explicitly requested by the business.

Examples:

Keep:

- UT Number
- FT Number
- Participant
- Participant Scheme
- Scheme
- Contract Signatory
- Actions Required
- Renewal Information
- Administration Charge
- Data Consent Declaration
- Postage Pricing Plan

Do not introduce alternative terminology simply because it sounds more modern.

---

## Preserve Existing Screen Layout

Preserve:

- Page Structure
- Section Structure
- Grouping Of Fields
- Existing User Workflow
- Existing Screen Navigation

Use GOV.UK components while maintaining the legacy screen layout as closely as practical.

---

## Preserve Existing Field Order

Field order is part of business workflow.

Maintain the same field sequence as the legacy screen wherever practical.

Do not reorder fields for visual or design reasons.

If field order changes:

- Explain why
- Identify business impact

Otherwise preserve existing ordering exactly.

---

## Preserve Existing Visibility Behaviour

Preserve:

- Hidden Fields
- Hidden Controls
- Hidden Sections
- Conditional Visibility Rules
- Show / Hide Logic

Existing visibility behaviour is considered business functionality.

---

## Preserve Existing ReadOnly Behaviour

Preserve:

- ReadOnly Fields
- ReadOnly Sections
- ReadOnly Screen States

Examples:

- Contract.IsReadOnly
- Scheme.IsReadonly
- Participant ReadOnly behaviour

Existing users expect these states.

---

## Preserve Existing Enable / Disable Behaviour

Preserve:

- Enabled Controls
- Disabled Controls
- Conditionally Editable Controls

Examples:

If:

RequiresAssessment

is:

- Enabled During Create
- Disabled During Edit

then preserve exactly the same behaviour.

Do not make previously disabled fields editable unless explicitly required.

---

## Preserve Existing Workflow Behaviour

Preserve:

- Create Workflows
- Edit Workflows
- Renewal Workflows
- Copy Workflows
- Approval Workflows
- Navigation Workflows
- Save Behaviour

Do not simplify workflows without an explicit migration decision.

---

## Preserve Existing Validation Behaviour

Preserve:

- Required Fields
- Length Restrictions
- Validation Messages
- Status Checks
- Workflow Restrictions
- Conditional Validation Rules
- Business Rule Validation

Do not replace existing validation behaviour with simplified alternatives.

---

## Minimise User Retraining

Assume users are already familiar with PTLIMS.

Do not redesign screens simply because newer UX approaches exist.

User familiarity takes precedence over UI redesign unless the migration documents specifically require change.

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

- PTL.Api
- PTL.ApiClient
- PTL.Core
- PTL.Data
- PTL.Contracts
- PTL.InternalWeb
- PTL.ExternalWeb

tests

- PTL.Api.Tests
- PTL.ApiClient.Tests
- PTL.InternalWeb.Tests
- PTL.ExternalWeb.Tests

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

Avoid introducing additional complexity unless required by migration documents.

Prefer the simplest maintainable solution.

---

# Domain Boundary Rules

Before implementation:

Determine what belongs to the [DOMAIN].

Implement only functionality that belongs to that domain.

Cross-domain interactions may be consumed but should not be implemented here unless explicitly required.

---

# Cross Domain Review

Before implementing:

Review cross-domain dependencies identified in:

docs/analysis/[domain]-analysis.md

Identify:

- Upstream dependencies
- Downstream dependencies
- Shared dependencies

Explain implementation impact.

---

# Legacy Behaviour Preservation Checklist

Before generating implementation code review the legacy screens and document:

## Field Order

Identify:

- Existing Field Order
- Existing Section Order

Preserve both.

---

## Hidden Controls

Identify:

- Hidden Fields
- Hidden Sections
- Hidden Actions

Preserve visibility behaviour.

---

## ReadOnly Controls

Identify:

- ReadOnly Fields
- ReadOnly Sections
- ReadOnly Screens

Preserve behaviour.

---

## Enable / Disable Behaviour

Identify:

- Enabled Controls
- Disabled Controls
- Conditionally Editable Controls

Preserve behaviour.

---

## Conditional Visibility Rules

Identify:

- Show Rules
- Hide Rules
- Conditional Sections
- Conditional Actions

Preserve behaviour.

---

## Validation Rules

Identify:

- Required Fields
- Length Rules
- Conditional Rules
- Workflow Rules

Preserve behaviour.

---

## Workflow Rules

Identify:

- Create Flow
- Edit Flow
- Save Flow
- Copy Flow
- Renewal Flow
- Approval Flow

Preserve behaviour.

---

## Navigation Rules

Identify:

- Entry Pages
- Exit Pages
- Navigation Flow

Preserve behaviour.

---

## Behaviour Mapping

Before implementation provide:

Legacy Behaviour
↓
Target Behaviour

for:

- Field Order
- Hidden Controls
- ReadOnly Controls
- Enable/Disable Controls
- Visibility Rules
- Validation Rules
- Workflow Rules
- Navigation Rules

Demonstrate how behaviour is preserved.

If behaviour cannot be confirmed:

[NEEDS INVESTIGATION]

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
Dapper
↓
Stored Procedures
↓
SQL Server

---

# Stored Procedure Validation

Before implementing any repository method:

1. Identify exact procedure.
2. Verify procedure exists.
3. Verify parameters.
4. Verify output columns.
5. Verify table dependencies.

If a procedure cannot be confirmed:

[NEEDS INVESTIGATION]

Do not invent procedure names.

---

# Authentication Rules

Follow authentication-analysis.md.

If authentication / authorization are currently out of scope:

- Do not implement authentication.
- Do not implement authorization.
- Preserve security requirements.
- Preserve future role mappings.
- Preserve future policy requirements.
- Document expected security behaviour for later implementation.

Assume temporary development access only.

Do not invent new roles.

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

PTL.Api

Generate:

- Controllers
- Request Models
- Response Models
- Service Interfaces
- Service Implementations

Requirements:

- REST conventions
- Proper status codes
- Error handling
- Logging

Only implement endpoints required by migration documents.

---

## Step 3 – API Client

PTL.ApiClient

Generate:

- Typed HttpClient
- Interface
- Request Models
- Response Models

---

## Step 4 – Core Layer

PTL.Core

Generate:

- Domain Models
- Services
- Interfaces
- Business Rules
- Validation Logic

---

## Step 5 – Data Layer

PTL.Data

Generate:

- Repository Interfaces
- Repository Implementations
- EF Core Configuration
- Stored Procedure Integration

---

## Step 6 – Internal Web

PTL.InternalWeb

Generate:

Features/[DOMAIN]

Include:

- Controller
- ViewModel
- Views
- Validation
- Navigation Integration

Use existing GOV.UK layout.

Follow existing feature-folder conventions.

---

## Step 7 – External Web

PTL.ExternalWeb

If externally accessible generate:

- Controller
- ViewModel
- Views
- Validation
- API Client Integration

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
- Field order
- Hidden controls
- ReadOnly controls
- Enable/Disable behaviour
- Visibility rules
- Navigation behaviour
- Existing user workflow

Do not invent validation rules.

---

## Step 9 – Testing

Generate:

- Unit Tests
- Controller Tests
- Repository Tests
- API Tests

Cover:

- Success scenarios
- Validation failures
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