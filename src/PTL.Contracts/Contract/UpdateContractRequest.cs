namespace PTL.Contracts.Contract;

// Same editable field set as CreateContractRequest. ContractId comes from the route; CustomerId,
// CommencementDate, IsInvoiceSent, ApprovedBy, and ApprovedDate are preserved server-side and
// cannot be edited (see docs/analysis/contract-analysis.md).
public sealed record UpdateContractRequest(
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
