namespace PTL.Core.Contract;

// Server-side domain aggregate for the Contract Items read-model (GET /api/contracts/{contractId}/items),
// built from spgContractItems by PTL.Data.Contract.ContractRepository.GetContractItemsAsync. Mirrors
// PtaBusinessObjects.BusinessObjects.Contracts.ContractItems' pricing computations exactly for the
// non-online-order case (mNumberCourier/mNumberPostage/mNumberSpecialDelivery trusted as stored,
// price = count * unit price).
//
// [NEEDS INVESTIGATION] Legacy ContractItems.CourierPriceTotal/PostagePriceTotal/
// SpecialDeliveryPriceTotal silently ignore the stored counts and dynamically recompute them from
// live scheme/participant-scheme distribution-month data when the owning Contract.IsOnlineOrder is
// true. docs/migration/contract-migration.md explicitly calls for this to become a separate,
// testable pricing service rather than being replatformed ad hoc - not implemented in this pass.
public sealed class ContractItemsAggregate
{
    public Guid ContractId { get; set; }

    public string Suffix { get; set; } = string.Empty;

    public int YearId { get; set; }

    public string QalNumber { get; set; } = string.Empty;

    public string Symbol { get; set; } = string.Empty;

    public decimal DiscountRate { get; set; }

    public decimal AdministrationCharge { get; set; }

    public int NumberCourier { get; set; }

    public decimal CourierPrice { get; set; }

    public int NumberPostage { get; set; }

    public decimal PostagePrice { get; set; }

    public int NumberSpecialDelivery { get; set; }

    public decimal SpecialDeliveryPrice { get; set; }

    public bool IsReadOnly { get; set; }

    public List<ContractItemSchemeGroup> Schemes { get; set; } = [];

    public decimal CourierPriceTotal => NumberCourier * CourierPrice;

    public decimal PostagePriceTotal => NumberPostage * PostagePrice;

    public decimal SpecialDeliveryPriceTotal => NumberSpecialDelivery * SpecialDeliveryPrice;

    // Sum of every non-removed, fee-paying participant-scheme line item's price - matches legacy
    // ContractItems.TotalPriceItems.
    public decimal TotalPriceItems => Schemes
        .SelectMany(s => s.Participants)
        .Where(p => !p.NonFeePaying)
        .Sum(p => p.Price);

    // Matches legacy ContractItems.DiscountPrice: negative because DiscountRate is a positive
    // fraction (e.g. 0.1 = 10%) being subtracted from the total.
    public decimal DiscountPrice => TotalPriceItems * -DiscountRate;

    public decimal TotalPrice => TotalPriceItems + DiscountPrice + AdministrationCharge + PostagePriceTotal + CourierPriceTotal + SpecialDeliveryPriceTotal;
}

// One scheme grouping - matches legacy ContractScheme (PtaBusinessObjects.BusinessObjects.Contracts.ContractSchemeCollection).
public sealed class ContractItemSchemeGroup
{
    public Guid SchemeId { get; set; }

    public string SchemeIdentifier { get; set; } = string.Empty;

    public string SchemeName { get; set; } = string.Empty;

    public List<ContractItemLine> Participants { get; set; } = [];
}

// One priced participant-scheme line item - matches legacy ParticipantSchemeInfo.
public sealed class ContractItemLine
{
    public Guid ParticipantSchemeId { get; set; }

    public Guid ParticipantId { get; set; }

    public Guid SchemeId { get; set; }

    public string LabCode { get; set; } = string.Empty;

    public string LabName { get; set; } = string.Empty;

    public int NumberOfDistributions { get; set; }

    public decimal Price { get; set; }

    public bool NonFeePaying { get; set; }

    public bool HasOverride { get; set; }

    public string FullName => $"{LabCode}: {LabName}";
}
