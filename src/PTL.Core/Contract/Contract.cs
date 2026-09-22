namespace PTL.Core.Contract;

// Domain entity mapped to tblContract by PTL.Data's ContractRepository (Dapper); shape matches the row
// returned by spgContractByContractId (joined to tblCustomer for CustomerName/QalNumber).
// CommencementDate, IsInvoiceSent, ApprovedBy, ApprovedDate, and IsReadOnly are system-managed
// (see docs/analysis/contract-analysis.md) - never bound from Create/Update requests.
public class Contract
{
    public Guid ContractId { get; set; }
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string QalNumber { get; set; } = string.Empty;
    public int YearId { get; set; }
    public string UTNumber { get; set; } = string.Empty;
    public string FTNumber { get; set; } = string.Empty;
    public string ContractSignatory { get; set; } = string.Empty;
    public string ActionsRequired { get; set; } = string.Empty;
    public string RenewalInformation { get; set; } = string.Empty;
    public decimal DiscountRate { get; set; }
    public decimal AdministrationCharge { get; set; }
    public int NumberCourier { get; set; }
    public decimal CourierPrice { get; set; }
    public int NumberPostage { get; set; }
    public decimal PostagePrice { get; set; }
    public int NumberSpecialDelivery { get; set; }
    public decimal SpecialDeliveryPrice { get; set; }
    // Nullable to match the actual nullable tblContract columns - legacy rows can have a true
    // NULL here (as opposed to the 1/1/9999 "unset" sentinel used by Create/Update requests).
    public DateTime? AcknowledgementPostedDate { get; set; }
    public DateTime? AcknowledgementReturnedDate { get; set; }
    public DateTime? JobSheetPostedDate { get; set; }
    public string ReasonForClosure { get; set; } = string.Empty;
    public DateTime? DateOfLeaving { get; set; }
    public bool IsActive { get; set; }

    // Computed by spgContractByContractId (YearId < current-year-with-delay); never sent in requests.
    public bool IsReadOnly { get; set; }
    public string Suffix { get; set; } = string.Empty;
    public DateTime? CommencementDate { get; set; }
    public string PurchaseOrderNumber { get; set; } = string.Empty;
    public bool OptOutOfInvoiceGeneration { get; set; }
    public bool IsInvoiceSent { get; set; }
    public bool IsOnlineOrder { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedDate { get; set; }
}
