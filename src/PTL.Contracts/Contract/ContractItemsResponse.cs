namespace PTL.Contracts.Contract;

// Public API contract for GET /api/contracts/{contractId}/items - a single aggregated read-model
// over spgContractItems, matching legacy PtaBusinessObjects.BusinessObjects.Contracts.ContractItems
// (ContractSchemeCollection / ParticipantSchemeInfoCollection reassembled server-side). Priced totals
// (CourierPriceTotal/PostagePriceTotal/SpecialDeliveryPriceTotal/DiscountPrice/TotalPrice) reproduce
// the legacy non-online-order pricing formulas exactly; the IsOnlineOrder dynamic recomputation
// (see docs/analysis/contract-analysis.md, "Business Rules") is deliberately out of scope for this
// pass - see PTL.Core.Contract.ContractItemsAggregate for the [NEEDS INVESTIGATION] note.
public sealed record ContractItemsResponse(
    Guid ContractId,
    string Suffix,
    int YearId,
    string QalNumber,
    string Symbol,
    decimal DiscountRate,
    decimal AdministrationCharge,
    int NumberCourier,
    decimal CourierPrice,
    decimal CourierPriceTotal,
    int NumberPostage,
    decimal PostagePrice,
    decimal PostagePriceTotal,
    int NumberSpecialDelivery,
    decimal SpecialDeliveryPrice,
    decimal SpecialDeliveryPriceTotal,
    decimal DiscountPrice,
    decimal TotalPriceItems,
    decimal TotalPrice,
    bool IsReadOnly,
    IReadOnlyList<ContractItemSchemeResponse> Schemes);
