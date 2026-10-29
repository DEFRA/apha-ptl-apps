namespace PTL.ExternalWeb.Features.Distribution;

public sealed class CurrentDistributionsViewModel
{
    public IReadOnlyList<CurrentDistributionRowViewModel> Distributions { get; init; } = [];
}

public sealed class CurrentDistributionRowViewModel
{
    public required Guid MonthlyDistributionSchemeId { get; init; }
    public required Guid DistributionParticipantId { get; init; }
    public required string DistributionReference { get; init; }
    public required string SchemeCode { get; init; }
    public required string SchemeName { get; init; }
    public required DateTime DistributionDate { get; init; }
    public required DateTime DeadlineDate { get; init; }
    public required bool HasResults { get; init; }
    public required bool Submitted { get; init; }

    public bool ResultsEntered => Submitted || HasResults;
}
