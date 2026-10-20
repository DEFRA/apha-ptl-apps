namespace PTL.Core.Distribution;

// The tabulation data both spgDistributionTabulation (legacy ResultsTabulation.Tabulation) and
// spgPublishedTabulation (legacy Published.Tabulation) are normalised into by the repository.
public sealed class TabulationSource
{
    public Guid TabulationId { get; init; }

    // The draft tabulation (the published copy's parent for an Issued Tabulation).
    public Guid DistributionTabulationId { get; init; }

    public bool IsPublished { get; init; }

    // "{reference}{suffix}/{schedule code}" - published rows store it pre-built as fldDistribution.
    public string Distribution { get; init; } = string.Empty;
    public string SchemeIdentifier { get; init; } = string.Empty;
    public string SchemeName { get; init; } = string.Empty;
    public string TabulationName { get; init; } = string.Empty;
    public bool RequiresAssessment { get; init; }
    public string Comments { get; init; } = string.Empty;
    public string TestConsultantComments { get; init; } = string.Empty;
    public string TestConsultantCommentsAuthor { get; init; } = string.Empty;
    public string TestConsultantCommentsDept { get; init; } = string.Empty;
    public DateTime DistributionDate { get; init; }
    public bool ShowUkasLogo { get; init; }
    public DateTime SchemeVersionDate { get; init; }
    public bool HasRatings { get; init; }
    public bool IntendedResultsOnly { get; init; }
    public bool SingleParticipantTabulation { get; init; }
    public bool AvailableToViewers { get; init; }
    public string StandardTabulationText { get; init; } = string.Empty;

    // Issued Tabulation only.
    public string Signatory { get; init; } = string.Empty;
    public DateTime? CreationDate { get; init; }

    public List<TabulationSourceTest> Tests { get; } = [];
    public List<TabulationSourceParticipant> Participants { get; } = [];
    public Dictionary<Guid, string> IntendedMethodResults { get; } = [];
    public Dictionary<(Guid SampleId, Guid ResultItemId), (string Result, string Result2)> IntendedResultItemResults { get; } = [];
    public List<TabulationSourceViewer> Viewers { get; } = [];
    public List<TabulationSourceAttachment> Attachments { get; } = [];
}

public sealed class TabulationSourceTest
{
    public Guid TestId { get; init; }
    public string Name { get; init; } = string.Empty;

    // Legacy only links to the assessment results once the test's assessment is submitted; the
    // Issued Tabulation shows the link for every test of an assessment scheme, so it sets this true.
    public bool IsAssessmentSubmitted { get; init; }

    public List<TabulationSourceSample> Samples { get; } = [];
    public List<TabulationSourceItem> MethodItems { get; } = [];
    public List<TabulationSourceItem> ResultItems { get; } = [];
    public List<TabulationSourceItem> CategoryItems { get; } = [];
}

public sealed record TabulationSourceSample(Guid SampleId, Guid? SampleTestId, string SampleNumber, string TabulationDescription);

public sealed record TabulationSourceItem(Guid ItemId, string Name);

public sealed class TabulationSourceParticipant
{
    public Guid ParticipantId { get; init; }
    public string LabCode { get; init; } = string.Empty;
    public string NilReturnReason { get; init; } = string.Empty;
    public Dictionary<Guid, string> SampleRatings { get; } = [];
    public Dictionary<Guid, string> MethodResults { get; } = [];
    public Dictionary<(Guid SampleId, Guid ResultItemId), string> ResultItemResults { get; } = [];
    public Dictionary<(Guid SampleId, Guid CategoryItemId), TabulationSourceCategoryResult> CategoryResults { get; } = [];
    public Dictionary<(Guid SampleId, Guid TestId), TabulationSourceAssessment> Assessments { get; } = [];
}

// SampleTestId is null for the published copy, which matches on the sample alone.
public sealed class TabulationSourceCategoryResult
{
    public Guid? SampleTestId { get; init; }
    public int Score { get; init; }
    public bool IsAssessmentSubmitted { get; init; }
    public List<string> Criteria { get; } = [];
}

public sealed record TabulationSourceAssessment(
    int Score,
    string Comment,
    DateTime? SubmissionDate,
    DateTime? NonSubmissionDate,
    DateTime? NonCapableDate,
    DateTime? AssessmentSubmitted);

public sealed class TabulationSourceViewer
{
    public Guid ViewerId { get; init; }
    public string Name { get; init; } = string.Empty;
    public HashSet<Guid> ParticipantIds { get; } = [];
}

public sealed record TabulationSourceAttachment(Guid AttachmentId, string FileName, string ContentType, byte[] FileData);

// spgPublishedTabulations - legacy Published.PublishedTabulationInfoCollection.
public sealed record PublishedTabulationsSource(
    string TabulationName,
    string Distribution,
    string SchemeIdentifier,
    string SchemeName,
    IReadOnlyList<PublishedTabulationSourceRow> Tabulations);

public sealed record PublishedTabulationSourceRow(Guid PublishedTabulationId, string Signatory, DateTime CreationDate);

// spgAssessmentScoreByTestId / spgPublishedAssessmentScoreByTestId - legacy AssessmentScoreInfoCollection.
public sealed record AssessmentScoreSource(
    string TabulationName,
    string SchemeName,
    string Distribution,
    string TestName,
    DateTime? AssessmentSubmitted,
    IReadOnlyList<string> SampleNumbers,
    IReadOnlyList<AssessmentScoreReceipt> Receipts);

public sealed record AssessmentScoreReceipt(
    DateTime? SubmissionDate, DateTime? NonSubmissionDate, DateTime? NonCapableDate, int Score, DateTime? AssessmentDate);
