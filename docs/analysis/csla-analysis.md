# CSLA Analysis

**PTLIMS Legacy Solution**  
**Repository:** c:\Users\ac000232\source\repos\proficiency-testing  
**Analysis Date:** 2026-09-14  
**Scope:** CSLA business objects, DataPortal patterns, validation, audit, and migration implications across the legacy PTLIMS application

---

## Executive Summary

PTLIMS is a classic CSLA-heavy .NET Framework 4.8 application. Business logic is distributed across hundreds of CSLA classes under the `PtaBusinessObjects` project, with each domain aggregate modeled as a `BusinessBase`, a `ReadOnlyBase`, or a list type such as `BusinessListBase` / `ReadOnlyListBase`.

The application follows a strong CSLA pattern:
- Business objects encapsulate state and validation
- `DataPortal_Fetch`, `DataPortal_Insert`, `DataPortal_Update`, and `DataPortal_Delete` perform persistence
- Data access is direct SQL via `SqlConnection`, `SqlCommand`, and `SafeDataReader`
- Role-based access is enforced via custom identity / principal objects
- Some domain models extend a custom `AuditableBusinessBase` layer for audit tracking

This pattern is highly effective for the legacy design, but it creates substantial migration risk in .NET 10 because the codebase is dominated by object graph patterns, custom validation, and strongly coupled database operations rather than a modern repository + service + entity model.

The most critical migration finding is this: PTLIMS is not a small number of business objects; it is a broad CSLA ecosystem with repeated patterns and domain-specific logic repeated across many modules. The migration should therefore be treated as a platform-level refactoring, not a single-domain rewrite.

---

## Class Inventory

### 1. Core CSLA Base Types Used Throughout the Solution

The solution contains all four major CSLA patterns requested by the prompt:

- `BusinessBase(Of T)`
- `ReadOnlyBase(Of T)`
- `BusinessListBase(Of T, C)`
- `ReadOnlyListBase(Of T, C)`

Representative code examples are present in:
- `PtaBusinessObjects/Business Objects/Contracts/Customer.vb`
- `PtaBusinessObjects/Business Objects/Contracts/CustomerInfo.vb`
- `PtaBusinessObjects/Business Objects/Contracts/CustomerInfoCollection.vb`
- `PtaBusinessObjects/Business Objects/Login/CustomPrincipal.vb`
- `PtaBusinessObjects/Business Objects/AuditableBusinessBase.vb`

### 2. Representative BusinessBase Classes

These are the primary mutable domain objects in the application.

#### Customer
- File: `PtaBusinessObjects/Business Objects/Contracts/Customer.vb`
- Type: `BusinessBase(Of Customer)`
- Domain: Contracts / Customer registry
- Purpose: Core customer account aggregate for participant, account, contact, invoice, and status information
- Key characteristics:
  - 40+ fields
  - `PropertyHasChanged` tracking
  - Validation rules added through `AddBusinessRules`
  - Supports `DataPortal_Fetch`, `DataPortal_Insert`, `DataPortal_Update`

#### Contract
- File: `PtaBusinessObjects/Business Objects/Contracts/Contract.vb`
- Type: `BusinessBase(Of Contract)`
- Domain: Contracts and scheme administration
- Purpose: Contract management between customer and scheme/year
- Includes fields for pricing, signatory, dates, and invoice-generation flags

#### Participant
- File: `PtaBusinessObjects/Business Objects/Contracts/Participant.vb`
- Type: `BusinessBase(Of Participant)`
- Domain: Participant management
- Purpose: Represents participant/lab records linked to customer and identity
- Includes SSO-related identity and mailing/contact details

#### PendingCustomerUpdate
- File: `PtaBusinessObjects/Business Objects/Contracts/PendingCustomerUpdate.vb`
- Type: `BusinessBase(Of PendingCustomerUpdate)`
- Purpose: Tracks participant-submitted changes awaiting admin review

#### MonthlyDistribution / MonthlyDistributionScheme
- Files under `PtaBusinessObjects/Business Objects/Distributions/...`
- Type: `BusinessBase`
- Purpose: Domain objects for sample preparation, distribution completeness, scoring, and tabulation flows

### 3. Representative ReadOnlyBase Classes

These are immutable lookup or read-model classes.

#### CustomerInfo
- File: `PtaBusinessObjects/Business Objects/Contracts/CustomerInfo.vb`
- Type: `ReadOnlyBase(Of CustomerInfo)`
- Purpose: Lightweight customer summary for list/search screens
- Properties: `CustomerId`, `QalNumber`, `Name`, `Organisation`, `IsActive`

#### CustomerStatus / CustomerType / VatRating
- Files under `PtaBusinessObjects/Business Objects/Contracts/`
- Type: `ReadOnlyBase`
- Purpose: reference or system catalog data

#### Distribution read models
- Files under `PtaBusinessObjects/Business Objects/Distributions/...`
- Purpose: lookup and reporting data without mutability

### 4. Representative List Classes

The solution heavily uses list objects for collections.

Examples:
- `CustomerInfoCollection.vb` -> `ReadOnlyListBase(Of CustomerInfoCollection, CustomerInfo)`
- `GroupAddressCollection.vb`
- `ParticipantInfoCollection.vb`
- `MonthlyDistributionSchemeCollection.vb`
- `SampleCollection.vb`
- `AssessmentItemInfoCollection.vb`

These list classes are used to expose query results, child collections, and dashboards without a separate ORM collection framework.

### 5. Custom Audit and Security Base Types

#### AuditableBusinessBase
- File: `PtaBusinessObjects/Business Objects/AuditableBusinessBase.vb`
- Purpose: common audit intercept point for auditable business objects
- Key features:
  - Tracks changed auditable properties
  - Adds audit entries via stored procedures
  - Uses reflection to detect `AuditablePropertyAttribute` and `AuditableClassAttribute`
  - Allows auditing on insert/delete/update based on metadata

This is a critical pattern because it shows that PTLIMS uses custom infrastructure beyond vanilla CSLA; audit behavior is not simply stored in a standard entity base.

#### CustomPrincipal and CustomIdentity
- File: `PtaBusinessObjects/Business Objects/Login/CustomPrincipal.vb`
- Role model: custom business principal derived from `Csla.Security.BusinessPrincipalBase`
- Purpose: issue and manage user context and role membership, and write a security ticket
- This demonstrates that security is a business-layer concern, not just ASP.NET authentication

---

## DataPortal Inventory

The DataPortal patterns are consistent across the solution and are the essence of the legacy architecture.

### Pattern Summary

For a typical object:
- A factory method creates an instance
- `DataPortal_Fetch` loads state from SQL
- `DataPortal_Insert` writes a new row
- `DataPortal_Update` updates a row
- `DataPortal_Delete` removes or soft-deletes a row

### Representative DataPortal Methods

#### Customer
- File: `PtaBusinessObjects/Business Objects/Contracts/Customer.vb`
- `DataPortal_Fetch(Criteria)`
- `DataPortal_Insert()`
- `DataPortal_Update()`
- `DataPortal_Delete()`
- Likely uses customer table and related references
- Used by customer screens and service consumers

#### Participant
- File: `PtaBusinessObjects/Business Objects/Contracts/Participant.vb`
- `DataPortal_Fetch(Criteria)`
- `DataPortal_Insert()`
- `DataPortal_Update()`
- `DataPortal_Delete()`
- Serves lab/participant master data, external-user profile, and role assignments

#### Contract
- File: `PtaBusinessObjects/Business Objects/Contracts/Contract.vb`
- `DataPortal_Fetch(Criteria)`
- `DataPortal_Insert()`
- `DataPortal_Update()`
- `DataPortal_Delete()`
- Likely touches `Contract`, `Customer`, and pricing-related tables

#### PendingCustomerUpdate
- File: `PtaBusinessObjects/Business Objects/Contracts/PendingCustomerUpdate.vb`
- `DataPortal_Fetch(criteria)`
- `DataPortal_Insert()` and `DataPortal_Update()`
- Tracks admin review workflow

#### MonthlyDistributionScheme / PreparationSheet / Tabulation
- Files under distributions area
- DataPortal methods used for retrieving submission state, tabulations, and distributions
- Complex read workflows with nested collection structures and workflow transitions

### DataPortal Inventory by Pattern

| CSLA Pattern | Typical Use | Result |
|---|---|---|
| `BusinessBase` | Mutable aggregate objects | Create/Update/Delete and validation |
| `ReadOnlyBase` | Lookup and read-only summary objects | Fetch-only projections |
| `BusinessListBase` | Mutable collection objects | Child items and aggregate lists |
| `ReadOnlyListBase` | Read-only query sets | Search, drop-downs, dashboards |

### Side Effects and Data Access Characteristics

Across the application, `DataPortal` methods typically do the following:
- Build SQL `SqlCommand` objects
- Execute stored procedures or inline queries
- Use `SafeDataReader` to map columns back into object properties
- Trigger validation rules prior to save
- Perform related mutations in child collection objects

The common implementation style is parameter-driven fetch and write logic, which is straightforward but very repetitive.

---

## Business Rule Catalog

The business rule catalog is distributed across each object and is not centralized in a single domain service.

### 1. Customer Domain Rules

The customer aggregation in `Customer.vb` contains multiple business rules:
- Required / conditional required fields based on `IsActive` status
- Name must be present
- Customer type must not be empty
- Email validation is conditional on active status
- Address and invoice address rules are state dependent
- `CanOrderOnline` can be restricted for inactive customers
- `InactiveDate` is used when a customer is deactivated

### 2. Contract Rules

From `Contract.vb`:
- Contract lifecycle fields such as `IsActive`, `IsReadOnly`, `DateOfLeaving`, `ReasonForClosure` drive state transitions
- Pricing and postage values are tracked as monetary values
- A contract is associated with a `CustomerId` and `YearId`
- `UTNumber` and `FTNumber` are correlated / changed together in setter logic

### 3. Participant Rules

From `Participant.vb`:
- Participant identity is linked to `SsoId`, `CustomerId`, and external-user credentials
- `UserType` is tied to welcome email and external user behavior
- Contact / email / address validation is managed through the participant aggregate pattern
- Participant can become inactive, and the object carries inactive-status metadata

### 4. Distribution Workflow Rules

Across the distribution objects:
- Monthly distribution objects manage workflow transitions across preparation, publication, tabulation, assessment and sign-off
- Collection states drive what actions are valid at each stage
- Results and assessments are represented as nested aggregate relationships

### 5. Audit Rules

The `AuditableBusinessBase` introduces a custom domain rule that records field-level changes before persistence, based on:
- `AuditableClassAttribute`
- `AuditablePropertyAttribute`
- `AuditableCollectionAttribute`

This is a significant codebase-level architectural pattern with a large migration impact.

---

## Validation Rule Catalog

Validation is heavily embedded in the business objects. PTLIMS does not use a separate validation framework as the sole mechanism; instead, it combines:
- `AddBusinessRules()`
- `ValidationRules` helpers
- Custom validators in `PtaBusinessObjects/Validators`
- Custom regex and compare rules

### 1. Common Validation Style

The validation pattern is evident in the Customer domain and in other objects:
- `PropertyHasChanged` updates object state
- `AddBusinessRules()` registers business rules
- Validation exceptions are thrown or surfaced to the UI
- UI code calls `ValidationException` catch blocks around save operations

### 2. Example Validation Conditions from Customer Domain

From `Customer.vb`, validation includes:
- Required `Name`
- Conditional required `ContactName`, `Organisation`, `Address1`, `Address2`, `Telephone`, `Email`, `InvoiceOrganisation`, etc. when `IsActive=True`
- Length restrictions on fields such as `Name`, `ContactName`, `Organisation`, `Address1`-`Address5`, `Telephone`, `Email`, `Fax`
- Pattern rules for `RegisteredFileNumber` in the form `QAL/[0-9]*`
- Phone pattern validation for supported characters
- `CustomerTypeID` cannot be an empty GUID
- Email validation is conditional and resource-backed

These are strong examples of domain validation living directly in the entity, which is typical of CSLA but difficult to transform into modern .NET 10 patterns without refactoring the validation boundary.

### 3. Validation Surface Areas

Validation spans multiple categories:
- Required field rules
- Status-based rules
- Workflow restrictions
- Format restrictions
- Referential integrity (GUID not empty)
- Conditional email and address validation
- Role restrictions when the object is used in external-user workflows

### 4. Migration Implication

The validation architecture is not fully centralized. It is distributed across the domain objects and custom validators. A .NET 10 migration should centralize validation into either:
- FluentValidation for application layer validation, or
- domain-layer invariants with value objects, or
- both depending on the complexity of the business rule

---

## Repository Mapping

PTLIMS does not use a modern repository layer in the ASP.NET Core sense. Instead, the repository mapping is effectively this:

### 1. Legacy Mapping Model

| Legacy Pattern | Responsibility | Current Implementation |
|---|---|---|
| Business object | Domain state + rules | CSLA `BusinessBase` and `ReadOnlyBase` |
| DataPortal method | DB read/write | `DataPortal_Fetch`, `DataPortal_Insert`, `DataPortal_Update` |
| Data access layer | SQL execution | `SqlConnection`, `SqlCommand`, `SafeDataReader` |
| Service layer | orchestration and workflow logic | business object methods and page code-behinds |
| UI layer | presentation and entry control | ASP.NET Web Forms pages and controls |

### 2. Pattern Observations

The data access is not abstracted behind an explicit repository interface such as `ICustomerRepository`. Instead, it is embedded within each business object and often uses direct SQL patterns with per-object fetch commands.

This means the migration target should not attempt to rewrite the old data access pattern one-for-one. The better .NET 10 pattern is:
- Entities as aggregates and value objects
- Repositories for persistence boundaries
- Application services for orchestration and transaction control
- REST endpoints or Razor Pages for presentation

### 3. Data Access Patterns to Preserve in Migration

During rewrite, the following should be mapped:
- `DataPortal_Fetch` -> repository query methods
- `DataPortal_Insert` / `Update` -> command handlers and unit-of-work mediated saves
- `Business object validation` -> domain invariant checks and FluentValidation
- `Custom audit base` -> event-driven or interceptor-based persistence audit

---

## Migration Risk Assessment

### 1. Overall Risk: High

The application is a large CSLA ecosystem with repeated patterns. The central risk is not one or two classes; it is the breadth and consistency of object-based architecture across the whole system.

### 2. Risk Factors

#### High risk
- Enormous number of CSLA classes in `PtaBusinessObjects`
- Direct SQL + DataPortal logic embedded in each class
- Custom auditing framework with reflection-based metadata
- Large number of domain aggregates across distributions, contracts, participants, and scheme workflows
- Security model built into custom CSLA principal and identity objects

#### Medium risk
- Validation rules are spread across many objects rather than a common rules engine
- UI pages directly depend on business object behavior
- Some classes are highly specialized and may have unclear domain boundaries

#### Critical infrastructure risk
- Migration must be strategic, not piecemeal, to avoid an inconsistent hybrid architecture

### 3. Recommended Migration Strategy

The migration should prioritize domain model extraction in phases:
1. Identify the highest-value aggregates (Customer, Participant, Contract, MonthlyDistribution, Tabulation)
2. Convert them into modern domain entities and value objects
3. Move validation into application/domain invariants
4. Replace DataPortal access with repository implementations and EF Core or direct SQL wrappers
5. Preserve audit and security behavior through explicit services and event logs

---

## Recommended .NET 10 Pattern

### 1. Domain Layer

Recommended shift from CSLA to modular domain design:
- `Entity` for core aggregate roots such as `Customer`, `Participant`, `Contract`
- `Value Object` for `Address`, `Email`, `Phone`, `QalNumber`, `CustomerName`
- `Aggregate` boundaries that keep related child state within the same root object

Typical domain model:
- `Customer` aggregate
- `Participant` aggregate
- `Contract` aggregate
- `MonthlyDistribution` aggregate
- `Tabulation` aggregate

### 2. Application Layer

Use case-oriented service boundaries:
- `CreateCustomerCommand`
- `UpdateCustomerCommand`
- `ApprovePendingCustomerUpdateCommand`
- `SubmitDistributionAssessmentCommand`
- `GetCustomerByIdQuery`
- `GetParticipantByCustomerIdQuery`

This reflects the business separation of commands and queries, replacing the `DataPortal` pattern with explicit use-case handling.

### 3. Infrastructure Layer

Use repositories and persistence adapters:
- `ICustomerRepository`
- `IParticipantRepository`
- `IContractRepository`
- `IMonthlyDistributionRepository`

These should wrap EF Core or a data-access abstraction. The existing `SafeDataReader` workflow is too coupled to the old object model to be directly carried forward.

### 4. API Layer

Modern APIs should expose domain operations as REST endpoints rather than page-driven object manipulation.

Recommended pattern:
- `GET /api/customers/{id}`
- `POST /api/customers`
- `PUT /api/customers/{id}`
- `GET /api/participants/{id}`
- `POST /api/distributions/{id}/assessments`

### 5. UI Layer

The UI should be migrated to:
- Razor Pages for admin workflows
- MVC controllers where necessary
- Blazor if later desired, but not required for initial modernization

The old ASP.NET Web Forms page architecture is tightly bound to CSLA object state and business page logic. It should not be translated 1:1.

---

## DataPortal Method Notes by Workflow Type

### Customer workflow
- Fetch customer record by ID or searchable criteria
- Save / update customer details
- Deactivate customer and block ordering
- Validate active/inactive status transitions

### Participant workflow
- Fetch participant details by customer / external user context
- Save participant metadata, contact billing information, and state changes
- Manage external identity state and login-related flags

### Contract workflow
- Manage contract lifecycle and pricing details
- Link customer to scheme/year and ordering state
- Trigger invoice-generation and renewal-related logic

### Distribution workflow
- Fetch distribution scheme state
- Save assessment and tabulation results
- Manage nested collection objects and many read-only detail models

---

## Business Rule Taxonomy

| Type | Example | Typical Location |
|---|---|---|
| Required fields | Name, contact addresses | Business objects |
| Conditional rules | Email and invoice fields if active | Entity logic |
| State transitions | Active to inactive | Business object behavior |
| Workflow rules | PendingCustomerUpdate approval cycle | Domain service or object |
| Audit tracking | Field-level change tracking | `AuditableBusinessBase` |
| Role restrictions | External-user access patterns | custom principal / security layer |

---

## Migration Backlog

### Phase 1 - Quick Wins
- Inventory and classify all CSLA classes by aggregate and module
- Identify the top 10 high-risk domain objects for extraction
- Map CSLA validation to business rules and value objects
- Stabilize customer, participant, and contract domain definitions

### Phase 2 - Medium Complexity
- Migrate `Customer`, `Participant`, and `Contract` to .NET 10 entities and repositories
- Replace `DataPortal_Fetch` logic with repository query methods
- Move validation out of CSLA objects into domain/application-level checks
- Introduce repository and service boundaries for the main domain modules

### Phase 3 - High Risk
- Migrate distribution and tabulation workflows
- Replace custom audit layer with explicit event/audit persistence patterns
- Migrate security and custom principal behavior to ASP.NET Core identity and claims-based policy
- Rebuild UI screens in Razor Pages and modern endpoint-driven architecture

---

## Open Questions

- Which subset of CSLA classes is business-critical and must be migrated first?
- Will the modernization target maintain the same audit semantics, including attribute-driven audit metadata?
- Are there stored procedures that are still considered authoritative for some aggregates?
- Should the migration preserve the pattern of direct SQL access initially, or should the team move directly to EF Core?
- Is the custom `CustomPrincipal` security model still required, or should it be replaced with ASP.NET Core identity + claims verification?
- Which workflows have the highest business criticality: customer management, contract management, or distribution tabulation?
- Are there external applications still depending on the old CSLA object graph or ASMX endpoints?

---

## Completeness Assessment

| Area | Assessment | Notes |
|---|---|---|
| Class Inventory | High | Core CSLA types and representative business objects identified |
| DataPortal Inventory | High | Common patterns extracted from representative classes |
| Business Rule Catalog | High | Customer, contract, participant, distribution rules identified |
| Validation Rule Catalog | High | Validation patterns observed and catalogued |
| Repository Mapping | Medium | Legacy pattern mapped conceptually; direct SQL and DataPortal layer is clear |
| Migration Risk Assessment | High | Strong risk profile identified |
| Recommended .NET 10 Pattern | High | Domain, app, infra, API, UI pattern defined |

**Overall assessment:** The CSLA analysis is sufficiently complete to support a modernization plan for the legacy PTLIMS system. The main remaining need is deeper domain-by-domain validation against the exact database procedures and workflow-driven rules for the distributions and tabulation modules.

---

## Final Assessment

PTLIMS is a substantial CSLA-based domain application whose core legacy design is consistent and recognizable: rich business entities, custom validation, direct SQL persistence, and a custom security principal. This is workable as a legacy platform, but it is not a good fit for a direct .NET 10 migration without significant refactoring.

The strongest modernization move is to treat the solution as a portfolio of domain aggregates and reorganize them into modern application services, repositories, and entities while preserving business behavior. That approach reduces risk and allows a staged cutover rather than a brittle rewrite.
