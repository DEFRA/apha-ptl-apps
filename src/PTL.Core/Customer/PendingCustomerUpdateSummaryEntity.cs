namespace PTL.Core.Customer;

// Row shape returned by spgaPendingCustomerDetailsEditInfo - one per outstanding pending customer
// update, joined with tblCustomer for display (legacy ReviewPendingCustomerUpdates.aspx grid).
public class PendingCustomerUpdateSummaryEntity
{
    public Guid CustomerId { get; set; }
    public Guid PendingCustomerUpdateId { get; set; }
    public string QalNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
