# Customer Domain Migration (To-Be)

**Domain:** Customer  
**Target solution alignment:** `PTL.Api`, `PTL.ApiClient`, `PTL.InternalWeb`, `PTL.ExternalWeb`  
**Scope:** future-state migration of the legacy PTLIMS Customer domain to the .NET 10 solution structure defined by the HLD.

---

## 1. Current State Summary

The legacy Customer domain is implemented as a Web Forms + CSLA + ASMX pattern with the following operating model:

- Internal customer administration is handled by `ProficiencyTestingWeb/Contracts Admin/Customer.aspx` and `CustomerList.aspx`.
- External participant self-service updates are handled by `ProficiencyTestingExternalWeb/EditCustomerDetails.aspx` and a pending-change approval workflow.
- Customer data is exposed through `Customer.asmx` and `PendingCustomerUpdate.asmx`.
- Core persistence is the CSLA `Customer` business object backed by SQL stored procedures and `tblCustomer`.
- This is a high-throughput domain with direct dependencies on participants, contracts, customer status, and outbound identity checks through `UserService`.

The to-be state keeps the same business behaviour, but moves it into the PTL solution structure and the HLD identity model.

---

## 2. UI Migration Mapping

### Internal Web mapping

| Legacy Page | Target UI | Notes |
|---|---|---|
| `Contracts Admin/CustomerList.aspx` | `PTL.InternalWeb/Features/Customer/Views/Index.cshtml` | Search/filter customer list by active/inactive/all status |
| `Contracts Admin/Customer.aspx` | `PTL.InternalWeb/Features/Customer/Views/Details.cshtml` and `Edit.cshtml` | Create, edit, save, deactivate, and maintain invoice/customer metadata |
| `Contracts Admin/PendingCustomerUpdateDetails.aspx` | `PTL.InternalWeb/Features/Customer/Views/PendingUpdateDetails.cshtml` | Review external-proposed changes |
| `Contracts Admin/ReviewPendingCustomerUpdates.aspx` | `PTL.InternalWeb/Features/Customer/Views/PendingUpdates.cshtml` | Decision list for approve/reject flow |

### External Web mapping

| Legacy Page | Target UI | Notes |
|---|---|---|
| `EditCustomerDetails.aspx` | `PTL.ExternalWeb/Features/Customer/Views/Edit.cshtml` | Participant updates own customer details through pending change submission |

### Internal Web controller mapping

- `CustomerController` in `PTL.InternalWeb/Features/Customer`
- Actions:
  - `Index` — list customers
  - `Details` — view customer
  - `Edit` / `Save` — create/update customer
  - `PendingUpdates` — admin review queue
  - `PendingUpdateDetails` — review a single submission

### External Web controller mapping

- `CustomerController` in `PTL.ExternalWeb/Features/Customer`
- Actions:
  - `Edit` — display current customer and pending update form
  - `Submit` — save pending update request
  - `Status` — show approval status

---

## 3. API Migration Mapping

| Legacy ASMX Service | Legacy Method | Target REST Endpoint | Target Controller |
|---|---|---|---|
| `Customer.asmx` | `GetCustomer(tokenId, customerId)` | `GET /api/customers/{customerId}` | `PTL.Api/Controllers/CustomerController` |
| `Customer.asmx` | list/search patterns | `GET /api/customers?status={status}` | `CustomerController` |
| `PendingCustomerUpdate.asmx` | `GetPendingCustomerUpdate(tokenId, customerId, isSubmitted)` | `GET /api/customers/{customerId}/pending-updates` | `CustomerController` |
| `PendingCustomerUpdate.asmx` | `InsertPendingCustomerUpdate(...)` | `POST /api/customers/{customerId}/pending-updates` | `CustomerController` |
| `PendingCustomerUpdate.asmx` | `UpdatePendingCustomerUpdate(...)` | `PUT /api/customers/{customerId}/pending-updates/{pendingUpdateId}` | `CustomerController` |

### API design notes

- `PTL.Api` owns the customer domain API surface.
- `PTL.ApiClient` exposes one typed client for each domain capability, for example `ICustomerApiClient`.
- Public API contracts remain thin DTOs, but do not expose CSLA or legacy SQL-specific structures.
- The endpoint model maps to the existing customer fields rather than inventing a new domain model.

---

## 4. Authentication Mapping

### Current state

- Internal web: legacy custom identity and Forms auth / Windows-based access patterns.
- External web: participant access tied to VLA / `UserService` and custom permission evaluation.
- API: token-based service calls with user service checks.

### Target state

| Legacy Concern | To-Be Mapping |
|---|---|
| Windows Auth / Forms Auth | Entra ID SAML for internal users |
| VLA / `UserService` identity validation | Gov UK One Login OIDC for external users |
| Service token identity | JWT bearer claims for PTL.Api |
| Role checks in code-behind and ASMX | Policy-based authorization and claims mapping |

### Customer domain claims

The customer domain should resolve the following to claims:

- `sub` or user identifier
- `customerId`
- `qalNumber`
- `role` or group claim (customer, participant, scheme admin, internal user, admin)
- optional `participantId`

### Authorization rules

- Internal admin pages require `Admin` or `SchemeAdmin` policy.
- External customer edit pages require `Participant` policy plus ownership check on customer id.
- Pending update review pages require `Admin` or `CustomerManager` policy.
- API endpoints validate JWT claims and require policy-based access.

---

## 5. Repository Mapping

### Repository structure

`PTL.Api` should include a simple repository layer for the Customer domain only, without introducing CQRS or a broader domain abstraction beyond the HLD need.

#### `CustomerRepository`

Methods:

- `GetCustomerByIdAsync(Guid customerId, CancellationToken)`
- `GetCustomersAsync(CustomerStatusFilter status, CancellationToken)`
- `InsertCustomerAsync(CustomerWriteRequest, CancellationToken)`
- `UpdateCustomerAsync(CustomerWriteRequest, CancellationToken)`
- `DeactivateCustomerAsync(Guid customerId, CancellationToken)`

Stored procedures used initially:

- `spgCustomerByCustomerId`
- `spgaCustomerInfo`
- `spiCustomer`
- `spuCustomer`

#### `PendingCustomerUpdateRepository`

Methods:

- `GetPendingCustomerUpdatesAsync(Guid customerId, bool? isSubmitted, CancellationToken)`
- `GetPendingCustomerUpdateByIdAsync(Guid customerId, Guid pendingUpdateId, CancellationToken)`
- `InsertPendingCustomerUpdateAsync(PendingCustomerUpdateWriteRequest, CancellationToken)`
- `UpdatePendingCustomerUpdateAsync(PendingCustomerUpdateWriteRequest, CancellationToken)`

Stored procedures used initially:

- `spgPendingCustomerDetailsEditByCustomerID`
- `spiPendingCustomerDetailsEdit`
- `spuPendingCustomerDetailsEditByCustomerID`
- `spdPendingCustomerDetailsEditByCustomerID`

### Repository design rule

Use stored procedures initially unless an explicit reason exists to replace them. The default choice is a thin Dapper repository wrapping the current SQL procedures rather than introducing Entity Framework or a new persistence model.

---

## 6. Database Strategy

### Recommendation

- Keep stored procedures for the Customer domain initially.
- Add thin repository wrappers over existing procedures.
- Use Dapper for query / command mapping to DTOs.
- Do not replace the stored procedures in the first migration wave unless a specific business gap is identified.

### Rationale

- The legacy database model is already working and mapped to business objects.
- The customer domain is data-heavy and field-rich; using existing SPs reduces conversion risk.
- The HLD does not require new database patterns such as CQRS or a separate write model.

### Strategy by object

| Domain object | Strategy |
|---|---|
| Customer master record | Keep `tblCustomer`; use `spgCustomerByCustomerId`, `spiCustomer`, `spuCustomer` |
| Customer summary list | Keep `spgaCustomerInfo`; use Dapper mapping |
| Pending customer updates | Keep existing pending-update stored procedures |
| Lookup/enum data | Keep `tblCustomerType`, `tblCustomerStatus`, and related lookup tables |

### Only replace if needed

Stored procedures should be replaced only if a business rule or operational constraint requires it, for example:

- performance issue in a specific query
- schema drift causing unsupported behaviour
- a requirement to split a monolithic procedure into smaller domain-specific queries

Without a strong reason, the default remains: wrap and preserve.

---

## 7. API Client Design

The typed client will live in `PTL.ApiClient` and be shared by `PTL.InternalWeb` and `PTL.ExternalWeb`.

### Interface contract

- `ICustomerApiClient`
- Methods:
  - `GetCustomerAsync(Guid customerId, CancellationToken)`
  - `GetCustomersAsync(CustomerStatusFilter status, CancellationToken)`
  - `CreateCustomerAsync(CreateCustomerRequest, CancellationToken)`
  - `UpdateCustomerAsync(Guid customerId, UpdateCustomerRequest, CancellationToken)`
  - `GetPendingUpdatesAsync(Guid customerId, CancellationToken)`
  - `SubmitPendingUpdateAsync(Guid customerId, SubmitPendingUpdateRequest, CancellationToken)`

### DTO contract

Examples:

- `CustomerDto`
- `CustomerSummaryDto`
- `CustomerStatusDto`
- `PendingCustomerUpdateDto`
- `CreateCustomerRequest`
- `UpdateCustomerRequest`
- `SubmitPendingUpdateRequest`

This keeps each web front end detached from the legacy ASMX payload layout while preserving the same business fields.

---

## 8. Target PTL.Api Structure

Example structure:

```text
src/PTL.Api/
  Controllers/
    CustomerController.cs
  Models/
    Customer/
      CustomerDto.cs
      CustomerSummaryDto.cs
      CreateCustomerRequest.cs
      UpdateCustomerRequest.cs
      PendingCustomerUpdateDto.cs
      SubmitPendingUpdateRequest.cs
  Services/
    CustomerService.cs
  Data/
    CustomerRepository.cs
    PendingCustomerUpdateRepository.cs
  Extensions/
    ServiceCollectionExtensions.cs
```

### Responsibilities

- `CustomerController` exposes the HTTP contract.
- `CustomerService` contains the application logic and orchestration.
- `CustomerRepository` wraps the stored procedures and SQL access.
- DTOs stay shaped to the public contract only.

This remains aligned to the HLD and avoids introducing an unnecessary abstraction layer beyond the domain service and repository boundaries.

---

## 9. Target PTL.InternalWeb Feature Structure

Example structure:

```text
src/PTL.InternalWeb/
  Features/
    Customer/
      CustomerController.cs
      CustomerViewModel.cs
      CustomerSearchViewModel.cs
      Views/
        Index.cshtml
        Details.cshtml
        Edit.cshtml
        PendingUpdates.cshtml
        PendingUpdateDetails.cshtml
```

### Internal Web responsibilities

- Admin search and validation screens
- Create/edit customer details
- View and approve pending updates
- Uses `PTL.ApiClient` to call `PTL.Api`
- Uses Entra ID SAML identity and claims-based authorization

---

## 10. Target PTL.ExternalWeb Feature Structure

Example structure:

```text
src/PTL.ExternalWeb/
  Features/
    Customer/
      CustomerController.cs
      CustomerViewModel.cs
      Views/
        Edit.cshtml
        Submitted.cshtml
        Status.cshtml
```

### External Web responsibilities

- Participant access to their own customer records
- Read current customer details
- Submit pending updates
- Show update status and response handling
- Uses Gov UK One Login OIDC identity and policy-based authorization

---

## 11. Test Strategy

### API tests

- `PTL.Api.Tests`
- Validate API contracts and service behaviour
- Test customer fetch, list, create, update, deactivate, and pending update routes
- Ensure SQL mapping respects current business fields and stored procedure behaviour

### UI tests

- `PTL.InternalWeb.Tests`
- Validate list/filter screens and admin review flow
- Verify role-based authorization and claim-driven access

- `PTL.ExternalWeb.Tests`
- Validate participant dashboard and update submission journey
- Verify status messages and validation flows

### Minimum tests for customer domain

- Search returns active/inactive/all filtered results.
- Create customer persists fields and validates required data.
- Update customer preserves invoice and contact data.
- Deactivate customer cascades to active participants.
- Pending update submission is visible to admin.
- Admin approval or rejection updates the customer record or rejects the change correctly.

---

## 12. Sprint Breakdown

### Sprint 1 — foundation and contracts

- Confirm HLD identity model and claims mapping
- Add typed API client contract for Customer domain
- Create `PTL.Api` customer endpoints skeleton
- Validate repository layer against existing SQL procedures

### Sprint 2 — read and search flows

- Implement customer list and fetch APIs
- Build `PTL.InternalWeb` customer list page
- Build `PTL.ExternalWeb` customer read/edit page shell
- Add API and controller tests

### Sprint 3 — write and pending update workflows

- Implement create/update/deactivate API and service layers
- Implement pending update API routes
- Build admin review screens and external submission screens
- Add validation and status messages

### Sprint 4 — security and integration

- Enforce Entra ID / One Login claims policy mapping
- Validate customer ownership and admin access rules
- Run end-to-end regression against current workflow behaviour

### Sprint 5 — hardening and sign-off

- Regression and UAT test pass
- SQL procedure freeze / compatibility verification
- readiness checklist sign-off

---

## 13. Risks

### Security alignment risk

The legacy customer workflow contains identity assumptions baked into the custom auth and VLA service pattern. Risks center on mismatched claims, ownership checks, or user identity resolution when moving to Entra ID / One Login.

### Data contract drift

The legacy `Customer` object is wide and field-rich. Risk exists if the DTOs omit required fields or if invoice fields are not mapped consistently.

### Stored procedure dependency risk

Keeping SPs initially is low-risk but must be carefully validated for parameter names, null behaviour, and active/inactive handling.

### Workflow risk during deactivation

Customer deactivation triggers participant deactivation and viewer cleanup. This must be preserved exactly in the new workflow.

---

## 14. Dependencies

### Functional dependencies

- Authentication providers: Entra ID SAML for internal, Gov UK One Login OIDC for external
- API authorization policies
- SQL access to `tblCustomer` and related tables
- Existing stored procedures for customer and pending updates
- Participant and contract data for deactivation and ownership checks

### Technical dependencies

- `PTL.Api` web host ready for customer controllers
- `PTL.ApiClient` typed client registration
- `PTL.InternalWeb` and `PTL.ExternalWeb` configured to use the API client
- Shared configuration properties for API base URLs and identity metadata

---

## 15. Development Readiness

### Ready to start?

Yes, for the Customer domain the project is ready to begin implementation, provided the following are still confirmed:

1. HLD identity mapping is confirmed for internal/external users.
2. Customer ownership rules are confirmed per product behaviour.
3. The active/inactive and pending-update workflows are fully confirmed against the legacy implementation.
4. `PTL.Api`, `PTL.ApiClient`, `PTL.InternalWeb`, and `PTL.ExternalWeb` are available in the working branch and the typed-client pattern remains in place.

### Missing items that would block delivery

- authoritative current production values for customer and pending-update SQL procedures
- final signed-off claim mapping for customer admin and participant roles
- acceptance criteria for the pending update approval flow
- confirmation whether all customer fields are required in the first release or only a subset is needed for MVP

---

## 16. Final Recommendation

The migration should use the existing PTL solution structure and preserve the current PTLIMS Customer domain semantics with a minimal, HLD-aligned design:

- `PTL.Api` owns the customer API and uses Dapper and stored procedures initially.
- `PTL.ApiClient` exposes typed customer endpoints.
- `PTL.InternalWeb` hosts customer admin and approval flows.
- `PTL.ExternalWeb` hosts participant self-service updates.
- Security is claims-driven and policy-based, aligned to Entra ID / One Login and JWT bearer access for the API.

This approach keeps the migration grounded in the current system while remaining compatible with the HLD and the existing .NET 10 solution layout.
