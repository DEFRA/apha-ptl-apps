namespace PTL.Core.Lookup;

// Keyless domain projection for the list returned by spgaCustomerType.
public class CustomerTypeEntity
{
    public Guid CustomerTypeId { get; set; }
    public string CustomerType { get; set; } = string.Empty;
}
