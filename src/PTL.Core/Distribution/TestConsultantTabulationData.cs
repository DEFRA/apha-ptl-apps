namespace PTL.Core.Distribution;

// One row of spgMonthlyDistributionInfoForTestConsultant: a distribution scheme the Test Consultant is allocated to.
public sealed class TestConsultantDistributionEntity
{
    public int YearId { get; init; }
    public int MonthId { get; init; }
    public Guid MonthlyDistributionSchemeId { get; init; }
    public string SchemeName { get; init; } = string.Empty;
    public string SchemeIdentifier { get; init; } = string.Empty;
    public string DistributionReference { get; init; } = string.Empty;
    public string DistributionReferenceSuffix { get; init; } = string.Empty;
    public string ScheduleCode { get; init; } = string.Empty;
    public DateTime TargetDate { get; init; }
    public bool IsCommentSubmitted { get; init; }
}

// spgMonthlyDistributionSchemeForTestConsultant - legacy Test Consultant MonthlyDistributionScheme. The tabulation
// is normalised into the same TabulationSource the Tabulation page uses, so TabulationReportBuilder draws the grids.
public sealed class TestConsultantTabulationEntity
{
    public required TabulationSource Source { get; init; }
    public bool IsCommentSubmitted { get; init; }
    public List<TestConsultantSampleInformation> SampleDetails { get; } = [];
    public List<TestConsultantParticipantInformation> Participants { get; } = [];
}

public sealed record TestConsultantSampleInformation(string SampleNumber, string TabulationDescription, string TestConsultantsDescription);

public sealed record TestConsultantParticipantInformation(string LabCode, bool ExtraDocumentsReturned, string DeadlineChangeReason);

// Who may comment on a scheme and what has been written so far. A Test Consultant slot is null when unallocated.
public sealed record TestConsultantSchemeState(
    Guid? TestConsultant1Id,
    Guid? TestConsultant2Id,
    Guid? TestConsultant3Id,
    bool IsCommentSubmitted,
    string Comments,
    Guid AuthorId)
{
    public bool IsAllocated(Guid testConsultantId) =>
        testConsultantId != Guid.Empty
        && (TestConsultant1Id == testConsultantId || TestConsultant2Id == testConsultantId || TestConsultant3Id == testConsultantId);
}

public interface ITestConsultantTabulationRepository
{
    // Months and schemes of spgMonthlyDistributionInfoForTestConsultant, newest month first.
    Task<IReadOnlyList<TestConsultantDistributionEntity>> GetDistributionsAsync(Guid testConsultantId, CancellationToken cancellationToken = default);

    // Null when the scheme does not exist or is older than the seven year retention window.
    Task<TestConsultantTabulationEntity?> GetTabulationAsync(Guid monthlyDistributionSchemeId, bool fullTabulation, CancellationToken cancellationToken = default);

    Task<TestConsultantSchemeState?> GetSchemeStateAsync(Guid monthlyDistributionSchemeId, CancellationToken cancellationToken = default);

    // fnGetTestConsultantNameById, used as the "who" of the audit row.
    Task<string?> GetTestConsultantNameAsync(Guid testConsultantId, CancellationToken cancellationToken = default);

    // True when the id belongs to an inactive Test Consultant - an internal user (tblUsers) or an
    // External Test Consultant (tblExternalTestConsultant). Unknown ids are not inactive.
    Task<bool> IsInactiveAsync(Guid testConsultantId, CancellationToken cancellationToken = default);
}
