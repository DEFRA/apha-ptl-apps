namespace PTL.Contracts.Distribution;

// Assessor menu (legacy Assessor/MenuAssessor.aspx): the years the signed-in Assessor has distributions in, the year being
// shown (the latest unless another valid one was asked for) and that year's months, newest first. SelectedYear is 0 when
// the Assessor has no distributions at all.
public sealed record AssessorMenuResponse(
    IReadOnlyList<int> Years,
    int SelectedYear,
    IReadOnlyList<AssessorMonthResponse> Months);

public sealed record AssessorMonthResponse(int YearId, int MonthId, IReadOnlyList<AssessorDistributionResponse> Distributions);

// DistributionTitle reads "{reference}{suffix}/{schedule}: {scheme identifier} {scheme name}".
public sealed record AssessorDistributionResponse(
    Guid MonthlyDistributionSchemeId,
    string DistributionTitle,
    IReadOnlyList<AssessorTestResponse> Tests);

public sealed record AssessorTestResponse(Guid TestId, string TestType, bool IsAssessmentSubmitted);

// Assessment Items (legacy Assessor/AssessmentItemList.aspx): one table per sample, a row per participant who submitted it.
// SubmittedBy and SubmittedDate are set once the Assessor has submitted the test; no further changes are possible then.
// HasCompletedAssessment is true when every item has an assessment, so Submit asks its plain question (legacy
// SampleInfoCollection.HasCompletedAssessment); otherwise Submit warns that not every sample has been assessed.
public sealed record AssessorItemListResponse(
    Guid TestId,
    string YearMonthTitle,
    string DistributionTitle,
    string TestName,
    bool IsAssessmentSubmitted,
    string SubmittedBy,
    DateTime? SubmittedDate,
    IReadOnlyList<AssessorSampleResponse> Samples,
    bool HasCompletedAssessment);

public sealed record AssessorSampleResponse(Guid SampleId, string SampleNumber, IReadOnlyList<AssessorItemResponse> Items);

// Participants are identified by Lab Code only. HasAssessment is "Assessment Entered" (Yes/No) on the page.
public sealed record AssessorItemResponse(
    Guid SampleTestId,
    Guid ParticipantId,
    string LabCode,
    bool HasAssessment);

// Assessment Entry (legacy Assessor/AssessmentEntry.aspx): one participant's submission of one sample. OverallScore is 0 until
// the first save. Once the test is submitted (IsAssessmentSubmitted) nothing can be changed.
public sealed record AssessorEntryResponse(
    Guid TestId,
    Guid SampleTestId,
    Guid ParticipantId,
    string YearMonthTitle,
    string DistributionTitle,
    string LabCode,
    string SampleNumber,
    string TestName,
    bool IsAssessmentSubmitted,
    string Comment,
    int OverallScore,
    IReadOnlyList<AssessorResultItemResponse> ResultItems,
    IReadOnlyList<AssessorCategoryResponse> Categories);

// The participant's own result for one of the test's result items (the page's "Test result item name" / "Result" grid).
public sealed record AssessorResultItemResponse(string Name, string Result);

// Score is 0 until it has been saved. Criteria are the checkboxes of the category.
public sealed record AssessorCategoryResponse(
    Guid CategoryItemId,
    string Name,
    int Score,
    IReadOnlyList<AssessorCriterionResponse> Criteria);

public sealed record AssessorCriterionResponse(Guid CriterionItemId, string Name, bool IsSelected);

// What Save and Apply send: every category of the test with its score and ticked criteria, the overall score and the comment.
public sealed record AssessorEntrySaveRequest(
    string? Comment,
    int OverallScore,
    IReadOnlyList<AssessorCategorySaveRequest> Categories);

public sealed record AssessorCategorySaveRequest(Guid CategoryItemId, int Score, IReadOnlyList<Guid> CriterionItemIds);
