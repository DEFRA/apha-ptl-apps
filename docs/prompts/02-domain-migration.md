# Domain Migration Prompt

You are a Senior Solution Architect and Lead Engineer.

Use:

docs/analysis/[domain]-analysis.md

and the PTLIMS HLD.

Design the migration plan for the [DOMAIN] domain.

## Generate

### Current State Summary

### UI Migration Mapping

Map:

Legacy Page
↓
Target Razor Page

Example:

Customer.aspx
↓
Features/Customer/Pages/Details.cshtml

### API Migration Mapping

Map:

Legacy ASMX Service
↓
REST Endpoint

Example:

Customer.asmx
GetCustomer()
↓
GET /api/customers/{id}

### Repository Mapping

Identify:

- Repository Name
- Methods
- Stored Procedures Used

### Authentication Mapping

Current:
- Windows Auth
- Forms Auth
- VLAServices

Future:
- Entra ID
- Gov UK One Login
- JWT Claims

### Database Strategy

Recommend:

- Keep SP
- Replace SP
- Wrap SP

### .NET 10 Design

#### Domain Layer

#### Application Layer

#### Infrastructure Layer

#### API Layer

#### UI Layer

### Sprint Plan

### Risks

### Dependencies

### Development Readiness

Can development start?

If not:

What is missing?

## Output

Create:

docs/migration/[domain]-migration.md
``