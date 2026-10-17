namespace PTL.Contracts.Distribution;

// Tabulation (legacy Scheduling/Tabulation.aspx) and Issued Tabulation (Scheduling/PublishedTabulation.aspx).
// Both pages build the same tables from different stored procedures, so they share this model.
public sealed record TabulationReportResponse(
    Guid TabulationId,
    bool IsPublished,
    Guid DistributionTabulationId,
    string Title,
    string TabulationName,
    string DistributionDateText,
    string StandardTabulationTextHtml,
    string CommentsHtml,
    string TestConsultantCommentsHtml,
    string TestConsultantCommentsTitle,
    bool ShowUkasLogo,
    TabulationSignatoryResponse? Signatory,
    IReadOnlyList<TabulationFilterOptionResponse> ParticipantOptions,
    Guid SelectedParticipantId,
    IReadOnlyList<TabulationFilterOptionResponse> ViewerOptions,
    Guid SelectedViewerId,
    IReadOnlyList<TabulationTableResponse> Tables,
    TabulationTableResponse? Ratings,
    TabulationAttachmentResponse? Attachment,
    TabulationPrintResponse Print,
    string DistributionHeading = "");

// Issued Tabulation only: the signature block printed under the tables.
public sealed record TabulationSignatoryResponse(
    string VersionText, string Signatory, IReadOnlyList<string> AddressLines, string IssuedText);

public sealed record TabulationFilterOptionResponse(Guid Value, string Text);

// One legacy table. Rows are the exact legacy grid (row/col spans included) so the screen version
// matches the legacy layout cell for cell. AssessmentTestId is set when the legacy page shows the
// "View assessment results" link under the table.
public sealed record TabulationTableResponse(
    string Caption,
    IReadOnlyList<TabulationGridRowResponse> Rows,
    Guid? AssessmentTestId);

// IsHeader marks the two legacy column-heading rows.
public sealed record TabulationGridRowResponse(IReadOnlyList<TabulationCellResponse> Cells, bool IsHeader);

// Kind: "Header1" / "Header2" (legacy TabulationHeader1/2), "Cell" (TabulationCell) or "Plain"
// (legacy cells rendered without a class). IsNilReturn = legacy NilReturn styling.
public sealed record TabulationCellResponse(string Text, int ColSpan, int RowSpan, string Kind, bool IsNilReturn);

public sealed record TabulationAttachmentResponse(Guid AttachmentId, string FileName);

// The data legacy wrote out for TabulationTables.js, which rebuilt the tables in print-sized
// chunks (columns are moved to a new table once the page width is exceeded).
public sealed record TabulationPrintResponse(
    IReadOnlyList<TabulationPrintParticipantResponse> Participants,
    IReadOnlyList<TabulationPrintTestResponse> Tests,
    IReadOnlyList<TabulationPrintRatingColumnResponse> RatingColumns);

public sealed record TabulationPrintParticipantResponse(string LabCode, bool HasResults, string NilReturnReason);

public sealed record TabulationPrintTestResponse(
    string Name,
    IReadOnlyList<TabulationPrintMethodColumnResponse> MethodColumns,
    IReadOnlyList<TabulationPrintSampleColumnResponse> SampleColumns);

public sealed record TabulationPrintMethodColumnResponse(string HeaderText, string IntendedResult, IReadOnlyList<string> Results);

public sealed record TabulationPrintSampleColumnResponse(
    string SampleNumber, string TabulationDescription, IReadOnlyList<TabulationPrintResultColumnResponse> ResultColumns);

public sealed record TabulationPrintResultColumnResponse(
    string HeaderText, string IntendedResult, IReadOnlyList<TabulationPrintResultResponse> Results);

// Span 0 = legacy "covered by the previous cell's span" (no cell is drawn).
public sealed record TabulationPrintResultResponse(string Result, int Span);

public sealed record TabulationPrintRatingColumnResponse(
    string SampleNumber,
    IReadOnlyList<string> Ratings,
    string SampleMean,
    string SamplePassRate,
    string NumberOfParticipants,
    string NumberReportingIntendedResult);

// Issued Tabulations (legacy Scheduling/PublishedTabulations.aspx).
public sealed record PublishedTabulationsResponse(
    Guid DistributionTabulationId,
    string Title,
    string TabulationName,
    IReadOnlyList<PublishedTabulationRowResponse> Tabulations);

public sealed record PublishedTabulationRowResponse(Guid PublishedTabulationId, DateTime CreationDate, string Signatory);

// Assessment results bar chart (legacy AssessmentResultsChart.ascx). IsAssessmentSubmitted false =
// legacy hid the chart and showed only the Back button.
public sealed record AssessmentResultsResponse(
    bool IsAssessmentSubmitted,
    IReadOnlyList<string> Titles,
    string XAxisTitle,
    string YAxisTitle,
    IReadOnlyList<AssessmentScoreFrequencyResponse> Scores,
    string DistributionHeading = "");

public sealed record AssessmentScoreFrequencyResponse(int Score, string Label, int Frequency);
