namespace PTL.Core.Customer;

// Keyless domain projection for the list returned by spgaCustomerInfo.
public class CustomerSummaryEntity
{
    public Guid CustomerId { get; set; }
    public string QalNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Organisation { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
