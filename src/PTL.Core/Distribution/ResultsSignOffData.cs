namespace PTL.Core.Distribution;

// spgaMonthlyDistributionInfoForResultsSignOff - legacy MonthlyDistributionInfoCollection.FetchCollection (Results
// Sign-off): the months, the schemes in them and the tabulations of each scheme that await final approval.
public sealed class ResultsSignOffMonthEntity
{
    public int YearId { get; init; }
    public int MonthId { get; init; }
    public List<ResultsSignOffSchemeEntity> Schemes { get; } = [];
}

public sealed class ResultsSignOffSchemeEntity
{
    public Guid MonthlyDistributionSchemeId { get; init; }
    public string SchemeName { get; init; } = string.Empty;
    public string SchemeIdentifier { get; init; } = string.Empty;
    public string DistributionReference { get; init; } = string.Empty;
    public string DistributionReferenceSuffix { get; init; } = string.Empty;
    public string ScheduleCode { get; init; } = string.Empty;
    public DateTime TargetDate { get; init; }
    public string TestConsultants { get; init; } = string.Empty;
    public string Assessors { get; init; } = string.Empty;
    public List<ResultsSignOffTabulationEntity> Tabulations { get; } = [];
}

public sealed class ResultsSignOffTabulationEntity
{
    public Guid DistributionTabulationId { get; init; }
    public string Name { get; init; } = string.Empty;
    public int PublishCount { get; init; }
    public bool SingleParticipantTabulation { get; init; }
    public bool AvailableToParticipants { get; init; }
    public bool AvailableToViewers { get; init; }
}
