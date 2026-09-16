namespace PTL.Contracts.Customer;

// Query-binding contract for GET /api/customers/search. Search/filter/paging is performed
// in-memory over the existing spgaCustomerInfo result set (see CustomerService) - no new
// stored procedure was introduced to satisfy this.
public sealed record CustomerSearchRequest(string? SearchTerm = null, CustomerStatusFilter Status = CustomerStatusFilter.Active, int Page = 1, int PageSize = 20);
