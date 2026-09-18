namespace PTL.Contracts.Lookup;

// Public API contract for GET /api/lookups/customer-types (spgaCustomerType). Not surfaced as a
// dropdown anywhere in the legacy Web Forms UI, but PtaBusinessObjects.BusinessObjects.Contracts.Customer
// validates CustomerTypeID as a required, non-empty GUID, so the modernised Create/Edit screens
// expose it as a proper "Customer type" dropdown instead of a free-text GUID box.
public sealed record CustomerTypeResponse(Guid CustomerTypeId, string CustomerType);
