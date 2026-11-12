namespace PTL.ExternalWeb.Features.Distribution;

public sealed class DistributionNilReturnViewModel
{
    public Guid? MonthlyDistributionSchemeId { get; set; }

    public string Heading { get; set; } = string.Empty;

    public bool? HasNilReturn { get; set; }

    public string? NilReturnReason { get; set; }
}
