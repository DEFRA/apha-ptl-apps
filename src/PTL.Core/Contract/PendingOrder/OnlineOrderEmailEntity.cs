namespace PTL.Core.Contract.PendingOrder;

// Row shape returned by spgaOnlineContractOrderEmail (tblOnlineContractOrderEmail, fldDateSent null).
public class OnlineOrderEmailEntity
{
    public Guid Id { get; set; }
    public string? Email { get; set; }
    public Guid ContractId { get; set; }
    public Guid PendingContractId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public int YearId { get; set; }
    public string Notes { get; set; } = string.Empty;
    public bool IsApproved { get; set; }
}
