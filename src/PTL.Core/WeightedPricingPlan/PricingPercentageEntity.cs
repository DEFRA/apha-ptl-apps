namespace PTL.Core.WeightedPricingPlan;

// One grid cell: the weight/percentage for a (year, distributions-available-on-scheme,
// distributions-chosen) combination. Backed by tblPricingPercentage (mirrors PtaBusinessObjects.
// BusinessObjects.SystemObjects.PricingPercentage).
public class PricingPercentageEntity
{
    public Guid PricingPercentageId { get; set; }
    public int YearId { get; set; }
    public int NumberOfDistributionsOnScheme { get; set; }
    public int NumberOfDistributionsChosen { get; set; }
    public int Weight { get; set; }
}
