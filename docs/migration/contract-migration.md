# Contract Domain Migration Plan

**Domain:** Contract
**Scope:** TO-BE migration plan for the legacy Contract domain in PTLIMS
**Analysis basis:** docs/analysis/contract-analysis.md, docs/analysis/customer-analysis.md, docs/analysis/participant-analysis.md, docs/analysis/csla-analysis.md, docs/migration/api-migration.md, docs/source/PTLIMS-HLD-v0.3.docx
**Status:** Design only; no implementation code produced.

---

## Current State Summary

The legacy Contract domain is the year-scoped commercial agreement between a `Customer` and PTLIMS. It is implemented as a mutable CSLA `BusinessBase` (`Contract`) persisted to `tblContract`, plus a cluster of read-only aggregate/projection objects (`ContractInfo`, `ContractItems`, `ContractSchemeCollection`, `ContractRenewal`, `ContractMergeInfo`) that reassemble pricing, scheme membership, and mail-merge data on demand.

The current AS-IS shape is:

- `Contract` owns identity (`ContractId`, `CustomerId`, `YearId`), UT/FT contract numbering (mutually exclusive), pricing (courier/postage/special-delivery unit prices and counts, discount rate, administration charge), lifecycle dates, invoicing flags, and approval metadata.
- The contract's actual priced line items ("contract items") are **not stored on a Contract-owned table** — they are participant-scheme selections owned by the Participant domain (`tlnkParticipantScheme`), reassembled per contract by `ContractItems`/`ContractSchemeCollection` via `spgContractItems`.
- Pricing for online-order contracts is recomputed dynamically at read time from live scheme/participant-scheme data rather than trusted from stored counts, and contract-item removal uses a deferred-deletion UI pattern (`IsRemoved` flag + delete queue) rather than immediate persistence.
- A narrow external ASMX surface (`PendingContractOrder.asmx`, `AvailableSchemeCollection.asmx`, `CurrentlyParticipatingSchemeCollection.asmx`) supports an external participant "contract order" pending-approval workflow, but no corresponding external Web Forms page was found in this workspace — the consuming external UI is unconfirmed.
- Mail-merge document generation (contract letter, sample-address letter, job sheet, renewal letter) is a first-class, internal-admin-facing capability of this domain.
- A known security gap exists: `AvailableSchemeCollection.asmx`/`CurrentlyParticipatingSchemeCollection.asmx` both have a role-check call present in code but **commented out**, mirroring the same gap already documented for the Participant domain.
- `tblContract`'s incremental `ALTER TABLE IF NOT EXISTS` migration history mirrors the pattern that caused the `spiParticipant` "too many arguments" schema-drift bug encountered during the Participant migration (see repo memory) — the same risk class applies to `spiContract`/`spuContract`.

In practice, the Contract domain is a **pricing and aggregation layer** sitting on top of Customer (ownership, currency), Scheme (pricing/postage rules), and Participant (`tlnkParticipantScheme`, the actual line items) — it does not own its own line-item table, which is an important boundary distinction for the target design.

---

## Domain Boundaries

### What belongs to this domain?

- Contract master record: identity, year, UT/FT numbering, signatory, lifecycle dates, approval metadata
- Contract pricing configuration: courier/postage/special-delivery unit prices and counts, discount rate, administration charge
- Contract invoicing flags: `OptOutOfInvoiceGeneration`, `IsInvoiceSent`, `IsOnlineOrder`
- Contract active/read-only state
- Contract item aggregation and pricing computation (read-model over Participant-owned scheme selections)
- Contract list/summary projections for a customer (current/historical)
- Mail-merge document generation read-models (`ContractRenewal`, `ContractMergeInfo`) and the export trigger workflow
- External "pending contract order" workflow (year-scoped scheme-ordering proposal, keyed by customer + year)

### What does not belong to this domain?

- Customer master configuration and customer account lifecycle (Customer domain)
- Scheme catalogue definitions, postage-type/pricing rules, and year-based scheme metadata (Scheme domain)
- Participant identity, participant-scheme month/price selections, and the `tlnkParticipantScheme` table itself (Participant domain) — Contract only **reads and aggregates** this data, it does not own it
- Distribution execution and result tabulation logic (Distribution / Results domain)
- Invoicing/financial posting logic beyond the flags and computed totals exposed by this domain (Invoicing domain, if modelled separately)
- Identity provider integration and token issuance (Authentication domain)
- Mail-merge template management/upload (`UploadedTemplateCollection`) beyond the read dependency at export time

### Boundary statement

The Contract domain should own the year-scoped commercial agreement, its pricing configuration, and the read-model that aggregates and prices a customer's scheme purchases for the year — but it should not own participant-scheme selection data itself, scheme catalogue/pricing rules, or invoicing execution. Any redesign that pulls `tlnkParticipantScheme` ownership into Contract, or that duplicates scheme pricing rules inside Contract, should be treated as a boundary violation.

---

## UI Migration Mapping

### Legacy Page → Target Page

| Legacy Page | Legacy Role | Target Page / Surface | Target Placement | Notes |
|---|---|---|---|---|
| Contracts Admin / ContractList.aspx | Admin | Contract list screen (current/historical, per customer) | PTL.InternalWeb | List, export/mail-merge actions |
| Contracts Admin / Contract.aspx | Admin | Contract details screen | PTL.InternalWeb | Create, edit, save contract |
| Contracts Admin / ContractItems.aspx | Admin | Contract items screen | PTL.InternalWeb | Priced scheme line items, remove item, navigate to scheme selection |
| (unconfirmed external UI consuming `PendingContractOrder.asmx`) | External participant | Contract renewal / ordering page | PTL.ExternalWeb | `[NEEDS INPUT: confirm external UI requirement — no legacy Web Forms page found]` |

### UI migration considerations

- Keep the internal admin surface (list/details/items) structurally aligned with the Customer/Participant admin pattern already established in `PTL.InternalWeb` (govuk-table list + summary-list details).
- `ContractItems` should render as a read-model view backed by a Contract-domain query that internally calls into Participant-domain data — the target API should not require the InternalWeb page to call two separate domains directly; the Contract API surface should present a single aggregated response.
- The deferred-deletion ("mark removed, save later") UX pattern from `ContractItems.aspx` should be preserved in the first migration wave for behavioural parity, but the underlying delete action should become an explicit REST mutation (`DELETE`/`PATCH`) rather than an in-memory flag reconciled on a separate save click, to avoid carrying forward Web Forms view-state semantics.
- Mail-merge export actions (contract letter, sample-address letter, job sheet, renewal letter) are out of scope for the first UI wave unless explicitly prioritised — they depend on template upload infrastructure (`UploadedTemplateCollection`) not otherwise covered by this domain.
- Confirm whether an external participant-facing "contract order" page is actually required before building `PTL.ExternalWeb` UI for it — the legacy consuming page was not located in this workspace.

---

## API Migration Mapping

### Legacy ASMX → REST Endpoint

| Legacy ASMX Service | Legacy WebMethod | Target REST Endpoint | Target Project |
|---|---|---|---|
| (internal, no ASMX — direct BO calls from Web Forms) | `Contract.FetchContract` | GET /api/contracts/{contractId} | PTL.Api |
| (internal, no ASMX) | `Contract.NewContract` / `Contract.Save()` (insert) | POST /api/customers/{customerId}/contracts | PTL.Api |
| (internal, no ASMX) | `Contract.Save()` (update) | PUT /api/contracts/{contractId} | PTL.Api |
| (internal, no ASMX) | `ContractInfoDataAccess.FetchContractInfoCollection` | GET /api/customers/{customerId}/contracts?active={0\|1} | PTL.Api |
| (internal, no ASMX) | `ContractInfoDataAccess.FetchContractInfoCollectionByYear` | GET /api/customers/{customerId}/contracts?year={yearId} | PTL.Api |
| (internal, no ASMX) | `ContractItems.FetchContractItems` | GET /api/contracts/{contractId}/items | PTL.Api |
| (internal, no ASMX) | contract-item removal (deferred delete in `ContractItems.aspx`) | DELETE /api/contracts/{contractId}/items/{participantSchemeId} | PTL.Api |
| `PendingContractOrder.asmx` | `GetPendingContractOrder(tokenId, customerId, isSubmitted, yearId)` | GET /api/customers/{customerId}/contracts/pending?year={yearId} | PTL.Api |
| `PendingContractOrder.asmx` | `InsertPendingContractOrder(tokenId, penConOrder)` | POST /api/customers/{customerId}/contracts/pending | PTL.Api |
| `PendingContractOrder.asmx` | `UpdatePendingContractOrder(tokenId, penConOrder)` | PUT /api/customers/{customerId}/contracts/pending/{id} | PTL.Api |
| `AvailableSchemeCollection.asmx` | `FetchAvailableSchemes(tokenId, yearId)` | GET /api/contracts/{yearId}/available-schemes | PTL.Api |
| `CurrentlyParticipatingSchemeCollection.asmx` | `FetchCurrentlyParticipatingSchemes(tokenId, yearId, participatingYearId, participantId)` | GET /api/participants/{participantId}/currently-participating-schemes?year={yearId}&participatingYear={participatingYearId} | PTL.Api |
| `PendingParticipantSchemeCollection.asmx` | `FetchPendingParticipantSchemeCollection(tokenId, contractId)` | GET /api/contracts/{contractId}/scheme-choices | PTL.Api (cross-referenced with Participant domain) |
| `PendingParticipantScheme.asmx` | `Insert/Update/DeletePendingParticipantScheme` | POST/PUT/DELETE /api/contracts/{contractId}/scheme-choices[/{id}] | PTL.Api (cross-referenced with Participant domain — see `docs/migration/participant-migration.md`) |

### API design direction

- The internal admin pages (`Contract.aspx`, `ContractList.aspx`, `ContractItems.aspx`) currently call the CSLA `Contract`/`ContractInfo`/`ContractItems` objects directly with no ASMX layer — the REST surface for these must be **newly designed**, not lifted from an existing SOAP contract, unlike Customer/Participant.
- `GET /api/contracts/{contractId}/items` should return a single aggregated read-model (equivalent to today's `ContractItems`/`ContractSchemeCollection`) so that `PTL.InternalWeb` does not need to orchestrate calls into both Contract and Participant APIs to render the items screen.
- Pending contract order and scheme-choice endpoints should be modelled consistently with the equivalent Customer/Participant pending-update endpoints (`GET`/`POST`/`PUT` on a `pending` sub-resource), preserving the "one active pending record per customer/year" uniqueness rule from the legacy `InsertPendingContractOrder` check.
- Mail-merge export endpoints are not mapped to REST in this pass — they should be treated as a separate document-generation capability (see `docs/migration/api-migration.md` for general export-service direction) and are not required for Phase 1/2 functional parity.

---

## Repository Mapping

### Repository Name: ContractRepository

**Purpose:** read and write the contract master record and its list/aggregate projections.

**Methods:**

- GetById(contractId)
- ListByCustomer(customerId, activeFilter)
- ListByCustomerAndYear(customerId, yearId)
- SaveContract(contract) — create or update
- GetContractItems(contractId) — aggregated priced scheme line items
- RemoveContractItem(contractId, participantSchemeId) — deferred-delete equivalent, made explicit

**Stored Procedures:**

- spgContractByContractId
- spiContract
- spuContract
- spgContractInfoByCustomerId
- spgContractInfoByCustomerIdAndYearId
- spgContractItems
- spgContractMerge (mail-merge projection, if retained)

**Database Dependencies:**

- tblContract
- tlnkParticipantScheme (read-only join, owned by Participant domain — see `docs/migration/participant-migration.md` repository mapping for the authoritative write path)
- tblCustomer (parent reference for ownership/currency)
- pending contract order table(s) `[NEEDS INVESTIGATION — not confirmed in docs/analysis/contract-analysis.md]`

### Repository design guidance

- Keep `ContractRepository` focused on the `tblContract` aggregate and its list projections; do **not** duplicate participant-scheme write logic here — `RemoveContractItem` should delegate to the Participant-domain repository/service for the actual `tlnkParticipantScheme` mutation, with Contract only orchestrating the "is this item part of this contract" check.
- Treat `ContractItems`'s dynamic online-order pricing recomputation (courier/postage totals recalculated from live scheme/participant-scheme data rather than trusted stored counts) as an explicit **pricing service** concern, not silent repository logic, so it is testable and visible in code review.
- Do not assume `spgContractItems` and `spgContractMerge`'s exact join shape from the legacy SQL without re-verifying against the live schema before wrapping them — this pattern already caused a parameter-count mismatch (`spiParticipant`) elsewhere in this migration.

---

## Authentication Mapping

### Current

Internal Contract Admin pages rely on ASP.NET session state (`Session("CurrentCustomer")`, `Session("CurrentContract")`, `Session("ContractId")`) with no explicit per-request identity/role check observed in the reviewed code-behind. The external ASMX surface resolves identity via `UserService.Service.GetUserByTokenId(tokenId)`, the same token-based SSO pattern used across Customer/Participant.

In the current model:

- there is **no evidence** of an explicit `Permission.CanEdit...`-style gate on the internal Contract Admin pages (contrast with Customer's `EditCustomerDetails.aspx` → `master.Permission.CanEditCustomer` check)
- `AvailableSchemeCollection.asmx`/`CurrentlyParticipatingSchemeCollection.asmx` contain a role-check call (`AuthoriseUser(tokenId, Roles.Participant)`) that is **commented out** — i.e. present in code but disabled
- `PendingContractOrder.asmx` performs no visible role/ownership check beyond identity resolution to `ssoId`

### Future

The target migration should map to ASP.NET Core identity and policy-based authorization, with contract-specific policies scoped by customer ownership.

**Recommended mapping:**

- Legacy `tokenId` → authenticated user principal / subject claim
- Legacy internal-admin session gating → an explicit internal admin authorization policy (e.g. Contracts Admin role), not implicit session-presence checks
- Legacy external participant contract-order flow → policy-based access scoped to the participant's own customer, explicitly re-enabling the equivalent of the currently-disabled `AuthoriseUser(tokenId, Roles.Participant)` check
- Legacy `customerId`/`yearId` ownership implied only by session/token → explicit ownership check in the API layer (does the resolved identity's customer match the requested `customerId`?)

### Impact

This is a **medium-to-high** risk migration domain from an authorization standpoint specifically because it currently has the **weakest** visible access control of the three domains analysed so far (Customer, Participant, Contract): no internal admin permission gate was found, and the external ASMX role check is disabled rather than merely absent-by-design. Re-enabling proper authorization here must not silently change who can already access these screens in a way that breaks existing admin workflows, but must close the external-facing gap before go-live.

---

## Database Strategy

### Keep SP

- `spgContractByContractId` — proven fetch logic, including the derived `Readonly` flag; low risk to retain initially.
- `spgContractInfoByCustomerId` / `spgContractInfoByCustomerIdAndYearId` — simple list projections, low migration risk.
- `spgContractItems` — encodes the join between `tblContract`, scheme, and `tlnkParticipantScheme` that reconstructs contract items; retaining this avoids re-deriving complex join/pricing logic in application code during the first wave.

**Rationale:**
- These encapsulate already-proven read paths with low write risk.
- They reduce migration risk while the new REST/application services are stabilised.

### Wrap SP

- `spiContract` / `spuContract` — real validation and default-derivation logic exists in the CSLA layer (`AddBusinessRules`, `DataPortal_Create` currency-based admin-charge default) that must be preserved in the service layer; wrap the existing SPs behind a clean `ContractRepository.SaveContract` while confirming the exact current parameter list against the live schema before wrapping (schema-drift risk, see Current State Summary).
- `spgContractMerge` — used only by the mail-merge subsystem; wrap as-is if mail-merge export is retained in an early phase, since its business value is in the join shape rather than needing new business rules.

**Rationale:**
- Real, non-trivial logic is still encoded in the SQL/CSLA layer.
- Wrapping preserves operational behaviour while giving the new service layer explicit, testable command boundaries.

### Replace SP

- Pending contract order persistence (backing `PendingContractOrder.asmx`) — the exact SP/table was not confirmed in `docs/analysis/contract-analysis.md`; once identified, this is a good candidate for a proper domain service once the target pending-workflow model (see `docs/migration/participant-migration.md`'s pending-update pattern) is agreed, rather than perpetuating an unconfirmed legacy structure.
- The online-order dynamic pricing recomputation currently embedded in `ContractItems.CourierPriceTotal`/`PostagePriceTotal` (re-fetching `Contract` and `Scheme` mid-calculation) should be replaced with an explicit, testable pricing service in the target design once the calculation is fully understood and validated — it should not be replatformed as opaque SQL or left as an ad-hoc in-memory recomputation.

**Rationale:**
- These areas are either unconfirmed (pending order persistence) or operationally risky as opaque logic (dynamic pricing) and will benefit from explicit domain services once the legacy behaviour is fully validated.
- Replacement should only occur after the exact current behaviour is confirmed against the live schema/application, consistent with the "validate before replace" lesson already learned from the `spiParticipant` schema-drift issue.

---

## API Client Design

### PTL.ApiClient

The API client for Contract should expose strongly typed, domain-oriented methods rather than raw HTTP plumbing.

**Suggested client surface:**

- GetContractAsync(contractId)
- GetContractsForCustomerAsync(customerId, activeFilter)
- GetContractsForCustomerByYearAsync(customerId, yearId)
- CreateContractAsync(customerId, request)
- UpdateContractAsync(contractId, request)
- GetContractItemsAsync(contractId)
- RemoveContractItemAsync(contractId, participantSchemeId)
- GetPendingContractOrderAsync(customerId, yearId)
- SubmitPendingContractOrderAsync(customerId, request)
- UpdatePendingContractOrderAsync(customerId, pendingOrderId, request)
- GetAvailableSchemesAsync(yearId)
- GetCurrentlyParticipatingSchemesAsync(participantId, yearId, participatingYearId)

**Design principles:**

- Keep the API client aligned to use cases rather than legacy method names (e.g. "contract items" not "spgContractItems").
- Do not expose CSLA-specific terminology (`IsReadOnly` derivation details, `DoInsertUpdate` parameter shape) through the client surface.
- Ensure the client works for both `PTL.InternalWeb` (contract admin) and, if the external ordering workflow is confirmed as in-scope, `PTL.ExternalWeb`.
- Avoid transport concerns; return domain-oriented DTOs matching `PTL.Contracts`.

---

## Target Project Placement

### PTL.Api

- contract resource endpoints (get/list/create/update)
- contract items aggregation endpoint
- contract item removal endpoint
- pending contract order endpoints
- available/currently-participating scheme lookup endpoints (or delegate to Scheme/Participant domain APIs if those are migrated first)

### PTL.ApiClient

- contract DTO contracts and request/response models
- contract item / pending order request models
- HTTP wrappers for all Contract operations listed above

### PTL.Core

- contract aggregate root logic (identity, UT/FT numbering rule, pricing fields)
- contract validation rules (ported from `AddBusinessRules`: UT/FT XOR, date-not-sentinel, non-negative pricing/counts, max lengths)
- contract pricing/aggregation domain service (replacing `ContractItems`'s dynamic recomputation) — explicitly modelled, not hidden in a repository
- contract read-only state derivation rules (once the `Readonly` SQL predicate is confirmed)

### PTL.Data

- contract repository implementation (EF Core + `FromSqlRaw`/stored procedures, per existing Customer/Participant pattern in this codebase)
- contract-items query implementation (joins into Participant-domain data for the aggregated read-model)
- SQL adapters for `spgContractByContractId`/`spiContract`/`spuContract`/`spgContractInfoByCustomerId(...)`/`spgContractItems`/`spgContractMerge`

### PTL.Contracts

- contract DTOs (list/summary, detail, create/update request)
- contract items response DTOs (scheme grouping + participant-scheme line items + computed totals)
- pending contract order request/response models

### PTL.InternalWeb

- contract list screen (current/historical, per customer)
- contract create/edit screen
- contract items screen (with explicit remove-item action)

### PTL.ExternalWeb

- contract renewal/ordering screen — **only if** the legacy consuming UI is confirmed to exist and be in scope; otherwise defer pending business confirmation

---

## Future Domain Dependencies

### Domains depending on Contract

- Distribution domain depends on active contract-scheme membership (via `tlnkParticipantScheme`, priced/validated through Contract) to determine eligible participation for a year.
- Invoicing/reporting depends on Contract's pricing fields and computed totals (`ContractItems`).
- Participant domain's scheme-selection workflow (`ParticipantScheme.aspx`) writes into a table that Contract reads and prices — the two domains are tightly coupled at the data level even though they are separate aggregates.

### Contract depends on other domains

- Customer for ownership (`CustomerId`) and currency-scoped default pricing (`CurrencyId` → default administration charge).
- Scheme for scheme identity, postage-type, and packaging rules used in item pricing.
- Participant for the actual scheme-selection line items (`tlnkParticipantScheme`/`ParticipantScheme`) that Contract aggregates and prices — Contract must never be designed to also own this data, per the Domain Boundaries section above.

---

## Feature Breakdown

### Phase 1

- Contract detail read model (`GET /api/contracts/{contractId}`)
- Contract list for customer, current/historical (`GET /api/customers/{customerId}/contracts`)
- Contract items read model (`GET /api/contracts/{contractId}/items`), reusing the existing `spgContractItems` join in the first wave

### Phase 2

- Contract create and edit (`POST`/`PUT`), including the UT/FT XOR and sentinel-date validation rules ported from `AddBusinessRules`
- Contract item removal as an explicit mutation (replacing the deferred-deletion UI pattern)
- Pending contract order read/submit/update endpoints

### Phase 3

- Explicit pricing/aggregation domain service replacing `ContractItems`'s dynamic online-order recomputation
- Available/currently-participating scheme lookup endpoints for the external ordering workflow, contingent on confirming that workflow is in scope
- Mail-merge document generation (contract letter, sample-address letter, job sheet, renewal letter), contingent on template-upload infrastructure being migrated

---

## Risks

### High risk areas

- **Authorization gap** — no confirmed internal admin permission gate, and a disabled external role check (`AuthoriseUser(tokenId, Roles.Participant)`) on `AvailableSchemeCollection.asmx`/`CurrentlyParticipatingSchemeCollection.asmx`. Migrating without re-enabling equivalent checks would carry the gap forward into the new system.
- **Schema drift** — `tblContract`'s incremental `ALTER TABLE IF NOT EXISTS` history and the 23+ parameter `spiContract`/`spuContract` calls are exactly the shape that caused the `spiParticipant` "too many arguments" bug already encountered in this migration; the live schema must be re-verified before wrapping these SPs.
- **Dynamic pricing recomputation** — `ContractItems.CourierPriceTotal` (and likely `PostagePriceTotal`/`SpecialDeliveryPriceTotal`) silently ignore stored counts for online-order contracts and recompute from live scheme/participant-scheme data; misunderstanding this during migration would silently change invoicing totals.

### Operational risk

Contract items are not stored on a Contract-owned table — they are read-modelled from Participant-domain data at request time. If the migration duplicates this data into a new Contract-owned table for convenience, it will diverge from the Participant domain's live selections and become a second source of truth.

### Business risk

Contract pricing directly feeds invoicing (`OptOutOfInvoiceGeneration`, `IsInvoiceSent`, computed totals). Any behavioural drift in pricing computation — especially the online-order dynamic recomputation — could silently produce incorrect invoices without an obvious UI symptom.

---

## Dependencies

The Contract migration depends on:

- Customer domain readiness (ownership, currency model) — see `docs/migration/customer-migration.md`
- Participant domain readiness, specifically the `tlnkParticipantScheme`/`ParticipantScheme` write path that Contract's item-removal mutation must delegate to — see `docs/migration/participant-migration.md`
- Scheme domain model (postage type, packaging, pricing) for accurate item-pricing computation
- Confirmation of the pending contract order's actual SP/table (currently unconfirmed) before that workflow can be safely wrapped or replaced
- Confirmation of whether an external "contract order" UI is actually required, before external-web work is scoped
- Authentication/claims mapping strategy shared with Customer/Participant, extended to close the disabled-role-check gap

---

## Testing Strategy

### Functional tests

- contract create and edit flows, including UT/FT XOR and sentinel-date validation
- contract list by customer (current/historical, year-filtered)
- contract items aggregation (scheme grouping, per-item pricing, discount/administration/total pricing)
- contract item removal (explicit mutation, replacing deferred-deletion UI behaviour)
- pending contract order create/read/update, including the one-per-customer/year uniqueness rule

### Integration tests

- API endpoints against a representative contract + participant-scheme dataset
- customer-to-contract relationship integrity (currency-based default admin charge)
- contract-to-participant-scheme join integrity for the items read model
- online-order dynamic pricing recomputation against known scheme/participant-scheme fixtures
- authorization checks: internal admin access gating, external ordering-workflow role/ownership enforcement (re-enabled, not disabled)

### Regression tests

- compare legacy contract admin behaviour (create/edit/list/items) against new system behaviour for the same scenarios
- validate that online-order pricing totals match legacy computed totals for the same scheme/participant-scheme fixtures
- validate that removing a contract item produces the same downstream effect (participant-scheme no longer considered part of the contract) as the legacy deferred-deletion flow

---

## Development Readiness

### Can development start?

Yes, but only on Phase 1 (read-only contract detail/list/items), while several open items from `docs/analysis/contract-analysis.md` are confirmed.

### What is missing?

- exact stored procedure(s)/table(s) backing `PendingContractOrder.asmx`
- the SQL predicate behind `Contract.IsReadOnly` (the `Readonly` column returned by `spgContractByContractId`)
- confirmation of whether customer deactivation cascades to contract deactivation
- confirmation of any internal admin authorization gate for `Contracts Admin` pages
- confirmation of whether an external "contract order" UI is actually required
- current live-schema parameter count for `spiContract`/`spuContract` (schema-drift risk, per the `spiParticipant` precedent)

### Recommended readiness approach

Proceed with Phase 1 (contract detail, list, and items read models) using the already-understood `spgContractByContractId`/`spgContractInfoByCustomerId(...)`/`spgContractItems` procedures, while the open items above are confirmed. Do not begin write-path migration (`spiContract`/`spuContract`, pending contract order mutations) until the live schema is re-verified and the authorization gaps are addressed — the disabled role check and absent admin-page gate should not be carried forward silently.

---

## Recommendations

1. Treat Contract Items as a read-model, not a second source of truth.
   - `ContractItems`/`ContractSchemeCollection` must remain a query over Participant-owned `tlnkParticipantScheme` data; do not introduce a new Contract-owned items table that could diverge from the Participant domain's live selections.

2. Close the authorization gap as part of this migration, not after.
   - Re-enable the equivalent of the currently-disabled `AuthoriseUser(tokenId, Roles.Participant)` check, and add an explicit internal admin authorization policy where none was found in the legacy code.

3. Verify the live schema before wrapping `spiContract`/`spuContract`.
   - Apply the same "confirm parameter count against `sys.parameters` before wrapping" discipline already learned from the `spiParticipant` incident.

4. Make dynamic pricing recomputation an explicit, testable service.
   - Do not replatform `ContractItems`'s online-order pricing logic as opaque SQL or leave it as ad-hoc in-memory recomputation; extract it into a named pricing service with test coverage against known fixtures.

5. Confirm the external ordering workflow's scope before building UI for it.
   - No legacy Web Forms page consuming `PendingContractOrder.asmx` was found in this workspace; get explicit business confirmation before allocating `PTL.ExternalWeb` work to it.

6. Keep pricing/invoicing behaviour changes highly visible.
   - Any change to contract pricing computation should be flagged for explicit business sign-off given its direct link to invoicing, consistent with the "financial impact" risk already identified in `docs/analysis/contract-analysis.md`.

---

## Summary

The Contract domain is the year-scoped commercial and pricing envelope for a customer's scheme participation in PTLIMS. It should be migrated as a distinct aggregate that owns contract identity, pricing configuration, and the read-model aggregation of scheme line items — while explicitly **not** owning the underlying participant-scheme selection data, which remains a Participant-domain responsibility. The main migration risks are not the contract record itself; they are (1) the currently weak/disabled authorization posture on this domain's external surface, (2) schema-drift risk on the same class of stored procedures that already caused an incident in the Participant migration, and (3) the dynamic, easily-misunderstood online-order pricing recomputation that directly feeds invoicing.
