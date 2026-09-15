namespace PTL.Contracts.Customer;

// Public API contract for GET /api/customers; matches the legacy CustomerInfo / spgaCustomerInfo projection.
public sealed record CustomerSummaryResponse(Guid CustomerId, string QalNumber, string Name, string Organisation, bool IsActive);
