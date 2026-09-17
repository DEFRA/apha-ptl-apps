# Domain Analysis Prompt

You are a Principal Solution Architect, Domain Analyst, and Legacy Modernization Specialist.

Analyze the [DOMAIN] domain across the entire PTLIMS legacy solution(C:\Users\ac000232\source\repos\proficiency-testing).

This is an AS-IS analysis only.

Do NOT generate:

- .NET 10 design
- Migration architecture
- Repository design
- CQRS
- DDD
- Future implementation plans

Use:

- Source code
- Database project
- WebForms pages
- ASMX services
- CSLA business objects
- Configuration
- docs/source/PTLIMS-HLD-v0.3.docx
- docs/source/PTLIMS-KT.md
- Existing analysis documents

Search:

- ASP.NET Web Forms (.aspx, .ascx)
- Code-behind (.vb, .cs)
- ASMX services
- CSLA business objects
- DTOs
- Database project
- Tables
- Views
- Functions
- Stored procedures
- Reports
- Unit tests

---

## Generate

### Executive Summary

### Business Purpose

What business problem does this domain solve?

### User Journeys

For each journey identify:

- User Role
- Entry Point
- Exit Point
- Workflow
- Dependencies

### Page Inventory

For each page:

- Page
- Role
- Purpose
- Dependencies
- Complexity

### Service Inventory

For each ASMX service:

- Service
- WebMethod
- Inputs
- Outputs
- Consumers

### Business Objects

Identify:

- BusinessBase
- ReadOnlyBase
- BusinessListBase
- ReadOnlyListBase

Extract:

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

Provide:

Page
↓
Service
↓
Business Object
↓
Stored Procedure
↓
Table

mapping.

### Validation Rules

Classify:

- Critical
- Important
- Optional

### Business Rules

Classify:

- Critical
- Important
- Optional

### Security Analysis

Identify:

- Authentication dependencies
- Authorization dependencies
- Role checks
- Ownership checks

### Cross-Domain Dependencies

Identify:

- Upstream Domains
- Downstream Domains
- Shared Domains

Example:

Customer
↓
Participant
↓
Contract

Explain:

- Dependency reason
- Impact of changes

Generate Mermaid diagram.

### Workflow Boundaries

Identify:

Entry Points

Exit Points

Example:

Customer
↓
Participant Creation

Customer
↓
Pending Customer Update

### Stored Procedure Dependency Matrix

For every major SP:

Page
↓
Service
↓
Business Object
↓
Stored Procedure
↓
Table

### Migration Impact Assessment

If this domain changes:

What domains are affected?

Classify:

- High Impact
- Medium Impact
- Low Impact

### Complexity Assessment

### Open Questions

Use:

[NEEDS INVESTIGATION]

for unknown information.

---

## Output

Create:

docs/analysis/[domain]-analysis.md

Provide source file references for all findings.

Do not generate code.
``