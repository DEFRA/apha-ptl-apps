using System.Globalization;
using PTL.Contracts.Distribution;

namespace PTL.Core.Distribution;

public enum AssessorAccess
{
    Granted,
    NotFound,

    // The user is not one of the scheme's Assessors (legacy only filtered the menu, so any Assessor could open any test by URL).
    Forbidden,
}

public sealed record AssessorResult<T>(AssessorAccess Access, T? Value = default);

public interface IAssessorRepository
{
    // The years the Assessor has distributions to assess in, oldest first (legacy YearInfoCollection.FetchYearInfoCollectionByAssessorId).
    Task<IReadOnlyList<int>> GetYearsAsync(Guid assessorId, CancellationToken cancellationToken = default);

    // The months of one year with their distributions and tests, newest month first (MonthlyDistributionInfoCollection).
    Task<IReadOnlyList<AssessorMonthEntity>> GetMonthsAsync(Guid assessorId, int yearId, CancellationToken cancellationToken = default);

    // Whether the Assessor is one of the Assessors (1-4) of the scheme the test belongs to; NotFound for an unknown test.
    Task<AssessorAccess> GetTestAccessAsync(Guid testId, Guid assessorId, CancellationToken cancellationToken = default);

    // Null when the test is unknown, cancelled or past the seven-year window (SampleInfoCollection.FetchSampleInfoCollection).
    Task<AssessorItemListEntity?> GetItemListAsync(Guid testId, CancellationToken cancellationToken = default);

    // Whether the Assessor is one of the Assessors (1-4) of the scheme the entry belongs to; NotFound unless it is a submission
    // the Assessment Items list shows (an applicable sample the participant submitted).
    Task<AssessorAccess> GetEntryAccessAsync(Guid sampleTestId, Guid participantId, Guid assessorId, CancellationToken cancellationToken = default);

    // Legacy AssessmentEntry.FetchAssessmentEntry plus the participant's results (ActualTestResultItemInfoCollection); null when
    // there is no such submission.
    Task<AssessorEntryEntity?> GetEntryAsync(Guid sampleTestId, Guid participantId, CancellationToken cancellationToken = default);

    // Legacy AssessmentEntry.Save: in one transaction, taken while the test row is locked so a submit cannot slip in between, it
    // checks the assignment and that the test is not submitted, then writes what changed (see AssessorEntryPlanner) and the
    // audit rows. Saved when nothing had changed either.
    Task<AssessorSaveOutcome> SaveEntryAsync(AssessorEntrySave save, CancellationToken cancellationToken = default);

    // Legacy SubmitAssessment.Execute (sppSubmitAssessment): marks the test submitted by the Assessor, in one transaction taken
    // while the test row is locked, so a save cannot slip in and a second submit cannot overwrite the first. submittedDate is
    // when it is submitted.
    Task<AssessorSubmitOutcome> SubmitAsync(Guid testId, Guid assessorId, DateTime submittedDate, CancellationToken cancellationToken = default);
}

public interface IAssessorService
{
    Task<AssessorMenuResponse> GetMenuAsync(Guid assessorId, int? year, CancellationToken cancellationToken = default);

    Task<AssessorResult<AssessorItemListResponse>> GetItemListAsync(Guid assessorId, Guid testId, CancellationToken cancellationToken = default);

    Task<AssessorResult<AssessorEntryResponse>> GetEntryAsync(Guid assessorId, Guid sampleTestId, Guid participantId, CancellationToken cancellationToken = default);

    Task<AssessorSaveResult> SaveEntryAsync(Guid assessorId, Guid sampleTestId, Guid participantId, AssessorEntrySaveRequest request, CancellationToken cancellationToken = default);

    Task<AssessorSubmitOutcome> SubmitAsync(Guid assessorId, Guid testId, CancellationToken cancellationToken = default);
}

// Legacy Assessor/MenuAssessor.aspx, AssessmentItemList.aspx and AssessmentEntry.aspx: the wording and the order come from there.
public sealed class AssessorService(IAssessorRepository repository, TimeProvider timeProvider) : IAssessorService
{
    // Legacy AssessmentEntry validators: the overall and category scores are required (1-5) and the comment holds 2000 characters.
    public const string OverallScoreRequiredMessage = "An overall score is required";
    public const string CategoryScoreRequiredMessage = "A category score is required";
    public const string CommentTooLongMessage = "The assessment comment cannot exceed 2000 characters";
    public const string MismatchMessage = "The assessment does not match the categories and criteria of this test";
    public const int MaxCommentLength = 2000;
    public const int MinScore = 1;
    public const int MaxScore = 5;

    public async Task<AssessorMenuResponse> GetMenuAsync(Guid assessorId, int? year, CancellationToken cancellationToken = default)
    {
        var years = (await repository.GetYearsAsync(assessorId, cancellationToken)).Distinct().Order().ToList();
        if (years.Count == 0)
        {
            return new AssessorMenuResponse([], 0, []);
        }

        // Legacy filled the drop-down oldest first and selected the last (latest) year.
        var selected = year is { } requested && years.Contains(requested) ? requested : years[^1];
        var months = await repository.GetMonthsAsync(assessorId, selected, cancellationToken);

        return new AssessorMenuResponse(
            years,
            selected,
            [.. months.Select(month => new AssessorMonthResponse(
                month.YearId,
                month.MonthId,
                [.. month.Distributions.Select(d => new AssessorDistributionResponse(
                    d.MonthlyDistributionSchemeId,
                    DistributionTitle(d.DistributionReference, d.DistributionReferenceSuffix, d.ScheduleCode, d.SchemeIdentifier, d.SchemeName),
                    [.. d.Tests.Select(t => new AssessorTestResponse(t.TestId, t.TestType, t.AssessmentSubmitted is not null))]))]))]);
    }

    public async Task<AssessorResult<AssessorItemListResponse>> GetItemListAsync(Guid assessorId, Guid testId, CancellationToken cancellationToken = default)
    {
        var access = await repository.GetTestAccessAsync(testId, assessorId, cancellationToken);
        if (access != AssessorAccess.Granted)
        {
            return new AssessorResult<AssessorItemListResponse>(access);
        }

        var list = await repository.GetItemListAsync(testId, cancellationToken);
        if (list is null)
        {
            return new AssessorResult<AssessorItemListResponse>(AssessorAccess.NotFound);
        }

        return new AssessorResult<AssessorItemListResponse>(
            AssessorAccess.Granted,
            new AssessorItemListResponse(
                testId,
                YearMonthTitle(list.MonthId, list.YearId),
                DistributionTitle(list.DistributionReference, list.DistributionReferenceSuffix, list.ScheduleCode, list.SchemeIdentifier, list.SchemeName),
                list.TestName,
                list.AssessmentSubmitted is not null,
                list.AssessorName,
                list.AssessmentSubmitted,
                [.. list.Samples.Select(s => new AssessorSampleResponse(
                    s.SampleId,
                    s.SampleNumber,
                    [.. s.Items.Select(i => new AssessorItemResponse(i.SampleTestId, i.ParticipantId, i.LabCode, i.AssessmentDate is not null))]))],
                list.Samples.TrueForAll(s => s.Items.TrueForAll(i => i.AssessmentDate is not null))));
    }

    // Legacy BtnSubmit_Click: SubmitAssessment.Execute(testId, assessorId), stamped with DateTime.Now (local time).
    public async Task<AssessorSubmitOutcome> SubmitAsync(Guid assessorId, Guid testId, CancellationToken cancellationToken = default) =>
        await repository.SubmitAsync(testId, assessorId, timeProvider.GetLocalNow().DateTime, cancellationToken);

    public async Task<AssessorResult<AssessorEntryResponse>> GetEntryAsync(Guid assessorId, Guid sampleTestId, Guid participantId, CancellationToken cancellationToken = default)
    {
        var access = await repository.GetEntryAccessAsync(sampleTestId, participantId, assessorId, cancellationToken);
        if (access != AssessorAccess.Granted)
        {
            return new AssessorResult<AssessorEntryResponse>(access);
        }

        var entry = await repository.GetEntryAsync(sampleTestId, participantId, cancellationToken);
        if (entry is null)
        {
            return new AssessorResult<AssessorEntryResponse>(AssessorAccess.NotFound);
        }

        return new AssessorResult<AssessorEntryResponse>(
            AssessorAccess.Granted,
            new AssessorEntryResponse(
                entry.TestId,
                sampleTestId,
                participantId,
                YearMonthTitle(entry.MonthId, entry.YearId),
                DistributionTitle(entry.DistributionReference, entry.DistributionReferenceSuffix, entry.ScheduleCode, entry.SchemeIdentifier, entry.SchemeName),
                entry.LabCode,
                entry.SampleNumber,
                entry.TestName,
                entry.AssessmentSubmitted is not null,
                entry.Comment,
                entry.Score,
                [.. entry.ResultItems.Select(r => new AssessorResultItemResponse(r.Name, r.Result))],
                [.. entry.Categories.Select(c => new AssessorCategoryResponse(
                    c.CategoryItemId,
                    c.Name,
                    c.ActualScore ?? 0,
                    [.. c.Criteria.Select(i => new AssessorCriterionResponse(i.CriterionItemId, i.Name, i.IsSelected))]))]));
    }

    public async Task<AssessorSaveResult> SaveEntryAsync(Guid assessorId, Guid sampleTestId, Guid participantId, AssessorEntrySaveRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var access = await repository.GetEntryAccessAsync(sampleTestId, participantId, assessorId, cancellationToken);
        if (access != AssessorAccess.Granted)
        {
            return AssessorSaveResult.Of(access == AssessorAccess.Forbidden ? AssessorSaveOutcome.Forbidden : AssessorSaveOutcome.NotFound);
        }

        var entry = await repository.GetEntryAsync(sampleTestId, participantId, cancellationToken);
        if (entry is null)
        {
            return AssessorSaveResult.Of(AssessorSaveOutcome.NotFound);
        }

        if (entry.AssessmentSubmitted is not null)
        {
            return AssessorSaveResult.Of(AssessorSaveOutcome.Submitted);
        }

        var errors = Validate(entry, request);
        if (errors.Count > 0)
        {
            return new AssessorSaveResult(AssessorSaveOutcome.Invalid, errors);
        }

        // Legacy stamped the assessment with DateTime.Now (local time).
        var save = new AssessorEntrySave(
            sampleTestId,
            participantId,
            assessorId,
            request.Comment ?? string.Empty,
            request.OverallScore,
            [.. request.Categories.Select(c => new AssessorCategorySave(c.CategoryItemId, c.Score, c.CriterionItemIds ?? []))],
            timeProvider.GetLocalNow().DateTime);

        return AssessorSaveResult.Of(await repository.SaveEntryAsync(save, cancellationToken));
    }

    // The messages and the 1-5 scores are legacy's validators; the rest keeps the request to the test's own categories and criteria.
    private static List<AssessorValidationError> Validate(AssessorEntryEntity entry, AssessorEntrySaveRequest request)
    {
        List<AssessorValidationError> errors = [];

        if ((request.Comment ?? string.Empty).Length > MaxCommentLength)
        {
            errors.Add(new("Comment", CommentTooLongMessage));
        }

        if (!IsScore(request.OverallScore))
        {
            errors.Add(new("OverallScore", OverallScoreRequiredMessage));
        }

        var requested = request.Categories ?? [];
        var byId = requested.GroupBy(c => c.CategoryItemId).ToDictionary(g => g.Key, g => g.ToList());
        if (byId.Values.Any(group => group.Count > 1) || byId.Keys.Any(id => entry.Categories.TrueForAll(c => c.CategoryItemId != id)))
        {
            errors.Add(new("Categories", MismatchMessage));
        }

        for (var i = 0; i < entry.Categories.Count; i++)
        {
            var category = entry.Categories[i];
            if (!byId.TryGetValue(category.CategoryItemId, out var matches) || !IsScore(matches[0].Score))
            {
                errors.Add(new($"Categories[{i}].Score", CategoryScoreRequiredMessage));
                continue;
            }

            var known = category.Criteria.Select(c => c.CriterionItemId).ToHashSet();
            if ((matches[0].CriterionItemIds ?? []).Any(id => !known.Contains(id)) && errors.TrueForAll(e => e.Key != "Categories"))
            {
                errors.Add(new("Categories", MismatchMessage));
            }
        }

        return errors;
    }

    private static bool IsScore(int score) => score is >= MinScore and <= MaxScore;

    public static string DistributionTitle(string reference, string suffix, string scheduleCode, string schemeIdentifier, string schemeName) =>
        $"{reference}{suffix}/{scheduleCode}: {schemeIdentifier} {schemeName}";

    public static string YearMonthTitle(int monthId, int yearId) =>
        $"{CultureInfo.GetCultureInfo("en-GB").DateTimeFormat.GetMonthName(Math.Clamp(monthId, 1, 12))} {yearId.ToString(CultureInfo.InvariantCulture)}";
}
