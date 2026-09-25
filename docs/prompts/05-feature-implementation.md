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

### Preserve Existing Labels And Text

Existing PTLIMS users are already familiar with the terminology used throughout the application.

Preserve all existing:

- Screen labels
- Field labels
- Section titles
- Help text
- Button text
- Navigation text
- Grid column names
- Link text
- Dropdown labels
- Validation messages

Do not generate alternative labels.

Do not modernise wording.

Do not rename fields.

Examples:

Keep:

- UT Number
- FT Number
- Contract Signatory
- Actions Required
- Renewal Information
- Create Customer
- Review Pending Orders
- Import Permit(s)
- Contract Items

Do not change to:

- Contract Number
- Financial Reference
- Signatory Name
- Required Actions
- Customer Creation
- Order Review

The legacy wording is the source of truth.

Users should see the same terminology they are familiar with in the legacy application.

If a label exists in the legacy application, use the same label in the migrated application.

Only change labels if:

- Explicitly requested by the business
- Proven to be incorrect
- Identified as a defect in the legacy application

### Preserve Existing Labels And Text

Existing PTLIMS users are already familiar with the terminology used throughout the application.

Preserve all existing:

- Screen labels
- Field labels
- Section titles
- Help text
- Button text
- Navigation text
- Grid column names
- Link text
- Dropdown labels
- Validation messages

Do not generate alternative labels.

Do not modernise wording.

Do not rename fields.

Examples:

Keep:

- UT Number
- FT Number
- Contract Signatory
- Actions Required
- Renewal Information
- Create Customer
- Review Pending Orders
- Import Permit(s)
- Contract Items

Do not change to:

- Contract Number
- Financial Reference
- Signatory Name
- Required Actions
- Customer Creation
- Order Review

The legacy wording is the source of truth.

Users should see the same terminology they are familiar with in the legacy application.

If a label exists in the legacy application, use the same label in the migrated application.

Only change labels if:

- Explicitly requested by the business
- Proven to be incorrect
- Identified as a defect in the legacy application


## Preserve Existing Screen Layout

Preserve:

- Page Structure
- Section Structure
- Grouping Of Fields
- Existing User Workflow
- Existing Screen Navigation

Use GOV.UK components while maintaining the legacy screen layout as closely as practical.

### Legacy Screen Parity

Before implementing any page:

Review the legacy screen.

Preserve:

- Labels
- Field order
- Section order
- Grouping
- Navigation
- Actions
- Hyperlinks
- Grid columns
- Workflow

Do not redesign the screen.

Do not remove columns.

Do not introduce additional fields, filters, links, actions or controls unless explicitly required by the migration document.

The migrated page should look and behave as closely as possible to the legacy PTLIMS screen.

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
---

# Avoid Code Duplication

## Existing Screen First Rule

Before generating any page:

1. Review the legacy screen.
2. Review the current migrated screen.
3. Identify missing fields.
4. Identify missing actions.
5. Identify missing links.
6. Identify missing validation.
7. Identify missing navigation.

Prefer updating existing screens over generating replacement screens.

Do not generate new pages when an existing migrated page can be enhanced.
Minimise duplication across the entire solution.

Before creating any new file:

1. Search for an existing implementation.
2. Determine whether existing code can be reused.
3. Extend existing code where appropriate.
4. Prefer enhancement over duplication.

Do NOT create:

- Duplicate repositories
- Duplicate services
- Duplicate DTOs
- Duplicate ViewModels
- Duplicate validators
- Duplicate controllers
- Duplicate helper classes
- Duplicate API clients
- Duplicate mapping logic
- Duplicate stored procedure wrappers

---

## Reuse Existing Components

When implementing a feature review existing:

- DTOs
- ViewModels
- Services
- Repositories
- Validators
- API Clients
- Extension Methods
- Mapping Helpers
- Shared Components
- GOV.UK UI Components

Use existing components wherever practical.

---

## Shared Functionality

If functionality is required by multiple domains:

Create or reuse a shared implementation instead of duplicating logic.

Examples:

- Paging
- Filtering
- Search Models
- API Response Wrappers
- Validation Helpers
- Lookup Services
- Navigation Components
- Date Formatting
- Common View Models

---

## Refactoring Rule

If similar functionality already exists:

Prefer:

Refactor Existing Code
↓
Reuse Existing Code

Instead of:

Copy Existing Code
↓
Modify Copy

---

## Duplicate File Check

Before generating new files provide:

Existing File
↓
Reuse / Extend

or

Existing File
↓
Cannot Reuse
↓
Reason

for each proposed file.

---

## DTO Rule

Do not create multiple DTOs containing identical data.

Reuse existing DTOs where appropriate.

Create a new DTO only when:

- The shape is materially different.
- Existing DTOs cannot satisfy the requirement.
- The migration document explicitly requires it.

---

## ViewModel Rule

Do not create:

- ContractDetailsViewModel
- ContractEditViewModel
- ContractCreateViewModel

if an existing ContractViewModel can be reused or extended.

Prefer reuse.

---

## Repository Rule

One domain should normally have a single repository abstraction.

Avoid creating:

- ContractRepository
- ContractQueryRepository
- ContractReadRepository
- ContractWriteRepository

unless explicitly required by architecture or migration documents.

---

## Service Rule

Avoid creating multiple services performing overlapping responsibilities.

Prefer:

IContractService

instead of:

- IContractReadService
- IContractWriteService
- IContractManagementService

unless explicitly justified.

---

## Navigation Rule

Reuse the existing shared navigation components.

Do not generate domain-specific navigation implementations if a shared navigation structure already exists.

Extend existing navigation models and components where possible.

---

## Testing Rule

Reuse existing:

- Test Helpers
- Builders
- Fixtures
- Mock Factories
- Test Base Classes

Do not duplicate test infrastructure.

---

Goal:

The PTLIMS migration should:

✅ Maximise reuse

✅ Minimise duplication

✅ Remain maintainable

✅ Follow existing solution patterns

✅ Extend existing implementations where possible

❌ Do not generate duplicate code simply because it is faster.

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
### Reuse Assessment

For every proposed file identify:

- Existing file available? (Yes/No)
- Can existing file be reused? (Yes/No)
- Reason

Create a new file only if reuse is not possible.
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
- Dapper Configuration
- Connection Factory
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