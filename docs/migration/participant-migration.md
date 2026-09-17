# Participant Domain Migration Plan

**Domain:** Participant  
**Scope:** TO-BE migration plan for the legacy Participant domain in PTLIMS  
**Analysis basis:** docs/analysis/participant-analysis.md, docs/analysis/customer-analysis.md, docs/analysis/csla-analysis.md, docs/migration/api-migration.md, docs/source/PTLIMS-HLD-v0.3.docx  
**Status:** Design only; no implementation code produced.

---

## Current State Summary

The legacy Participant domain is a multi-layer business aggregate spanning administrative Web Forms pages, SOAP ASMX services, CSLA business objects, and SQL Server stored procedures. The domain is not just a person record; it is the identity and participation anchor for a customer organisation, a scheme contract, and the external user profile used in the PTLIMS participant portal.

The current AS-IS shape is:

- Participant is a CSLA `BusinessBase` with live state in `tblParticipant`.
- Participant identity is bound to `CustomerId` and `SsoId`.
- Scheme involvement is represented by `tlnkParticipantScheme` rather than a direct participant-to-scheme property on the participant aggregate.
- Profile edit flows use pending-update records, so participant changes are often submitted and approved rather than written directly to the live record.
- Viewer access and distribution participation are additional participant relationships that affect business visibility and downstream operational workflows.
- The participant domain depends heavily on external identity services and token-based access in the ASMX layer.

In practice, the Participant domain is a shared access and relationship hub between:

- Customer ownership
- Contract and scheme membership
- External participant authentication
- Distribution participation
- Viewer access and reporting

This makes the Participant domain both central and sensitive: a change in its structure or identity mapping can ripple across several upstream and downstream domains.

---

## Domain Boundaries

### What belongs to this domain?

The following should be treated as part of the Participant domain:

- Participant master record and lab identity
- Customer-to-participant ownership model
- External participant login and SSO linkage
- Participant contact and address data
- Participant active/inactive status
- Participant viewer associations
- Participant-scheme selection and distribution-month configuration
- Pending participant updates and approval workflow
- Participant profile retrieval for external portal users
- Participant list and search operations for admin screens

### What does not belong to this domain?

The following belong to adjacent domains and should not be mixed into the Participant domain model:

- Customer master configuration and customer account lifecycle (Customer domain)
- Contract definition and pricing contracts (Contract domain)
- Scheme catalogue definitions and year-based scheme metadata (Scheme domain)
- Distribution execution and result tabulation logic (Distribution / Results domain)
- Audit and reporting infrastructure (cross-cutting services)
- Identity provider integration and token issuance (Authentication domain)
- Global lookup values such as country or post code catalog data (reference data domain)

### Boundary statement

The Participant domain should own the participant identity and participation state, but it should not own the definition of scheme rules, distribution execution, or the authentication provider itself.

---

## UI Migration Mapping

### Legacy Page → Target Page

| Legacy Page | Legacy Role | Target Page / Surface | Target Placement | Notes |
|---|---|---|---|---|
| Contracts Admin / ParticipantList.aspx | Admin | Participant list screen | PTL.InternalWeb | List, active/inactive filter, login management |
| Contracts Admin / Participant.aspx | Admin | Participant details screen | PTL.InternalWeb | Create, edit, save participant |
| Contracts Admin / ParticipantScheme.aspx | Admin | Participant scheme management | PTL.InternalWeb | Scheme choices, month-level selections |
| Contracts Admin / ParticipantViewers.aspx | Admin | Viewer management | PTL.InternalWeb | Assign/remove participant viewer access |
| ExternalWeb / EditParticipantDetails.aspx | External participant | My profile page | PTL.ExternalWeb | Read and submit pending updates |
| ExternalWeb / pending participant updates screen | External participant | Proposed profile updates | PTL.ExternalWeb | Review and submit change proposals |

### UI migration considerations

- Keep a clear distinction between internal administration pages and external self-service pages.
- Internal screens should focus on admin-managed registry data and scheme membership.
- External self-service screens should use current authenticated participant identity and should not allow live mutation of the canonical participant record without an approval workflow.
- The legacy pattern of “pending update” should be preserved in the first migration wave to reduce business risk and align with the existing approval model.

---

## API Migration Mapping

### Legacy ASMX → REST Endpoint

| Legacy ASMX Service | Legacy WebMethod | Target REST Endpoint | Target Project |
|---|---|---|---|
| Participant.asmx | GetParticipant(tokenId) | GET /api/participants/me | PTL.Api |
| ParticipantCollection.asmx | FetchParticipantCollection(tokenId, customerId) | GET /api/customers/{customerId}/participants | PTL.Api |
| PendingParticipantUpdate.asmx | GetPendingParticipantUpdate | GET /api/participants/{participantId}/pending-updates | PTL.Api |
| PendingParticipantUpdate.asmx | InsertPendingParticipantUpdate | POST /api/participants/{participantId}/pending-updates | PTL.Api |
| PendingParticipantUpdate.asmx | UpdatePendingParticipantUpdate | PUT /api/participants/{participantId}/pending-updates/{id} | PTL.Api |
| PendingParticipantScheme.asmx | GetPendingParticipantScheme | GET /api/participants/{participantId}/scheme-choices | PTL.Api |
| PendingParticipantScheme.asmx | InsertPendingParticipantScheme | POST /api/participants/{participantId}/scheme-choices | PTL.Api |
| PendingParticipantScheme.asmx | UpdatePendingParticipantScheme | PUT /api/participants/{participantId}/scheme-choices/{id} | PTL.Api |
| PendingParticipantScheme.asmx | DeletePendingParticipantScheme | DELETE /api/participants/{participantId}/scheme-choices/{id} | PTL.Api |
| Viewer-related participant service flow | Viewer assignment operations | GET/PUT /api/participants/{participantId}/viewers | PTL.Api |

### API design direction

The REST surface should be organised around the participant as the primary resource, with secondary resources for:

- profile
- scheme choices
- viewer access
- pending updates

The first REST pass should not expose raw internal CSLA objects. Instead, it should use domain-oriented DTOs representing the participant aggregate and its child collections.

---

## Repository Mapping

### Repository Name: ParticipantRepository

**Purpose:** read and write participant aggregate data including master participant state, scheme membership, and pending change records.

**Methods:**

- GetById(participantId)
- GetByCustomerId(customerId)
- GetBySsoId(ssoId)
- GetByTokenUser(userId)
- ListForCustomer(customerId, includeInactive)
- SaveParticipant(participant)
- SaveSchemeMemberships(participantId, schemeSelections)
- SavePendingUpdate(participantId, pendingUpdate)
- RemoveViewerAccess(participantId)

**Stored Procedures:**

- spgParticipantByCustomerId
- spgParticipantBySsoId
- spgParticipantInfoByCustomerId
- participant scheme and pending update procedures used by the legacy ParticipantScheme and PendingParticipantUpdate flows

**Database Dependencies:**

- tblParticipant
- tlnkParticipantScheme
- tlnkViewerParticipant
- related customer and distribution tables indirectly referenced by participant workflows

### Repository design guidance

- Keep the repository focused on persistence and query translation.
- Treat participant-scheme membership as a child collection rather than flattening it into the participant root object.
- Do not hide pending-update logic in the repository alone; it should sit behind a participant workflow service layer where approval rules and validation are enforced.

---

## Authentication Mapping

### Current

The legacy system resolves user identity through token-based authentication, using the current user and then loading the corresponding participant record by SSO identity. The ASMX layer repeatedly calls user-service resolution before reading or writing participant data.

In the current model:

- user identity is resolved through a token or user service lookup
- the participant is mapped from that identity to `SsoId`
- some endpoint-level authorisation is present, but in several ASMX services the role checks are commented out or inconsistent

### Future

The target migration should map to ASP.NET Core identity and policy-based authorization, with participant-specific claims and policies.

**Recommended mapping:**

- Legacy `tokenId` → authenticated user principal / subject claim
- Legacy `SsoId` → participant identity claim or user profile map
- Legacy participant profile access → policy-based access to the current participant profile
- Legacy admin access → internal authorization policy for admin roles and customer ownership checks

### Impact

This is a critical migration domain because the participant domain is strongly coupled to external user identity and access. If identity mapping is wrong, the participant domain will fail not only at the profile screen but also at scheme selection, viewer assignment, and pending updates.

---

## Database Strategy

Classify each participant-related persistence area as one of the following:

### Keep SP

These are good candidates to retain initially due to established business logic and lower migration risk:

- participant lookup procedures by customer and SSO identity
- participant list procedures used by admin screens
- scheme membership retrieval procedures used in participant scheme management
- pending participant update retrieval procedures

**Rationale:**

- They encapsulate legacy business rules already proven in production.
- They are tied to the current data model and are consistent with the current PTLIMS operating model.
- They reduce migration risk while the new REST and application services are stabilised.

### Wrap SP

These are suitable for service-level wrapping rather than full replacement:

- participant save and update procedures that still manage status and relationship updates
- scheme selection save operations that update month-level participation flags
- viewer-assignment persistence for participant-related access rights

**Rationale:**

- Real business logic is still encoded in the SQL layer.
- The target service can wrap these calls but expose cleaner domain commands.
- This is the safest mid-term approach while preserving operational behavior.

### Replace SP

These should be replaced only when the target domain model is explicitly re-specified and validated:

- complex approval logic for pending participant updates if the target workflow is redesigned
- any scheme membership logic that is too tightly coupled to the legacy contract structure
- highly stateful participant-to-viewer cleanup logic associated with deactivation flows

**Rationale:**

- These areas are operationally and behaviourally rich.
- They are likely to benefit from explicit domain services rather than stored procedures.
- Replacement should occur after business rules are codified in the new application model.

---

## API Client Design

### PTL.ApiClient

The API client for Participant should expose strongly typed, domain-oriented methods rather than raw HTTP plumbing.

**Suggested client surface:**

- GetCurrentParticipantAsync()
- GetParticipantsForCustomerAsync(customerId, includeInactive)
- GetParticipantPendingUpdatesAsync(participantId)
- SubmitParticipantPendingUpdateAsync(participantId, request)
- GetParticipantSchemeChoicesAsync(participantId)
- SaveParticipantSchemeChoiceAsync(participantId, schemeChoice)
- UpdateViewerAccessAsync(participantId, viewerIds)

**Design principles:**

- Keep the API client aligned to use cases rather than ASMX method names.
- Translate legacy concepts to modern names such as “participant profile”, “scheme choice”, and “pending update”.
- Ensure the client works for both internal and external web applications.
- Avoid exposing transport concerns or CSLA-specific terminology.

---

## Target Project Placement

### PTL.Api

Place participant endpoints and application workflows here:

- participant resource endpoints
- participant profile and list queries
- pending update submission and retrieval
- participant-scheme selection workflows
- participant viewer management endpoints

### PTL.ApiClient

Place participant client models and helper methods here:

- participant DTO contracts
- participant query requests and responses
- scheme-choice and pending-update request models
- HTTP wrappers for participant operations

### PTL.Core

Place domain abstractions here:

- participant aggregate root logic
- participant validation rules
- participant identity policy and status transitions
- cross-domain coordination rules

### PTL.Data

Place repository and persistence adapters here:

- participant repository implementation
- participant persistence queries
- SQL adapters for legacy stored procedures or wrapped tables
- mapping between database rows and participant aggregate models

### PTL.Contracts

Place external contracts here:

- participant DTOs
- participant profile request/response models
- pending update models
- participant scheme choice models

### PTL.InternalWeb

Place admin screens here:

- participant list
- participant management
- participant viewer assignment
- participant scheme management

### PTL.ExternalWeb

Place external participant pages here:

- my profile screen
- pending updates submission flow
- scheme selection display and actions

---

## Future Domain Dependencies

### Domains depending on Participant

The following domains depend on the Participant domain:

- Customer domain depends on participant ownership and validation
- Contract domain depends on participant membership in scheme and contract selection workflows
- Scheme domain depends on participant scheme choices and distribution participation
- Distribution domain depends on participant participation state and current-year participation
- Reporting domain depends on participant participation and visibility data
- Authentication / user access depends on the participant-to-user mapping

### Participant depends on other domains

The Participant domain also depends on:

- Customer for ownership and customer-context filtering
- Scheme for membership options and distribution schedule availability
- Contract for contract-level membership and pricing context
- User identity and authentication for external access
- Viewer/role model for access visibility

---

## Feature Breakdown

### Phase 1

- Participant profile read model
- Participant list for customer
- Current participant lookup by authenticated user
- Basic participant active/inactive status read operations

### Phase 2

- Participant create and edit screens
- Participant scheme selection management
- Pending participant update creation and retrieval
- Viewer assignment management

### Phase 3

- Workflow-driven approval and rejection processing
- Participant deactivation process with downstream cleanup
- Advanced scheme-month eligibility logic and validation
- External self-service profile update approval workflow

---

## Risks

### High risk areas

- SSO and participant identity mapping
- Pending update workflows and approval semantics
- Participant-scheme month-level override logic
- Legacy viewer assignment cleanup on inactive participant state
- Downstream impact of participant deactivation on distribution access

### Operational risk

The legacy system bundles several concerns into a single participant aggregate: identity, access, membership, approvals, and reporting. If the migration duplicates that coupling too early, the new model will be brittle and hard to evolve.

### Business risk

A participant-related change could affect customer, scheme, or distribution workflows without obvious UI warning signs. This means migration validation must include cross-domain scenario tests, not just direct participant CRUD checks.

---

## Dependencies

The Participant migration depends on:

- Customer domain readiness and customer identity model
- Contract and scheme model definitions
- Authentication and claim mapping strategy
- Legacy stored procedure inventory and behavioural validation
- External participant portal requirements and approval policy
- Distribution and results domain semantics for active participation and scheme eligibility

---

## Testing Strategy

### Functional tests

- participant creation and edit flows
- participant list by customer
- participant lookup by user and by SSO id
- active/inactive transitions
- scheme membership save and retrieval
- viewer assignment and removal
- pending updates create/read/update

### Integration tests

- API endpoints against a representative participant dataset
- customer-to-participant relationship integrity
- scheme-selection data integrity
- participant access rules by role and ownership
- deactivation behavior and linked viewer cleanup

### Regression tests

- compare legacy participant administrative behaviour against new system behavior for the same business scenarios
- validate participant data is not changed directly during pending-update workflows without explicit approval
- validate scheme and distribution downstream access after participant state changes

---

## Development Readiness

### Can development start?

Yes, but only in a bounded and staged way.

### What is missing?

- final target participant aggregate model and approval rules
- agreed participant identity mapping from auth claims to participant profile
- final contract and scheme domain boundaries
- confirmation of which legacy stored procedures will be retained versus wrapped or replaced
- explicit business approval workflow for pending participant changes

### Recommended readiness approach

Proceed with Phase 1 on a limited set of participant read and list flows, while the identity mapping and approval semantics are confirmed. Do not begin a broad replacement of viewer and scheme logic until the live business behaviour has been validated against the legacy system.

---

## Recommendations

1. Treat Participant as a central aggregate, but not as a monolith.
   - Separate participant identity, participant profile, scheme choices, viewer access, and pending updates into discrete subdomains inside the overall participant aggregate.

2. Preserve the approval pattern from the legacy system in the first migration wave.
   - This reduces business risk and aligns with current operational behaviour.

3. Make identity mapping explicit and claim-based.
   - The participant domain should resolve user identity without relying on legacy service-side token assumptions.

4. Use a staged repository/database strategy.
   - Retain proven stored procedures first, then replace or wrap as domain logic becomes explicit.

5. Validate cross-domain impact early.
   - Ensure participant changes do not unexpectedly affect customer, scheme, distribution, or external access flows.

6. Keep audit and deactivation logic as first-class domain concerns.
   - They are part of the operational reality of the participant lifecycle and should not be treated as optional extras.

---

## Summary

The Participant domain is a core business hub in PTLIMS and should be migrated as a first-class aggregate with clear boundaries between:

- participant identity
- participant profile data
- scheme membership
- viewer access
- pending change workflows
- downstream distribution and access effects

The migration should preserve the business behaviour of the legacy application while introducing clearer REST, repository, and domain boundaries. The main modernization risk is not the participant record itself; it is the way the legacy application combines identity, approval, participation, and access concerns into one operationally sensitive domain cluster.
