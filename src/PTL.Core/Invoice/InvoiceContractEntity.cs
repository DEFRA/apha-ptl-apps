namespace PTL.Core.Invoice;

// Keyless projection of spgaExportContractDetailsForAutomaticInvoicing's first result set
// (docs/analysis/invoice-analysis.md). Deliberately NOT the Contract domain's own Contract entity -
// legacy keeps a wholly separate, invoice-shaped read-model (PtaBusinessObjects.BusinessObjects.
// Invoices.Contract) with its own Fetch, and this mirrors that same boundary.
public class InvoiceContractEntity
{
    public Guid ContractId { get; set; }
    public Guid CustomerId { get; set; }
    public string Suffix { get; set; } = string.Empty;
    public int YearId { get; set; }
    public decimal AdministrationCharge { get; set; }
    public decimal DiscountRate { get; set; }
    public int NumberPostage { get; set; }
    public decimal PostagePrice { get; set; }
    public int NumberCourier { get; set; }
    public decimal CourierPrice { get; set; }
    public int NumberSpecialDelivery { get; set; }
    public decimal SpecialDeliveryPrice { get; set; }
    public string QalNumber { get; set; } = string.Empty;
    public string InvoiceOrganisation { get; set; } = string.Empty;
    public string InvoiceAddress1 { get; set; } = string.Empty;
    public string InvoiceAddress2 { get; set; } = string.Empty;
    public string InvoiceAddress3 { get; set; } = string.Empty;
    public string InvoiceAddress4 { get; set; } = string.Empty;
    public string InvoiceAddress5 { get; set; } = string.Empty;
    public string InvoiceCountry { get; set; } = string.Empty;
    public string VatNumber { get; set; } = string.Empty;
    public string VatRating { get; set; } = string.Empty;
    public string PurchaseOrderNumber { get; set; } = string.Empty;
    public string CustomerNumber { get; set; } = string.Empty;
    public string CustomerType { get; set; } = string.Empty;

    // Informational only - never a generation filter (approved business decision, see
    // docs/migration/invoice-migration.md "Opt Out Of Invoice Generation").
    public bool OptOutOfInvoiceGeneration { get; set; }

    // Legacy Contract.CombinedPostagePriceTotal - sum of the three already-persisted postage
    // quantities x prices. Invoice Generation never recalculates postage, only reads it.
    public decimal CombinedPostagePriceTotal =>
        (NumberPostage * PostagePrice) + (NumberCourier * CourierPrice) + (NumberSpecialDelivery * SpecialDeliveryPrice);

    // Legacy Contract.SpecialInstructions - a computed display string, not a stored column.
    // Preserve the exact concatenation (no space before QalNumber, no separator before Suffix).
    public string SpecialInstructions => "Provision of PT Services Contract Ref No." + QalNumber + Suffix;

    // Legacy Contract.InvoiceDetails - comma-joins non-empty invoice address lines, each line only
    // appended while the PRECEDING line is also non-empty (a legacy quirk, preserved exactly).
    public string InvoiceDetails
    {
        get
        {
            var details = InvoiceAddress1;
            if (!string.IsNullOrEmpty(InvoiceAddress2))
            {
                details += ", " + InvoiceAddress2;
                if (!string.IsNullOrEmpty(InvoiceAddress3))
                {
                    details += ", " + InvoiceAddress3;
                    if (!string.IsNullOrEmpty(InvoiceAddress4))
                    {
                        details += ", " + InvoiceAddress4;
                        if (!string.IsNullOrEmpty(InvoiceAddress5))
                        {
                            details += ", " + InvoiceAddress5;
                        }
                    }
                }
            }

            if (!string.IsNullOrEmpty(InvoiceCountry))
            {
                details += ", " + InvoiceCountry;
            }

            return details;
        }
    }
}
