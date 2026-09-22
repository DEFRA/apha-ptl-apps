namespace PTL.Contracts.Contract;

// Field set matches ContractResponse minus server-generated/system-managed values (ContractId,
// CustomerId - taken from the route, CustomerName, QalNumber, IsReadOnly, CommencementDate,
// IsInvoiceSent, ApprovedBy, ApprovedDate). See docs/analysis/contract-analysis.md: Contract.aspx.vb
// never posts these fields back even on edit - they are system/other-process managed. Shared by
// both create and update actions since the editable field set is identical for both operations.
public sealed record ContractRequest(
    int YearId,
    string UTNumber,
    string FTNumber,
    string ContractSignatory,
    string ActionsRequired,
    string RenewalInformation,
    decimal DiscountRate,
    decimal AdministrationCharge,
    int NumberCourier,
    decimal CourierPrice,
    int NumberPostage,
    decimal PostagePrice,
    int NumberSpecialDelivery,
    decimal SpecialDeliveryPrice,
    DateTime AcknowledgementPostedDate,
    DateTime AcknowledgementReturnedDate,
    DateTime JobSheetPostedDate,
    string ReasonForClosure,
    DateTime DateOfLeaving,
    bool IsActive,
    string Suffix,
    string PurchaseOrderNumber,
    bool OptOutOfInvoiceGeneration,
    bool IsOnlineOrder);
