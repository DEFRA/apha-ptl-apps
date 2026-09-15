namespace PTL.Contracts.Customer;

// Query-binding contract for GET /api/customers.
public sealed record CustomerRequest(CustomerStatusFilter Status = CustomerStatusFilter.Active);
