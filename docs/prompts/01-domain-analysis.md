# Domain Analysis Prompt

You are a Senior .NET Modernization Architect, Solution Analyst, and Migration Lead.

Analyze the [DOMAIN] domain across the entire PTLIMS legacy solution.

Use:

- Source code
- Database project
- WebForms pages
- ASMX services
- CSLA business objects
- Configuration
- HLD
- KT documentation

Search all relevant projects:

- ASP.NET Web Forms (.aspx, .ascx)
- Code-behind (.vb, .cs)
- ASMX Services
- CSLA Business Objects
- Class Libraries
- DTOs
- Structures
- Database Project
- Tables
- Views
- Functions
- Stored Procedures
- Reports
- Unit Tests

## Generate

### Executive Summary

### User Journeys

### Page Inventory

Include:

- Page
- Role
- Purpose
- Dependencies
- Complexity

### Service Inventory

Include:

- Service
- WebMethod
- Inputs
- Outputs
- Consumers

### Business Objects

Extract:

- BusinessBase
- ReadOnlyBase
- BusinessListBase
- DataPortal_Fetch
- DataPortal_Insert
- DataPortal_Update
- DataPortal_Delete

### DTO Inventory

### Database Mapping

Identify:

- Tables
- Views
- Functions
- Stored Procedures
- TVPs

### Validation Rules

### Business Rules

### Security Analysis

### Dependency Analysis

Generate Mermaid diagrams.

### Migration Complexity Assessment

### Open Questions

For any missing information write:

[NEEDS INVESTIGATION]

## Output

Create:

docs/analysis/[domain]-analysis.md

Do not generate code.

Provide source file references for all findings.