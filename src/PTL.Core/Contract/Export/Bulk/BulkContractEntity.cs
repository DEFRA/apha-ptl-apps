namespace PTL.Core.Contract.Export.Bulk;

/// <summary>
/// One contract row from <c>spgaExportContractDetails</c> (first result set), carrying exactly the
/// merge fields the legacy Contract and Job Sheet templates use.
/// </summary>
public sealed class BulkContractEntity
{
    public Guid ContractId { get; set; }
    public Guid CustomerId { get; set; }
    public string ContractNumber { get; set; } = string.Empty;
    public string Suffix { get; set; } = string.Empty;
    public int YearId { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public decimal AdministrationCharge { get; set; }
    public decimal DiscountRate { get; set; }
    public int NumberPostage { get; set; }
    public int NumberCourier { get; set; }
    public int NumberSpecialDelivery { get; set; }
    public decimal PostagePrice { get; set; }
    public decimal CourierPrice { get; set; }
    public decimal SpecialDeliveryPrice { get; set; }
    public DateTime CommencementDate { get; set; }
    public string QalNumber { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
    public string Organisation { get; set; } = string.Empty;
    public string Address1 { get; set; } = string.Empty;
    public string Address2 { get; set; } = string.Empty;
    public string Address3 { get; set; } = string.Empty;
    public string Address4 { get; set; } = string.Empty;
    public string Address5 { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string Telephone { get; set; } = string.Empty;
    public string Fax { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string InvoiceName { get; set; } = string.Empty;
    public string InvoiceOrganisation { get; set; } = string.Empty;
    public string InvoiceAddress1 { get; set; } = string.Empty;
    public string InvoiceAddress2 { get; set; } = string.Empty;
    public string InvoiceAddress3 { get; set; } = string.Empty;
    public string InvoiceAddress4 { get; set; } = string.Empty;
    public string InvoiceAddress5 { get; set; } = string.Empty;
    public string InvoiceCountry { get; set; } = string.Empty;
    public string InvoiceTelephone { get; set; } = string.Empty;
    public string InvoiceFax { get; set; } = string.Empty;
    public string InvoiceEmail { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string VatNumber { get; set; } = string.Empty;
    public string VatRating { get; set; } = string.Empty;
    public string PurchaseOrderNumber { get; set; } = string.Empty;

    public IReadOnlyList<BulkContractItemEntity> Items { get; set; } = [];

    public decimal TotalPriceItems => Items.Sum(i => i.Price);

    public decimal DiscountPrice => TotalPriceItems * -DiscountRate;

    /// <summary>Legacy Exports.Contract.TotalPrice.</summary>
    public decimal TotalPrice =>
        TotalPriceItems
        + DiscountPrice
        + AdministrationCharge
        + (PostagePrice * NumberPostage)
        + (CourierPrice * NumberCourier)
        + (SpecialDeliveryPrice * NumberSpecialDelivery);
}

/// <summary>A contract item row from the second result set of <c>spgaExportContractDetails</c>.</summary>
public sealed class BulkContractItemEntity
{
    public Guid ContractId { get; set; }
    public Guid ParticipantSchemeId { get; set; }
    public Guid ParticipantId { get; set; }
    public Guid SchemeId { get; set; }
    public string Identifier { get; set; } = string.Empty;
    public string SchemeName { get; set; } = string.Empty;
    public string LabCode { get; set; } = string.Empty;
    public string LabName { get; set; } = string.Empty;
    public int NoOfDistributions { get; set; }
    public decimal Price { get; set; }
}
