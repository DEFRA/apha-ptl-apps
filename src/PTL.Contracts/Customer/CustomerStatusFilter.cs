namespace PTL.Contracts.Customer;

// Mirrors the legacy Enums.CustomerStatus filter used by spgaCustomerInfo (@IsActive bit, null = all).
// Defined here (not PTL.Core) because it is also bound directly on CustomerRequest and used by
// PTL.ApiClient/PTL.InternalWeb, which do not reference PTL.Core.
public enum CustomerStatusFilter
{
    Active,
    Inactive,
    All
}
