namespace PTL.Core.Invoice;

// Keyless projection of spgaExportContractDetailsForAutomaticInvoicing's second result set
// (docs/analysis/invoice-analysis.md). Mirrors legacy PtaBusinessObjects.BusinessObjects.Invoices.
// ContractItem exactly - price/override/non-fee-paying are already computed by the stored
// procedure (fnGetParticipantSchemePrice/fnParticipantSchemeHasOverride); this never recalculates.
public class InvoiceContractItemEntity
{
    public Guid ContractId { get; set; }
    public Guid ParticipantSchemeId { get; set; }
    public string SchemeIdentifier { get; set; } = string.Empty;
    public string SchemeName { get; set; } = string.Empty;
    public decimal Price { get; set; }

    // Informational only - never a generation filter (see docs/migration/invoice-migration.md).
    public bool NonFeePaying { get; set; }

    public bool HasOverride { get; set; }

    // Legacy ContractItem.Description.
    public string Description => SchemeIdentifier + " " + SchemeName;
}
