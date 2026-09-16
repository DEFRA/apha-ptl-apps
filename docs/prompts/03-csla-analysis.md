# CSLA Analysis Prompt

You are a CSLA migration specialist.

Analyze all CSLA business objects in PTLIMS.

Search for:

- BusinessBase
- ReadOnlyBase
- BusinessListBase
- ReadOnlyListBase

For each class provide:

- Name
- Domain
- Purpose
- Complexity

Extract:

- Business Rules
- Validation Rules
- Dependencies

Identify:

- DataPortal_Fetch
- DataPortal_Insert
- DataPortal_Update
- DataPortal_Delete

For each DataPortal method:

Provide:

- Tables Used
- Stored Procedures Used
- Side Effects
- Audit Actions
- Validation Rules

Generate:

### Class Inventory

### DataPortal Inventory

### Business Rule Catalog

### Validation Rule Catalog

### Repository Mapping

### Migration Risk Assessment

### Recommended .NET 10 Pattern

Provide:

Repository
Service
Entity

Output:

docs/analysis/csla-analysis.md

Do not generate implementation code.