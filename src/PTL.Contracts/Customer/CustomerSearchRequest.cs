namespace PTL.Contracts.Customer;

// Query-binding contract for GET /api/customers/search: the Customers list's Status filter and paging
// (legacy CustomerList.aspx). Filtering and paging are done in-memory over the existing
// spgaCustomerInfo result set (see CustomerService). Searching by criteria is the Search feature
// (PTL.Contracts.Search).
public sealed record CustomerSearchRequest(CustomerStatusFilter Status = CustomerStatusFilter.Active, int Page = 1, int PageSize = 20);
