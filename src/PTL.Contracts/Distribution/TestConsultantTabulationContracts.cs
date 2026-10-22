namespace PTL.Contracts.Distribution;

// Test Consultant menu (legacy Test Consultant/MenuTestConsultants.aspx): the distributions the signed-in
// Test Consultant is allocated to, newest first. The page groups them by year and month.
public sealed record TestConsultantDistributionsResponse(IReadOnlyList<TestConsultantDistributionResponse> Distributions);

// CommentStatus is "Awaiting comments" until the comments are submitted, then "Comments submitted".
public sealed record TestConsultantDistributionResponse(
    Guid MonthlyDistributionSchemeId,
    int YearId,
    int MonthId,
    string DistributionTitle,
    DateTime TargetDate,
    bool IsCommentSubmitted,
    string CommentStatus);

// Which columns the Test Consultant has un-ticked before pressing "Draw tabulations" (legacy HiddenColumnBlocks).
// HiddenSamples holds sample numbers such as "26/1257".
public sealed record TestConsultantTabulationFilter(IReadOnlyList<string> HiddenSamples, bool HideMethodItems)
{
    public static TestConsultantTabulationFilter None { get; } = new([], false);
}

// Test Consultant Tabulation (legacy Test Consultant/Tabulation.aspx). The Results and Full Results tabs
// are the same grid model as the Tabulation page; the full tab adds every result/method item and the
// participant information table.
public sealed record TestConsultantTabulationResponse(
    Guid MonthlyDistributionSchemeId,
    string Title,
    string DistributionDateText,
    string StandardTabulationTextHtml,
    string CommentsHtml,
    bool HasMethodItems,
    IReadOnlyList<string> SampleNumbers,
    IReadOnlyList<TabulationTableResponse> ResultsTables,
    TabulationTableResponse? ResultsRatings,
    IReadOnlyList<TabulationTableResponse> FullTables,
    TabulationTableResponse? FullRatings,
    TabulationTableResponse? ParticipantInformation,
    IReadOnlyList<TestConsultantSampleInformationResponse> SampleInformation,
    TestConsultantCommentsStateResponse Comments);

public sealed record TestConsultantSampleInformationResponse(string SampleNumber, string TabulationDescription, string TestConsultantsDescription);

// IsSubmitted comments are read-only for good; Heading is the legacy label above them ("Add your comments here:",
// "Comments provided by ...:" or the no-comments message).
public sealed record TestConsultantCommentsStateResponse(bool IsSubmitted, string Heading, string CommentsHtml, bool HasComments);

// Submit = "Save and Submit"; otherwise Save (a draft the Test Consultant can return to).
public sealed record TestConsultantTabulationSaveRequest(string? Comments, bool Submit);

// Errors are keyed by field ("Comments"). IsLocked: the comments were already submitted, so nothing was changed.
public sealed record TestConsultantTabulationSaveResult(bool Success, bool IsLocked, IReadOnlyDictionary<string, string> Errors);
