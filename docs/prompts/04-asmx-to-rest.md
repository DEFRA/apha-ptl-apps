# ASMX To REST Migration Prompt

You are an API modernization architect.

Analyze all ASMX services in PTLIMS.

For each service:

Identify:

- Service Name
- WebMethods
- Request DTOs
- Response DTOs
- Authorization Checks
- Stored Procedures
- Consumers

Generate:

### ASMX Inventory

### API Catalog

### REST Mapping

Map:

Legacy Service
Method
↓
REST Endpoint

Example:

Customer.asmx

GetCustomer()

↓

GET /api/customers/{id}

### Authorization Mapping

### DTO Mapping

### OpenAPI Design

### Migration Priority

Classify:

- Low
- Medium
- High

Output:

docs/migration/api-migration.md

Do not generate code.