namespace PTL.Core.Lookup;

// Keyless domain projection for the rows returned by spgPostageByYearID - see
// docs/analysis/scheme-analysis.md, "Postage Pricing Plan Read Operations".
public class PostagePricingPlanEntity
{
    public Guid PostageId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal? UKPrice { get; set; }
    public decimal? EUPrice { get; set; }
    public decimal? NonEUPrice { get; set; }
    public int YearId { get; set; }
}
