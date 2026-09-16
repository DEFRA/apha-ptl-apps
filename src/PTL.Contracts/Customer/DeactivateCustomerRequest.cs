namespace PTL.Contracts.Customer;

// Body for POST /api/customers/{customerId}/deactivate. CustomerStatusId records the inactive
// reason (mirrors Customer.aspx.vb's LoadStatusValues inactive-error flag).
public sealed record DeactivateCustomerRequest(Guid CustomerStatusId);
