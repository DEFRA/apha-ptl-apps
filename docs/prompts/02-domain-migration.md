# Domain Migration Prompt

You are a Principal Solution Architect and .NET Modernization Lead.

Use:

- docs/analysis/[domain]-analysis.md
- docs/analysis/csla-analysis.md
- docs/migration/api-migration.md
- docs/analysis/authentication-analysis.md
- docs/source/PTLIMS-HLD-v0.3.docx
- docs/source/PTLIMS-KT.md


Design the TO-BE migration plan for the [DOMAIN] domain.

Do NOT write implementation code.

---

## Generate

### Current State Summary

### Domain Boundaries

Define:

What belongs to this domain?

What does NOT belong to this domain?

Example:

Customer Domain

Contains:
- Customer
- CustomerStatus
- CustomerType

Does Not Contain:
- Participant
- Scheme
- Distribution

### UI Migration Mapping

Legacy Page
↓
Target Page

Map all pages.

### API Migration Mapping

Legacy ASMX
↓
REST Endpoint

Map all methods.

### Repository Mapping

For every repository provide:

- Repository Name
- Methods
- Stored Procedures
- Database Dependencies

### Authentication Mapping

Current

Future

Impact

### Database Strategy

Classify:

- Keep SP
- Wrap SP
- Replace SP

with rationale.

### API Client Design

For PTL.ApiClient.

### Target Project Placement

Identify what goes into:

- PTL.Api
- PTL.ApiClient
- PTL.Core
- PTL.Data
- PTL.Contracts
- PTL.InternalWeb
- PTL.ExternalWeb

### Future Domain Dependencies

Identify:

Domains depending on this one.

Example:

Participant depends on Customer.

Contract depends on Customer.

### Feature Breakdown

Separate:

Phase 1
Phase 2
Phase 3

Example:

Phase 1
- List
- Details

Phase 2
- Create
- Edit

Phase 3
- Workflow
- Approvals

### Risks

### Dependencies

### Testing Strategy

### Development Readiness

Can development start?

What is missing?

### Recommendations

---

## Output

Create:

docs/migration/[domain]-migration.md

Do not generate code.