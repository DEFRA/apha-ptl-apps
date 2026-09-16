# PTLIMS ASMX to REST Migration Analysis

**Scope:** PTLIMS legacy .NET Framework 4.8 service layer under `proficiency-testing/ProficiencyTestingWebServices`  
**Source repository:** `c:\Users\ac000232\source\repos\proficiency-testing`  
**Analysis date:** 2026-09-14

---

## Executive Summary

PTLIMS exposes a broad SOAP-based service layer through classic ASMX web services. The services are not a modern API layer; they are a thin façade over CSLA business objects, custom identity/authorization checks, and direct SQL-backed DataPortal methods.

The architecture pattern is consistent across the codebase:

- Each ASMX service calls `UserService.Service.GetUserByTokenId(tokenId)`
- Many methods call `AuthoriseUser(tokenId, Roles.X)` before proceeding
- Some authorization checks are commented out in implementation, which suggests inconsistent enforcement or a transitional state
- The service returns serializable DTO-like `Structure` classes instead of domain entities
- The business logic itself is implemented in the CSLA `PtWebServicesBusinessObjects` layer, not in the web service boundary

This means the REST migration is best treated as a domain-service extraction rather than a pure protocol rewrite. The biggest modernization gains are to:

1. Replace ASMX SOAP contracts with HTTP REST endpoints
2. Move authorization into ASP.NET Core authentication and policy-based authorization
3. Convert legacy CSLA DTO structures to proper JSON request/response models
4. Consolidate repeated lookup and list operations behind clean query handlers
5. Separate read-only catalog endpoints from workflow-heavy mutation endpoints

---

## ASMX Inventory

The service inventory below is derived from the files under `ProficiencyTestingWebServices` and the `WebMethod` signatures in each service class.

| Service | File | WebMethods | Request DTOs | Response DTOs | Authorization pattern | Likely consumers |
|---|---|---|---|---|---|---|
| AssessmentResultCollection | `AssessmentResultCollection.asmx.vb` | `GetAssessmentResultCollection(tokenId, tabulationId, testId)` | `tabulationId`, `testId`, `tokenId` | `AssessmentResult` | `AuthoriseUser(tokenId, Roles.Viewer)` | viewer/tabulation screens |
| AvailableSchemeCollection | `AvailableSchemeCollection.asmx.vb` | `FetchAvailableSchemes(tokenId, yearId)` | `yearId` | `Collection(Of AvailableScheme)` | user token lookup; role check commented out | customer/participant contract setup |
| CountryCollection | `CountryCollection.asmx.vb` | `GetCountryCollection()` | none | `CountryCollection` | no explicit role check | lookup dropdowns |
| CurrentDistributionCollection | `CurrentDistributionCollection.vb` | `GetCurrentDistributionCollection(tokenId)` | `tokenId` | `Collection(Of DistributionCurrent)` | `AuthoriseUser(tokenId, Roles.Participant)` | participant dashboard |
| CurrentlyParticipatingSchemeCollection | `CurrentlyParticipatingSchemeCollection.asmx.vb` | `FetchCurrentlyParticipatingSchemes(tokenId, yearId, participatingYearId, participantId)` | `yearId`, `participatingYearId`, `participantId` | `Collection(Of CurrentlyParticipatingScheme)` | role check commented out | contract participation screens |
| Customer | `Customer.asmx.vb` | `GetCustomer(tokenId, customerId)` | `customerId` | `Customer` | token lookup; role check commented out | customer account screens |
| DistributionForComment | `DistributionForComment.vb` | `GetDistributionForComment(tokenId, monthlyDistributionSchemeId)`, `UpdateTestConsultantComments(tokenId, monthlyDistributionSchemeId, testConsultantComments, isFinalComment)` | comments payload and distribution id | `MonthlyDistributionScheme`, `Boolean` | `AuthoriseUser(tokenId, Roles.TestConsultant)` | test consultant comments workflow |
| DistributionForCommentCollection | `DistributionForCommentCollection.vb` | `GetDistributionForCommentCollection(tokenId)` | `tokenId` | `Collection(Of DistributionForComment)` | `AuthoriseUser(tokenId, Roles.TestConsultant)` | consultant distribution list |
| MainPageMessage | `MainPageMessage.vb` | `GetMainPageMessage(tokenId)` | `tokenId` | `MainPageMessage` | token lookup only | homepage/banner content |
| Participant | `Participant.asmx.vb` | `GetParticipant(tokenId)` | `tokenId` | `Participant` | token lookup; role check commented out | participant profile screens |
| ParticipantCollection | `ParticipantCollection.asmx.vb` | `FetchParticipantCollection(tokenId, customerId)` | `customerId` | `Collection(Of Participant)` | role check commented out | customer/participant management |
| PastDistributionCollection | `PastDistributionCollection.vb` | `GetPastDistributionCollection(tokenId)` | `tokenId` | `Collection(Of DistributionPast)` | `AuthoriseUser(tokenId, Roles.Participant)` | participant history screens |
| PendingContractOrder | `PendingContractOrder.asmx.vb` | `GetPendingContractOrder(tokenId, customerId, isSubmitted, yearId)`, `InsertPendingContractOrder(...)`, `UpdatePendingContractOrder(...)` | `PendingContractOrder` payload | `PendingContractOrder`, `Boolean` | role check commented out | online contract ordering workflow |
| PendingCustomerUpdate | `PendingCustomerUpdate.asmx.vb` | `GetPendingCustomerUpdate(...)`, `UpdatePendingCustomerUpdate(...)`, `InsertPendingCustomerUpdate(...)` | `PendingCustomerUpdate` payload | `PendingCustomerUpdate`, `Boolean` | role check commented out | customer change approval workflow |
| PendingParticipantScheme | `PendingParticipantScheme.asmx.vb` | `GetPendingParticipantScheme(...)`, `InsertPendingParticipantScheme(...)`, `UpdatePendingParticipantScheme(...)`, `DeletePendingParticipantScheme(...)` | `PendingParticipantScheme` payload | `PendingParticipantScheme`, `Boolean` | token lookup only | participant scheme selection workflow |
| PendingParticipantSchemeCollection | `PendingParticipantSchemeCollection.asmx.vb` | `FetchPendingParticipantSchemeCollection(tokenId, contractId)` | `contractId` | `Collection(Of PendingParticipantScheme)` | role check commented out | contract details screens |
| PendingParticipantUpdate | `PendingParticipantUpdate.asmx.vb` | `GetPendingParticipantUpdate(...)`, `UpdatePendingParticipantUpdate(...)`, `InsertPendingParticipantUpdate(...)` | `PendingParticipantUpdate` payload | `PendingParticipantUpdate`, `Boolean` | role check commented out | participant change approval workflow |
| PostagePricingPlan | `PostagePricingPlan.asmx.vb` | `GetPostagePricingPlan(tokenId, postagePricingPlanId)`, `GetAllPostagePricingPlans(tokenId)` | `postagePricingPlanId` | `PostagePricingPlan`, `List(Of PostagePricingPlan)` | token lookup; role check commented out | pricing screens |
| ResultsEntry | `ResultsEntry.vb` | `GetResultsEntry(tokenId, monthlyDistributionSchemeId)`, `UpdateResults(...)` | result payloads and distribution id | `ResultsEntry`, `Boolean` | `AuthoriseUser(tokenId, Roles.Participant)` | participant results entry |
| Scheme | `Scheme.asmx.vb` | `GetScheme(tokenId, schemeId)`, `GetFullSchemeInformationListCollection(tokenId, yearId)` | `schemeId`, `yearId` | `Scheme`, `Collection(Of Scheme)` | token lookup; role check commented out | scheme administration and lookup |
| SchemeCurrency | `SchemeCurrency.asmx.vb` | `GetSchemeCurrency(tokenId, schemeId, currencyId)`, `GetAllSchemeCurrency(tokenId)` | `schemeId`, `currencyId` | `SchemeCurrency`, `List(Of SchemeCurrency)` | token lookup; role check commented out | pricing/currency screens |
| SchemeListCollection | `SchemeListCollection.asmx.vb` | `GetSchemeListCollection(tokenId, listType)`, `GetSchemeInfoListCollection(tokenId, yearId)` | `listType`, `yearId` | `Collection(Of SchemeList)`, `Collection(Of SchemeInfoList)` | `AuthoriseUser(tokenId, Roles.Participant)` | participant scheme list dashboards |
| SystemInformation | `SystemInformation.asmx.vb` | `GetSystemInformation(tokenId)` | `tokenId` | `SystemInformation` | token lookup | environment metadata |
| SystemSettings | `SystemSettings.asmx.vb` | `GetSystemSettings(tokenId)` | `tokenId` | `SystemSettings` | token lookup | configuration screens |
| Tabulation | `Tabulation.vb` | `GetTabulation(tokenId, tabulationId)` | `tabulationId` | `Tabulation` | `AuthoriseUser(tokenId, Roles.Viewer)` | published results viewer |
| TabulationCollection | `TabulationCollection.vb` | `GetTabulationCollection(tokenId)` | `tokenId` | `Collection(Of Tabulation)` | `AuthoriseUser(tokenId, Roles.Viewer)` | viewer summary screens |
| TestConsultant | `TestConsultant.asmx.vb` | `GetTestConsultant(tokenId)` | `tokenId` | `TestConsultant` | token lookup | consultant profile screens |
| Viewer | `Viewer.vb` | `GetViewer(tokenId)` | `tokenId` | `Viewer` | token lookup | viewer/authentication screens |

### Architectural pattern

Most services follow a predictable pattern:

- Build a `UserService.Service()` client
- Resolve the current user via `GetUserByTokenId(tokenId)`
- Optionally check `AuthoriseUser(tokenId, Roles.X)`
- Fetch a CSLA business object from `PtWebServicesBusinessObjects`
- Map the resulting BO into a serializable `Structure` DTO to be returned over SOAP
- For write operations, mutate the CSLA object, then call `Save()` or `Delete()`

This is an ideal candidate for conversion to REST endpoints, but the underlying CSLA object model should not be serialized directly into HTTP responses.

---

## API Catalog

### Read operations

| Capability | Representative legacy service(s) | Modern REST intent |
|---|---|---|
| Customer profile | `Customer.GetCustomer`, `Participant.GetParticipant` | `GET /api/customers/{id}`, `GET /api/participants/me` |
| Scheme lookup | `Scheme.GetScheme`, `SchemeListCollection.GetSchemeInfoListCollection`, `AvailableSchemeCollection.FetchAvailableSchemes` | `GET /api/schemes/{id}`, `GET /api/schemes?year={year}` |
| Distribution lists | `CurrentDistributionCollection`, `PastDistributionCollection`, `DistributionForCommentCollection`, `TabulationCollection` | `GET /api/distributions/current`, `GET /api/distributions/history`, `GET /api/distributions/{id}/comments`, `GET /api/tabulations` |
| Results entry | `ResultsEntry.GetResultsEntry`, `AssessmentResultCollection.GetAssessmentResultCollection` | `GET /api/distributions/{distributionId}/results`, `GET /api/tabulations/{id}/assessment-results` |
| Customer/participant workflow | `PendingCustomerUpdate`, `PendingParticipantUpdate`, `PendingContractOrder`, `PendingParticipantScheme` | `GET /api/workflows/{type}/{id}`, `POST /api/workflows/{type}` |
| System metadata | `SystemInformation.GetSystemInformation`, `SystemSettings.GetSystemSettings`, `CountryCollection.GetCountryCollection` | `GET /api/system/info`, `GET /api/system/settings`, `GET /api/lookups/countries` |

### Write operations

| Workflow | Legacy service(s) | Modern REST intent |
|---|---|---|
| Update participant/customer details | `PendingCustomerUpdate.UpdatePendingCustomerUpdate`, `PendingParticipantUpdate.UpdatePendingParticipantUpdate` | `PATCH /api/customers/{id}/proposed-updates`, `PATCH /api/participants/{id}/proposed-updates` |
| Submit contract order | `PendingContractOrder.InsertPendingContractOrder`, `UpdatePendingContractOrder` | `POST /api/contracts/pending`, `PUT /api/contracts/pending/{id}` |
| Select scheme membership | `PendingParticipantScheme.InsertPendingParticipantScheme`, `UpdatePendingParticipantScheme`, `DeletePendingParticipantScheme` | `POST /api/contracts/{contractId}/scheme-choices`, `PATCH /api/contracts/{contractId}/scheme-choices/{id}`, `DELETE /api/contracts/{contractId}/scheme-choices/{id}` |
| Results submission | `ResultsEntry.UpdateResults` | `PUT /api/distributions/{distributionId}/results` |
| Consultant comments | `DistributionForComment.UpdateTestConsultantComments` | `PUT /api/distributions/{distributionId}/comments` |

---

## REST Mapping

Legacy Service → REST Endpoint

| Legacy Service | Legacy Method | REST Endpoint | Notes |
|---|---|---|---|
| `Customer` | `GetCustomer()` | `GET /api/customers/{customerId}` | Fetch customer profile |
| `Participant` | `GetParticipant()` | `GET /api/participants/me` | Current participant profile from token |
| `ParticipantCollection` | `FetchParticipantCollection()` | `GET /api/customers/{customerId}/participants` | List members under a customer |
| `Scheme` | `GetScheme()` | `GET /api/schemes/{schemeId}` | Detail object |
| `Scheme` | `GetFullSchemeInformationListCollection()` | `GET /api/schemes?year={yearId}` | List of scheme metadata by year |
| `SchemeListCollection` | `GetSchemeListCollection()` | `GET /api/participants/me/scheme-lists?listType={type}` | Filtered list of scheme membership/availability |
| `SchemeListCollection` | `GetSchemeInfoListCollection()` | `GET /api/schemes/info?year={yearId}` | Scheme info summaries |
| `AvailableSchemeCollection` | `FetchAvailableSchemes()` | `GET /api/contracts/{yearId}/available-schemes` | Available selection list |
| `CurrentDistributionCollection` | `GetCurrentDistributionCollection()` | `GET /api/participants/me/distributions/current` | Current open distributions |
| `PastDistributionCollection` | `GetPastDistributionCollection()` | `GET /api/participants/me/distributions/history` | Prior distributions |
| `ResultsEntry` | `GetResultsEntry()` | `GET /api/distributions/{distributionId}/results` | Read result entry screen data |
| `ResultsEntry` | `UpdateResults()` | `PUT /api/distributions/{distributionId}/results` | Save and submit results |
| `DistributionForComment` | `GetDistributionForComment()` | `GET /api/distributions/{distributionId}/comments` | Read consultant comments |
| `DistributionForComment` | `UpdateTestConsultantComments()` | `PUT /api/distributions/{distributionId}/comments` | Save consultant comments |
| `Tabulation` | `GetTabulation()` | `GET /api/tabulations/{tabulationId}` | Published tabulation detail |
| `TabulationCollection` | `GetTabulationCollection()` | `GET /api/tabulations` | List of published tabulations |
| `AssessmentResultCollection` | `GetAssessmentResultCollection()` | `GET /api/tabulations/{tabulationId}/tests/{testId}/assessment-results` | Assessment scoring detail |
| `PendingCustomerUpdate` | `GetPendingCustomerUpdate()` | `GET /api/customers/{customerId}/pending-updates` | Read pending change request |
| `PendingCustomerUpdate` | `InsertPendingCustomerUpdate()` | `POST /api/customers/{customerId}/pending-updates` | Create pending update |
| `PendingCustomerUpdate` | `UpdatePendingCustomerUpdate()` | `PUT /api/customers/{customerId}/pending-updates/{id}` | Update pending update |
| `PendingParticipantUpdate` | `GetPendingParticipantUpdate()` | `GET /api/participants/{participantId}/pending-updates` | Read participant change request |
| `PendingParticipantUpdate` | `InsertPendingParticipantUpdate()` | `POST /api/participants/{participantId}/pending-updates` | Create participant update |
| `PendingParticipantUpdate` | `UpdatePendingParticipantUpdate()` | `PUT /api/participants/{participantId}/pending-updates/{id}` | Update participant update |
| `PendingContractOrder` | `GetPendingContractOrder()` | `GET /api/customers/{customerId}/contracts/pending?year={yearId}` | Contract order workflow |
| `PendingContractOrder` | `InsertPendingContractOrder()` | `POST /api/customers/{customerId}/contracts/pending` | Create pending order |
| `PendingContractOrder` | `UpdatePendingContractOrder()` | `PUT /api/customers/{customerId}/contracts/pending/{id}` | Update pending order |
| `PendingParticipantScheme` | `GetPendingParticipantScheme()` | `GET /api/contracts/{contractId}/scheme-choices/{id}` | Read a chosen scheme |
| `PendingParticipantScheme` | `InsertPendingParticipantScheme()` | `POST /api/contracts/{contractId}/scheme-choices` | Add scheme to contract |
| `PendingParticipantScheme` | `UpdatePendingParticipantScheme()` | `PUT /api/contracts/{contractId}/scheme-choices/{id}` | Update scheme selection |
| `PendingParticipantScheme` | `DeletePendingParticipantScheme()` | `DELETE /api/contracts/{contractId}/scheme-choices/{id}` | Remove scheme selection |
| `PostagePricingPlan` | `GetPostagePricingPlan()` | `GET /api/postage-pricing-plans/{planId}` | Lookup pricing plan |
| `PostagePricingPlan` | `GetAllPostagePricingPlans()` | `GET /api/postage-pricing-plans` | All pricing plans |
| `SchemeCurrency` | `GetSchemeCurrency()` | `GET /api/schemes/{schemeId}/currencies/{currencyId}` | Scheme-specific currency |
| `SchemeCurrency` | `GetAllSchemeCurrency()` | `GET /api/schemes/{schemeId}/currencies` | Currency list |
| `SystemInformation` | `GetSystemInformation()` | `GET /api/system/info` | environment metadata |
| `SystemSettings` | `GetSystemSettings()` | `GET /api/system/settings` | system settings |
| `Viewer` | `GetViewer()` | `GET /api/viewers/me` | Viewer metadata |
| `TestConsultant` | `GetTestConsultant()` | `GET /api/test-consultants/me` | Consultant profile |

---

## Authorization Mapping

The pattern in the legacy services is consistent with a centralized external identity service (`UserService`) and a custom role model.

### Legacy authorization model

`UserService.Service` exposes:

- `GetUserByTokenId(tokenId)`
- `AuthoriseUser(tokenId, roleId)`

The service layer calls these methods to resolve the current user identity and then check membership against a role such as `Roles.Participant`, `Roles.Viewer`, or `Roles.TestConsultant`.

### Observed role-based patterns

| Legacy role | Used by service(s) | Modern equivalent |
|---|---|---|
| `Participant` | `ResultsEntry`, `CurrentDistributionCollection`, `SchemeListCollection`, `PastDistributionCollection`, `CurrentDistribution` workflows | policy `ParticipantAccess` |
| `Viewer` | `Tabulation`, `TabulationCollection`, `AssessmentResultCollection` | policy `ViewerAccess` |
| `TestConsultant` | `DistributionForComment`, `DistributionForCommentCollection` | policy `TestConsultantAccess` |
| `Customer` | some customer operations are authored with `Roles.Customer` but commented out | policy `CustomerAccess` |

### Important migration note

Several services contain commented-out authorization checks, and therefore the real enforcement is inconsistent across the service layer. The safest REST migration should make authorization explicit and policy-based in ASP.NET Core rather than relying on legacy token validation plus role IDs.

Recommended modern mapping:

- `tokenId` → `userId` or `sub` claim from Entra ID / OIDC
- `Roles.Participant` → claim-based policy `participants`
- `Roles.Viewer` → claim-based policy `viewers`
- `Roles.TestConsultant` → claim-based policy `test-consultants`
- `UserService` identity validation → ASP.NET Core authentication middleware and `IAuthorizationService`

---

## DTO Mapping

The ASMX layer does not expose the CSLA domain objects directly. Instead, each `Structure` is a flat, serializable projection that is manually mapped from a CSLA BO.

### Example 1: Customer

Legacy SOAP response `Customer`:

- `CustomerId`
- `QalNumber`
- `RegisteredFileNumber`
- `Name`
- `PreviousName`
- `CustomerTypeID`
- `Address1..Address5`
- `CountryId`
- `Invoice*` and `Contact*` fields
- `IsActive`, `CanOrderOnline`

Modern REST DTO:

```json
{
  "customerId": "uuid",
  "qalNumber": "string",
  "registeredFileNumber": "string",
  "name": "string",
  "previousName": "string",
  "contactName": "string",
  "organisation": "string",
  "address": { "line1": "string", "line2": "string", "line3": "string", "line4": "string", "line5": "string", "countryId": "uuid" },
  "invoiceAddress": { "...": "..." },
  "isActive": true,
  "canOrderOnline": true
}
```

### Example 2: Results entry

Legacy SOAP types:

- `ResultsEntry`
- `Test`
- `TestResultItem`
- `TestMethodItem`
- `Sample`
- `SampleTest`
- `SampleResultItem`

All are structured as nested collections and flat result payloads with custom formatting metadata.

Modern REST DTO should be normalized to:

```json
{
  "actualResultsId": "uuid",
  "distributionId": "uuid",
  "distributionReference": "string",
  "schemeName": "string",
  "tests": [
    {
      "id": "uuid",
      "name": "string",
      "methodItems": [ { "id": "uuid", "name": "string", "result": "string" } ],
      "resultItems": [ { "id": "uuid", "name": "string", "format": "string" } ]
    }
  ],
  "samples": [ { "id": "uuid", "number": "string", "tests": [ { ... } ] } ]
}
```

### Example 3: Pending participant scheme

The legacy structure includes a long list of month flags (`DistributionMonthJan`..`DistributionMonthDec`) and content flags (`CanEditJan`..`CanEditDec`) with many duplicated properties.

Modern REST mapping should collapse these into a simpler nested structure such as:

```json
{
  "id": "uuid",
  "contractId": "uuid",
  "participantId": "uuid",
  "schemeId": "uuid",
  "distributionMonths": [ { "month": "Jan", "selected": true, "editable": true } ],
  "nonUk": false,
  "isSelected": true,
  "dataConsentGiven": true
}
```

### DTO migration principles

- Flattened SOAP-style structures should become domain-shaped JSON resources
- Keep read models and command models separate
- Do not expose internal CSLA property names as public API contract names
- Use explicit request DTOs for writes (`CreatePendingCustomerUpdateRequest`, `UpdateResultsRequest`) rather than passing large parameter arrays

---

## OpenAPI Design

A suitable REST contract for the PTLIMS domain would be structured around the core resources below.

```yaml
openapi: 3.0.3
info:
  title: PTLIMS API
  version: 1.0.0
paths:
  /api/customers/{customerId}:
    get:
      summary: Get customer profile
      parameters:
        - in: path
          name: customerId
          required: true
          schema:
            type: string
            format: uuid
      responses:
        '200':
          description: Customer detail

  /api/participants/me:
    get:
      summary: Get current participant profile
      responses:
        '200':
          description: Participant detail

  /api/schemes:
    get:
      summary: List schemes by year
      parameters:
        - in: query
          name: year
          schema:
            type: integer
      responses:
        '200':
          description: Scheme collection

  /api/distributions/{distributionId}/results:
    get:
      summary: Fetch results entry for a distribution
    put:
      summary: Save or submit results

  /api/distributions/{distributionId}/comments:
    get:
      summary: Get consultant comments
    put:
      summary: Update consultant comments

  /api/contracts/{contractId}/scheme-choices:
    get:
      summary: Get scheme choices for contract
    post:
      summary: Add scheme choice

  /api/contracts/{contractId}/scheme-choices/{choiceId}:
    put:
      summary: Update scheme choice
    delete:
      summary: Remove scheme choice

  /api/tabulations:
    get:
      summary: List tabulations
```

### API conventions

- Use plural nouns for resources: `customers`, `participants`, `schemes`, `distributions`
- Use `GET` for reads, `POST` for create, `PUT`/`PATCH` for update, `DELETE` for removal
- Prefer the authenticated user context for `me` endpoints
- Use `202` or `200` statuses consistently and avoid exposing legacy SOAP fault patterns directly

---

## Migration Priority

### High priority

These services implement the core domain workflows and transactional data entry and should be addressed first.

- `ResultsEntry` — participant data-entry workflow and business validation
- `Scheme` — core scheme and year configuration
- `Customer` and `Participant` — core identity/profile entities
- `PendingParticipantScheme` and `PendingContractOrder` — contract and scheme assignment workflow
- `DistributionForComment` — test consultant comments and sign-off process

### Medium priority

These services support administrative or dashboard operations and are important but less critical for transaction throughput.

- `CurrentDistributionCollection`, `PastDistributionCollection`, `DistributionForCommentCollection`
- `SchemeListCollection`, `AvailableSchemeCollection`, `CurrentlyParticipatingSchemeCollection`
- `PendingCustomerUpdate`, `PendingParticipantUpdate`
- `Tabulation`, `TabulationCollection`, `AssessmentResultCollection`
- `PostagePricingPlan`, `SchemeCurrency`

### Low priority

These are primarily metadata, system, or lookup services and can be migrated once the core workflow endpoints are stabilized.

- `CountryCollection`
- `MainPageMessage`
- `SystemInformation`
- `SystemSettings`
- `TestConsultant`
- `Viewer`

### Recommended migration order

1. `ResultsEntry`
2. `Customer` / `Participant`
3. `Scheme` / `SchemeListCollection`
4. `PendingContractOrder` / `PendingParticipantScheme`
5. `DistributionForComment`
6. `Tabulation` / `AssessmentResultCollection`
7. `System` and lookup endpoints

---

## Summary Assessment

PTLIMS is a classic, domain-heavy ASMX service layer built over CSLA and stored procedures. It is structurally consistent and well understood, but it is not suitable for direct translation into a modern REST API without first separating:

- read models from write models
- authorization policies from service-layer checks
- CSLA business-object mappings from public API DTOs
- workflow state transitions from pure data access

The most defensible migration strategy is to convert the service layer to ASP.NET Core API endpoints organized around resource nouns and domain workflows, while retaining the established PTLIMS business rules in a modern domain service layer or application service layer.

---

## Evidence base used for this analysis

- `proficiency-testing/ProficiencyTestingWebServices/*.vb`
- `proficiency-testing/PtaBusinessObjects/Business Objects/...`
- `proficiency-testing/ProficiencyTestingWebServices/Web References/UserService/Reference.vb`

This summary intentionally avoids generating code and is limited to architecture analysis and migration planning.
