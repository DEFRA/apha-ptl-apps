# Scheme Domain Migration Plan

**Domain:** Scheme
**Scope:** TO-BE migration plan for the legacy Scheme domain in PTLIMS
**Analysis basis:** docs/analysis/scheme-analysis.md, docs/analysis/contract-analysis.md, docs/analysis/customer-analysis.md, docs/analysis/participant-analysis.md, docs/analysis/csla-analysis.md, docs/migration/api-migration.md, docs/source/PTLIMS-HLD-v0.3.docx
**Status:** Design only; no implementation code produced.

---

## Current State Summary

The legacy Scheme domain is the proficiency-testing "round" catalogue: it defines the test/sample configuration, schedule, pricing, and visibility of a scheme, and is the richest, most structurally complex domain analysed in this series (larger than Customer, Participant, or Contract).

The current AS-IS shape is:

- `Scheme` is a CSLA `AuditableBusinessBase` (audited, not just validated) persisted to `tblScheme`, owning four transactionally-coupled child aggregates: `SchemeCurrency` (currency pricing), `Tests`/`TestCollection` (each `Test` itself audited, with `TestResultItems`/`TestMethodItems`/`CategoryItems` nested a level further), `Tabulations`, and `Viewers` (`ViewerScheme`).
- Scheme has two distinct clone operations with materially different meaning: `RenewScheme` (same scheme family, year+1, `SharedId` preserved, postage plan rolled forward by name) and `CopyScheme` (same year, brand-new unrelated `SharedId`).
- The scheme save is a single database transaction with a non-obvious, explicitly-commented insert/delete ordering constraint across the `Tests`/`Tabulations` child collections due to a foreign-key relationship.
- Internal admin pages exist **twice**, under both `ProficiencyTestingWeb/Scheme Admin` and `ProficiencyTestingAdmin/Scheme Admin`, with identical file names — canonical ownership is unconfirmed.
- The domain's own ASMX surface has **inconsistent** authorization enforcement: `SchemeListCollection.asmx` actively enforces a participant role check, while `Scheme.asmx`, `PostagePricingPlan.asmx`, `AvailableSchemeCollection.asmx`, and `CurrentlyParticipatingSchemeCollection.asmx` all have the identical check commented out, and `SchemeCurrency.asmx` has no check at all.
- `tblScheme` shows the same incremental `ALTER TABLE IF NOT EXISTS` schema-drift pattern already encountered for `tblParticipant`/`tblContract` (e.g. `fldAssessor1..4` added after the original table).

In practice, the Scheme domain is the **catalogue and pricing-rule source** that Contract (commercial aggregation/pricing) and Participant (selection) both depend on, and the **structural definition source** (`Tests`/`TestResultItems`/`TestMethodItems`) that the not-yet-analysed Distribution/Results/Tabulation domain depends on. This makes Scheme both foundational and high-blast-radius: a change to its identity model, renewal semantics, or test structure ripples into every domain analysed so far plus at least one not yet analysed.

---

## Domain Boundaries

### What belongs to this domain?

- Scheme master record: identity (`SchemeId`, `SharedId`, `Identifier`, `Name`, `YearId`), schedule, distribution-month/as-available configuration, logistics, customs metadata, consent configuration
- Scheme renewal/copy semantics (`RenewScheme` vs `CopyScheme`) and the year-family relationship (`SharedId`)
- Scheme currency-scoped pricing (`SchemeCurrency`)
- Scheme test/result **structure** definition (`Tests`, `TestResultItems`, `TestMethodItems`, `CategoryItems`) — the definition only, not the results data captured against it
- Scheme viewer visibility (`ViewerScheme`)
- Scheme list/history read-models (`SchemeInfoCollection`)
- Postage pricing plan lookup as consumed by scheme renewal (`PostagePricingPlan` read access)

### What does not belong to this domain?

- Customer master configuration and customer account lifecycle (Customer domain)
- Contract-level commercial aggregation, pricing computation over participant-scheme selections, and invoicing flags (Contract domain) — Scheme supplies pricing/postage **rules**, it does not own the contract-level pricing computation itself
- Participant identity and participant-scheme selection state (`tlnkParticipantScheme`) — Scheme is referenced by these, it does not own them (Participant domain)
- Actual results-entry data, distribution execution, and tabulation/publication logic (Distribution / Results / Tabulation domain) — Scheme defines the **shape** of tests/results, it does not own the captured results themselves
- Postage pricing plan master data ownership (referenced, not owned, by Scheme)
- Identity provider integration and token issuance (Authentication domain)
- Viewer identity/role management itself (only the scheme-to-viewer visibility link belongs here)

### Boundary statement

The Scheme domain should own the catalogue definition of a proficiency-testing round — its identity, schedule, pricing rules, test/result structure, and viewer visibility — but it should not own contract-level commercial pricing computation, participant-scheme selection state, or captured results/tabulation data. Any redesign that pulls contract pricing logic into Scheme, or that stores actual result values on Scheme-owned entities, should be treated as a boundary violation.

---

## UI Migration Mapping

### Legacy Page → Target Page

| Legacy Page | Legacy Role | Target Page / Surface | Target Placement | Notes |
|---|---|---|---|---|
| Scheme Admin / Scheme.aspx | Admin | Scheme details screen | PTL.InternalWeb | Create, edit, renew, copy — four distinct entry modes must map to explicit, separate actions rather than one overloaded page |
| Scheme Admin / SchemeList.aspx | Admin | Scheme list screen | PTL.InternalWeb | List + renewal shortcut; split the "renew" action into an explicit button rather than an overloaded list-row link |
| Scheme Admin / SchemeHistory.aspx | Admin | Scheme family history screen | PTL.InternalWeb | Year-over-year history by `SharedId` |
| Scheme Admin / SchemeListForPrinting.aspx | Admin | Print/export view | PTL.InternalWeb | Low priority; consider a print stylesheet on the list screen instead of a separate page |
| (external) SchemeList.aspx | External participant | My schemes screen (commercial / non-commercial) | PTL.ExternalWeb | Preserve the two-grid (commercial/non-commercial) split; confirm the "bold schemes" highlighting rule with the business before reimplementing it |

**Duplicated admin app note:** `ProficiencyTestingAdmin/Scheme Admin/*` mirrors `ProficiencyTestingWeb/Scheme Admin/*` exactly. Before scoping UI work, confirm which project is the live/canonical one — do not migrate both, and do not silently pick one without confirmation.

### UI migration considerations

- Treat "create", "edit", "renew", and "copy" as four distinct target-page actions with explicit intent, rather than one page branching on query-string flags (`?renewal=true`, `?currentyear=true`) as in the legacy implementation — this removes an easy source of migration bugs given how different `RenewScheme` and `CopyScheme` are in outcome.
- The nested `Tests` editor (with its own `TestResultItems`/`TestMethodItems`/`CategoryItems`) is the single most complex UI surface in this domain; it should be scoped as its own sub-feature rather than a tab within the main scheme form, consistent with its 3-level nested-aggregate shape in the legacy object model.
- Preserve the list screen's ability to initiate a renewal for schemes with no "next" scheme yet, but as an explicit, clearly-labelled action rather than a relabelled hyperlink.
- Confirm whether the "bold schemes" client-side highlighting on the external list has a defined business rule before reimplementing it as-is.

---

## API Migration Mapping

### Legacy ASMX → REST Endpoint

| Legacy ASMX Service | Legacy WebMethod | Target REST Endpoint | Target Project |
|---|---|---|---|
| `Scheme.asmx` | `GetScheme(tokenId, schemeId)` | GET /api/schemes/{schemeId} | PTL.Api |
| `Scheme.asmx` | `GetFullSchemeInformationListCollection(tokenId, yearId)` | GET /api/schemes?year={yearId} | PTL.Api |
| (internal, no ASMX) | `Scheme.NewScheme`/`Scheme.Save()` (insert) | POST /api/schemes | PTL.Api |
| (internal, no ASMX) | `Scheme.Save()` (update) | PUT /api/schemes/{schemeId} | PTL.Api |
| (internal, no ASMX) | `Scheme.RenewScheme(oldSchemeId)` | POST /api/schemes/{schemeId}/renew | PTL.Api |
| (internal, no ASMX) | `Scheme.CopyScheme(oldSchemeId)` | POST /api/schemes/{schemeId}/copy | PTL.Api |
| (internal, no ASMX) | `SchemeInfoCollection.FetchSchemeInfoCollection`/`...ByYearId` | GET /api/schemes/summaries?year={yearId} | PTL.Api |
| (internal, no ASMX) | `SchemeInfoCollection.FetchSchemeInfoCollectionBySharedId` | GET /api/schemes/families/{sharedId}/history | PTL.Api |
| `SchemeListCollection.asmx` | `GetSchemeListCollection(tokenId, listType)` | GET /api/participants/me/scheme-lists?listType={type} | PTL.Api |
| `SchemeListCollection.asmx` | `GetSchemeInfoListCollection(tokenId, yearId)` | GET /api/schemes/info?year={yearId} | PTL.Api |
| `SchemeCurrency.asmx` | `GetSchemeCurrency(tokenId, schemeId, currencyId)` | GET /api/schemes/{schemeId}/currencies/{currencyId} | PTL.Api |
| `SchemeCurrency.asmx` | `GetAllSchemeCurrency(tokenId)` | GET /api/schemes/{schemeId}/currencies | PTL.Api |
| `PostagePricingPlan.asmx` | `GetPostagePricingPlan(tokenId, postagePricingPlanId)` | GET /api/postage-pricing-plans/{planId} | PTL.Api |
| `PostagePricingPlan.asmx` | `GetAllPostagePricingPlans(tokenId)` | GET /api/postage-pricing-plans | PTL.Api |
| `AvailableSchemeCollection.asmx` | `FetchAvailableSchemes(tokenId, yearId)` | GET /api/contracts/{yearId}/available-schemes | PTL.Api (cross-referenced with Contract domain — see `docs/migration/contract-migration.md`) |
| `CurrentlyParticipatingSchemeCollection.asmx` | `FetchCurrentlyParticipatingSchemes(tokenId, yearId, participatingYearId, participantId)` | GET /api/participants/{participantId}/currently-participating-schemes?year={yearId}&participatingYear={participatingYearId} | PTL.Api (cross-referenced with Contract domain) |

### API design direction

- `GET /api/schemes/{schemeId}` should be the single authoritative read model, and — unlike the legacy `Scheme.asmx.GetScheme`, which omits the nested `Tests`/`SchemeCurrency`/`Viewers` collections — the target API should decide explicitly (via query parameter or separate sub-resource endpoints, e.g. `GET /api/schemes/{schemeId}/tests`) whether nested data is included, rather than silently omitting it as the legacy method does.
- `POST /api/schemes/{schemeId}/renew` and `POST /api/schemes/{schemeId}/copy` must be modelled as **distinct** endpoints with distinct response semantics (new scheme with same `SharedId` vs new scheme with new `SharedId`) — never collapse these into one "duplicate" endpoint with a flag, given how easily this distinction is lost in translation.
- Reuse the same available/currently-participating scheme endpoints already scoped in `docs/migration/contract-migration.md` rather than re-defining them from the Scheme side — these are shared surface area between the two domains.
- Authorization for every one of these endpoints must be **explicitly decided and consistently applied** in the target design — the legacy inconsistency (`SchemeListCollection` enforced, others disabled or absent) must not be carried forward as "some endpoints are protected, some aren't" without an explicit, documented policy decision.

---

## Repository Mapping

### Repository Name: SchemeRepository

**Purpose:** read and write the scheme master record and its four child aggregates (currency pricing, tests/results structure, tabulations, viewer visibility), plus list/history projections.

**Methods:**

- GetById(schemeId)
- GetBySharedId(sharedId) — family history
- ListByYear(yearId)
- ListAll()
- SaveScheme(scheme) — create or update, encompassing all four child collections in one transaction
- RenewScheme(oldSchemeId) — explicit renewal command, not a generic "clone"
- CopyScheme(oldSchemeId) — explicit copy command, not a generic "clone"
- GetSchemeCurrency(schemeId, currencyId) / ListSchemeCurrencies(schemeId)

**Stored Procedures:**

- spgSchemeBySchemeId
- spiScheme
- spuScheme
- spgSchemeInfoBySchemeId
- spgSchemeInfoBySharedId
- spgSchemeInfoByYearId
- child-collection procedures for `SchemeCurrency`/`Tests`/`TestResultItems`/`TestMethodItems`/`CategoryItems`/`Tabulations`/`ViewerScheme` — exact names unconfirmed (see `docs/analysis/scheme-analysis.md` Open Questions)

**Database Dependencies:**

- tblScheme
- child tables backing `SchemeCurrency`, `Test`/`TestResultItem`/`TestMethodItem`/`CategoryItem`, `Tabulations` (as a Scheme child collection), `ViewerScheme` — table names unconfirmed, `[NEEDS INVESTIGATION]`
- tlnkParticipantScheme (read-only reference, owned by Participant domain)

### Repository design guidance

- Do **not** flatten `SaveScheme` into a single wide stored-procedure call the way `Contract`/`Participant` were wrapped — the legacy `Scheme.DataPortal_Insert`/`DataPortal_Update` explicitly requires a transaction spanning five separate write operations (parent + 4 child collections) with a documented cross-collection ordering constraint; the target repository must preserve both the transactional boundary and the ordering constraint, not just the individual SQL calls.
- Model `RenewScheme` and `CopyScheme` as two named repository/service methods, never as one parameterised method — this mirrors the "explicit, not overloaded" UI guidance above and reduces the risk of the two being conflated during implementation.
- Confirm the exact child-table schema for `SchemeCurrency`/`Test*`/`Tabulations`/`ViewerScheme` against the live database before wrapping any of the associated stored procedures — apply the same "verify before wrap" discipline already required for Contract/Participant given the confirmed `tblScheme` schema-drift pattern.
- Treat the `Tests`/`TestResultItems`/`TestMethodItems`/`CategoryItems` structure as a candidate for its own bounded sub-module within `PTL.Core`/`PTL.Data` (e.g. `Scheme.Tests`) rather than flattening it into the top-level `Scheme` aggregate, given its independent audit behaviour (`Test` is itself `AuditableBusinessBase`).

---

## Authentication Mapping

### Current

Internal `Scheme Admin` pages rely on ASP.NET session/ViewState (`SessionPageStatePersister`) with no explicit per-request identity/role check observed in the reviewed code-behind — the same gap already flagged for Contract's internal admin pages. The external participant-facing page (`SchemeList.aspx`) uses a shared `ParticipantPageGuard` helper. The ASMX layer resolves identity via `UserService.Service.GetUserByTokenId(tokenId)`.

In the current model:

- `SchemeListCollection.asmx` **actively enforces** `AuthoriseUser(tokenId, Roles.Participant)`
- `Scheme.asmx`, `PostagePricingPlan.asmx`, `AvailableSchemeCollection.asmx`, `CurrentlyParticipatingSchemeCollection.asmx` all have the **identical role-check call present in code but commented out**
- `SchemeCurrency.asmx` has **no role-check call at all**, only a `ssoId <> Guid.Empty` sanity check
- no internal admin permission gate was found for `Scheme Admin` pages

### Future

The target migration should map to ASP.NET Core identity and policy-based authorization, with a **single, explicitly documented** policy applied consistently across every Scheme endpoint — not the mixture of enforced/disabled/absent checks found in the legacy code.

**Recommended mapping:**

- Legacy `tokenId` → authenticated user principal / subject claim
- Legacy internal `Scheme Admin` implicit session gating → an explicit internal Scheme Admin authorization policy
- Legacy inconsistent participant role check across `Scheme.asmx`/`SchemeListCollection.asmx`/`SchemeCurrency.asmx`/etc. → a single "authenticated participant" policy applied uniformly to every read endpoint that returns participant-visible scheme data
- Legacy `ParticipantPageGuard` (external Web Forms) → equivalent ASP.NET Core policy/middleware applied to `PTL.ExternalWeb`'s scheme pages

### Impact

This is a **high** risk migration domain from an authorization standpoint: it is the only domain analysed so far where the **same class of endpoint** (fetch scheme/pricing data for a participant) is inconsistently protected within the domain itself, rather than uniformly missing or uniformly present. A migration that "wraps" each legacy ASMX method 1:1 without first deciding a single target policy would mechanically reproduce this inconsistency in the new system.

---

## Database Strategy

### Keep SP

- `spgSchemeBySchemeId` — proven fetch logic including the derived `Readonly` flag; low risk to retain initially.
- `spgSchemeInfoBySchemeId` / `spgSchemeInfoBySharedId` / `spgSchemeInfoByYearId` — simple list/history projections, low migration risk.

**Rationale:**
- These encapsulate already-proven read paths with low write risk, consistent with the "keep proven reads" approach already applied to Customer/Participant/Contract.

### Wrap SP

- `spiScheme` / `spuScheme` — real validation (`AddBusinessRules`, including the distribution-month/as-available mutual exclusivity and conditional consent-text rule) and non-trivial default/rollover logic (`RenewScheme`'s postage-plan lookup) exist in the CSLA layer that must be preserved in the service layer. Wrap behind explicit `SaveScheme`/`RenewScheme`/`CopyScheme` methods, verifying the current live-schema parameter count first.
- Child-collection persistence procedures for `SchemeCurrency`/`Tests`/`Tabulations`/`ViewerScheme` (exact names unconfirmed) — wrap as a set behind the same transactional `SaveScheme` operation, preserving the documented insert/delete ordering constraint.

**Rationale:**
- Real, non-trivial business logic (validation rules, renewal roll-forward, transactional multi-collection ordering) is encoded here and must not be silently dropped or reordered.

### Replace SP

- None identified as safe-to-replace in this pass. Given the confirmed structural complexity (four child collections, cross-collection FK ordering, audited sub-aggregates), replacement of any Scheme write-path SP should only be considered **after** the full child-table schema is confirmed and the ordering constraint is understood and test-covered — premature replacement here carries the highest risk of any domain analysed so far.

**Rationale:**
- Unlike Contract's dynamic pricing recomputation (a good, understood replace candidate), Scheme's complexity is primarily structural/relational (multi-table transactional save with FK ordering), which is exactly the kind of logic that is easy to get subtly wrong if reimplemented before the legacy behaviour is fully mapped.

---

## API Client Design

### PTL.ApiClient

**Suggested client surface:**

- GetSchemeAsync(schemeId)
- GetSchemesForYearAsync(yearId)
- GetSchemeFamilyHistoryAsync(sharedId)
- CreateSchemeAsync(request)
- UpdateSchemeAsync(schemeId, request)
- RenewSchemeAsync(schemeId)
- CopySchemeAsync(schemeId)
- GetSchemeCurrencyAsync(schemeId, currencyId)
- GetSchemeCurrenciesAsync(schemeId)
- GetPostagePricingPlanAsync(planId)
- GetAllPostagePricingPlansAsync()
- GetMySchemeListsAsync(listType)
- GetAvailableSchemesAsync(yearId)
- GetCurrentlyParticipatingSchemesAsync(participantId, yearId, participatingYearId)

**Design principles:**

- Expose `RenewSchemeAsync`/`CopySchemeAsync` as two named methods, mirroring the repository-layer guidance above — never a single `CloneSchemeAsync(mode)` method.
- Do not expose CSLA-specific terminology (`IsReadonly` derivation, `AuditableBusinessBase` semantics, `DoInsertUpdate` parameter shape) through the client surface.
- Ensure the client works for both `PTL.InternalWeb` (scheme admin) and `PTL.ExternalWeb` (participant scheme lists).
- Keep the nested `Tests`/`TestResultItems`/`TestMethodItems`/`CategoryItems` structure as its own explicit request/response shape rather than flattening it into the top-level scheme DTO, so that the test-structure sub-feature (see UI Migration Mapping) has a clean client contract to build against.

---

## Target Project Placement

### PTL.Api

- scheme resource endpoints (get/list/create/update)
- scheme renew/copy endpoints (explicit, separate)
- scheme family history endpoint
- scheme currency pricing endpoints
- postage pricing plan endpoints
- participant scheme-list endpoints (commercial/non-commercial)
- available/currently-participating scheme endpoints (shared with Contract domain — coordinate placement with `docs/migration/contract-migration.md` to avoid duplicate implementations)

### PTL.ApiClient

- scheme DTO contracts and request/response models
- scheme test-structure request/response models
- HTTP wrappers for all Scheme operations listed above

### PTL.Core

- scheme aggregate root logic (identity, `SharedId` family linkage, renewal vs copy semantics)
- scheme validation rules (ported from `AddBusinessRules`: identifier format, required fields, numeric ranges, distribution-month/as-available mutual exclusivity, conditional consent-text requirement)
- scheme test-structure domain model (`Test`, `TestResultItem`, `TestMethodItem`, `CategoryItem`) as its own cohesive sub-model
- postage-plan roll-forward domain rule (name-matched lookup for `YearId + 1`)
- scheme read-only state derivation rule (once the `Readonly` SQL predicate is confirmed)

### PTL.Data

- scheme repository implementation (EF Core + stored procedures, per the existing Customer/Participant/Contract pattern), with the multi-collection transactional save and its ordering constraint made explicit and testable
- scheme currency, test-structure, tabulation-link, and viewer-visibility persistence adapters
- SQL adapters for `spgSchemeBySchemeId`/`spiScheme`/`spuScheme`/`spgSchemeInfoBySchemeId`/`spgSchemeInfoBySharedId`/`spgSchemeInfoByYearId` and the (unconfirmed) child-collection procedures

### PTL.Contracts

- scheme DTOs (list/summary, detail, create/update/renew/copy request)
- scheme test-structure DTOs
- scheme currency and postage-plan DTOs

### PTL.InternalWeb

- scheme list screen (with explicit renew action)
- scheme create/edit screen
- scheme renewal/copy confirmation screens (distinct)
- scheme family history screen
- scheme test-structure editor (scoped as its own sub-feature)

### PTL.ExternalWeb

- "my schemes" screen (commercial / non-commercial lists)

---

## Future Domain Dependencies

### Domains depending on Scheme

- Contract depends on Scheme for postage/packaging pricing rules used in item-pricing computation (see `docs/migration/contract-migration.md`).
- Participant depends on Scheme for `SchemeId` reference and mirrors several `CanEdit*` flags at the participant-selection level.
- Distribution/Results/Tabulation (not yet analysed as its own domain) depends on Scheme's `Tests`/`TestResultItems`/`TestMethodItems`/`Tabulations` structure to define what data is captured and published.
- Viewer visibility/reporting depends on `ViewerScheme` to determine which external viewers can see a scheme's published tabulation.

### Scheme depends on other domains

- SystemSettings for current/next-year defaults on scheme creation.
- PostagePricingPlan for postage cost lookup, including the year-matched roll-forward on renewal.
- (Indirectly) Contract's external ordering workflow ASMX (`AvailableSchemeCollection`/`CurrentlyParticipatingSchemeCollection`) sits adjacent to Scheme in the legacy codebase and should be jointly owned/coordinated with the Contract domain rather than duplicated.

---

## Feature Breakdown

### Phase 1

- Scheme detail read model (`GET /api/schemes/{schemeId}`)
- Scheme list/summary read model (`GET /api/schemes`, by year)
- Scheme family history read model (`GET /api/schemes/families/{sharedId}/history`)
- Scheme currency and postage pricing plan read endpoints

### Phase 2

- Scheme create and edit (`POST`/`PUT`), including all validation rules ported from `AddBusinessRules`
- Scheme renew and copy as two explicit, separately-tested endpoints
- Participant scheme-list endpoints (commercial/non-commercial) for `PTL.ExternalWeb`

### Phase 3

- Test-structure editor (`Tests`/`TestResultItems`/`TestMethodItems`/`CategoryItems`) as its own sub-feature, once the exact child-table schema is confirmed
- Viewer-visibility management (`ViewerScheme`) integrated with the (not yet analysed) Viewer domain
- Available/currently-participating scheme endpoints, coordinated jointly with the Contract domain's external ordering workflow

---

## Risks

### High risk areas

- **Inconsistent authorization within one domain** — `SchemeListCollection.asmx` enforces a role check that four sibling ASMX services in the same domain do not; migrating each endpoint independently without first agreeing a single target policy would mechanically carry this inconsistency into the new system.
- **Transactional multi-collection save with FK ordering** — the documented insert-then-delete-in-reverse-order constraint across `Tests`/`Tabulations` is exactly the kind of implicit business rule that is easy to lose during re-platforming; losing it would risk FK-constraint violations or silent data loss on save.
- **Renewal vs copy conflation** — `RenewScheme` and `CopyScheme` differ only in `YearId`/`SharedId` handling but have very different business meaning; a migration that merges them into one "duplicate" concept would silently break scheme family history.
- **Duplicated admin application** — building for the wrong one of `ProficiencyTestingWeb`/`ProficiencyTestingAdmin` would waste migration effort or miss the actually-live admin surface.
- **Schema drift** — `tblScheme`'s incremental `ALTER TABLE IF NOT EXISTS` history (and the unconfirmed child-table shapes) carries the same risk class as the `spiParticipant` incident already hit in this workspace.

### Operational risk

Scheme's test/result structure (`Tests`/`TestResultItems`/`TestMethodItems`/`CategoryItems`) is consumed by the not-yet-analysed Distribution/Results/Tabulation domain. Migrating Scheme in isolation, without at least a preliminary understanding of how that downstream domain consumes this structure, risks a mismatch discovered only when that domain is analysed/migrated.

### Business risk

Scheme pricing (currency pricing, postage plan) directly feeds Contract's invoicing computation. Any behavioural drift in scheme pricing/postage data during migration could silently produce incorrect contract pricing without an obvious UI symptom, compounding the risk already identified in `docs/migration/contract-migration.md`.

---

## Dependencies

The Scheme migration depends on:

- Confirmation of which internal admin project (`ProficiencyTestingWeb` vs `ProficiencyTestingAdmin`) is canonical, before any UI work is scoped
- Confirmation of the exact child-table schema for `SchemeCurrency`/`Test*`/`Tabulations`/`ViewerScheme`
- Confirmation of the SQL predicate behind `Scheme.IsReadonly`
- A single, explicitly agreed authorization policy for Scheme's participant-facing read endpoints, resolving the current inconsistent enforcement
- Contract domain readiness for the shared `AvailableSchemeCollection`/`CurrentlyParticipatingSchemeCollection` endpoints (see `docs/migration/contract-migration.md`)
- Participant domain readiness for `SchemeId` references and `CanEdit*` flag parity
- A future Distribution/Results/Tabulation domain analysis, to confirm how `Tests`/`Tabulations` are consumed downstream before finalising the target test-structure model

---

## Testing Strategy

### Functional tests

- scheme create/edit flows, including all validation rules (identifier format, required fields, numeric ranges, distribution-month/as-available mutual exclusivity, conditional consent text)
- scheme renewal: `SharedId` preserved, `YearId + 1`, postage plan rolled forward by name (including the case where no same-named plan exists for the new year)
- scheme copy: new `SharedId`, same `YearId`
- scheme list/history projections (all, by year, by shared family id)
- scheme currency and postage pricing plan reads
- participant scheme-list reads (commercial/non-commercial), for an authorised vs unauthorised token

### Integration tests

- transactional save covering `tblScheme` + all four child collections, including a forced failure mid-save to confirm rollback
- explicit test of the insert/delete ordering constraint between `Tests` and `Tabulations` (e.g. removing a test that has an associated tabulation)
- cross-domain: Contract's pricing computation against known scheme postage/currency fixtures
- cross-domain: Participant's `ParticipantScheme` against a known `SchemeId`
- authorization: every Scheme read endpoint enforces the single agreed target policy (no endpoint left unintentionally open)

### Regression tests

- compare legacy scheme admin behaviour (create/edit/renew/copy/list/history) against new system behaviour for the same scenarios
- validate that renewal produces the same family linkage (`SharedId`) and postage-plan resolution as the legacy system for known scheme fixtures
- validate that scheme pricing/postage data feeding Contract produces the same computed contract totals as the legacy system

---

## Development Readiness

### Can development start?

Yes, but only on Phase 1 (read-only scheme detail/list/history/pricing), while several open items from `docs/analysis/scheme-analysis.md` are confirmed.

### What is missing?

- confirmation of the canonical internal admin project (`ProficiencyTestingWeb` vs `ProficiencyTestingAdmin`)
- exact child-table schema for `SchemeCurrency`/`Test*`/`Tabulations`/`ViewerScheme`
- the SQL predicate behind `Scheme.IsReadonly`
- an explicit, agreed authorization policy resolving the current inconsistent enforcement across Scheme's ASMX surface
- current live-schema parameter count for `spiScheme`/`spuScheme` (schema-drift risk)
- at least a preliminary view of how the Distribution/Results/Tabulation domain consumes `Tests`/`Tabulations`, to avoid a structural mismatch later

### Recommended readiness approach

Proceed with Phase 1 (scheme detail, list, history, currency, and postage-plan reads) using the already-understood `spgSchemeBySchemeId`/`spgSchemeInfoBy*` procedures, while the open items above are confirmed. Do not begin write-path migration (`spiScheme`/`spuScheme`, renew/copy, child-collection persistence) until the child-table schema is confirmed and the transactional ordering constraint is understood and test-covered. Do not build participant-facing scheme endpoints in the target system until a single authorization policy has been agreed — do not replicate the legacy inconsistency.

---

## Recommendations

1. Resolve the canonical-admin-app question before scoping any UI work.
   - Confirm with the business/ops team which of `ProficiencyTestingWeb`/`ProficiencyTestingAdmin` is actually deployed and used for Scheme Admin.

2. Decide one authorization policy for Scheme's participant-facing endpoints, then apply it everywhere.
   - Do not migrate `Scheme.asmx`, `SchemeListCollection.asmx`, `SchemeCurrency.asmx`, `PostagePricingPlan.asmx`, `AvailableSchemeCollection.asmx`, and `CurrentlyParticipatingSchemeCollection.asmx` independently of each other with respect to authorization — treat this as one decision applied consistently.

3. Model renewal and copy as two first-class, separately-named operations throughout the stack (API, client, UI).
   - Never introduce a single "duplicate scheme" concept with a mode flag; the business meaning (family continuation vs unrelated duplicate) is materially different.

4. Preserve the transactional, ordering-constrained multi-collection save exactly, or replace it only after full understanding.
   - This is the highest-risk piece of write-path logic in the domain; do not attempt to "simplify" it without first confirming the FK relationships that necessitate the ordering.

5. Verify the live schema before wrapping `spiScheme`/`spuScheme` and any child-collection procedures.
   - Apply the same "confirm parameter count/columns against the live database before wrapping" discipline already learned from the `spiParticipant` incident.

6. Scope the test-structure editor as its own feature, not a tab on the main scheme form.
   - Given its three-level nested-aggregate shape and independent audit behaviour, it deserves its own design pass rather than being bundled into the top-level scheme UI/API.

7. Coordinate the shared Contract-adjacent ASMX (`AvailableSchemeCollection`/`CurrentlyParticipatingSchemeCollection`) with the Contract migration team.
   - Avoid two independent, divergent implementations of the same endpoints from the Scheme and Contract migration efforts.

---

## Summary

The Scheme domain is the catalogue, pricing-rule, and test/result-structure source that both Contract and Participant depend on, and the structural foundation for the not-yet-analysed Distribution/Results/Tabulation domain. It is the most structurally complex domain migrated in this series so far, combining an audited, multi-child-collection aggregate with a non-obvious transactional ordering constraint, two easily-conflated clone operations, and — uniquely among the domains analysed — an authorization posture that is inconsistent even within the domain's own ASMX surface rather than uniformly present or absent. The migration should preserve the domain's structural integrity (transactional save, FK ordering, renewal-vs-copy semantics) exactly as understood today, while explicitly and consistently closing the authorization gap rather than mechanically reproducing the legacy inconsistency endpoint-by-endpoint.
