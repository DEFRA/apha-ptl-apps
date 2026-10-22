using PTL.Contracts.Distribution;
using PTL.Core.Scheme;

namespace PTL.Core.Distribution;

public enum TestConsultantAccess
{
    Granted,
    NotFound,

    // The user is not one of the scheme's Test Consultants (legacy redirected to AccessDenied.aspx),
    // or is an inactive Test Consultant, who must not access Test Consultant functionality.
    Forbidden,
}

public sealed record TestConsultantResult<T>(TestConsultantAccess Access, T? Value = default);

public static class TestConsultantResult
{
    public static TestConsultantResult<T> Of<T>(T value) => new(TestConsultantAccess.Granted, value);

    public static TestConsultantResult<T> NotFound<T>() => new(TestConsultantAccess.NotFound);

    public static TestConsultantResult<T> Forbidden<T>() => new(TestConsultantAccess.Forbidden);
}

public sealed record TestConsultantExportFile(string FileName, byte[] Content);

public interface ITestConsultantTabulationService
{
    Task<TestConsultantResult<TestConsultantDistributionsResponse>> GetDistributionsAsync(Guid testConsultantId, CancellationToken cancellationToken = default);

    Task<TestConsultantResult<TestConsultantTabulationResponse>> GetTabulationAsync(
        Guid testConsultantId, Guid monthlyDistributionSchemeId, TestConsultantTabulationFilter filter, CancellationToken cancellationToken = default);

    Task<TestConsultantResult<TestConsultantExportFile>> ExportAsync(
        Guid testConsultantId, Guid monthlyDistributionSchemeId, TestConsultantTabulationFilter filter, CancellationToken cancellationToken = default);

    Task<TestConsultantResult<TestConsultantTabulationSaveResult>> SaveCommentsAsync(
        Guid testConsultantId, Guid monthlyDistributionSchemeId, TestConsultantTabulationSaveRequest request, CancellationToken cancellationToken = default);
}

// The Test Consultant side of the comments: legacy Test Consultant/MenuTestConsultants.aspx and Tabulation.aspx.
// Unlike the View Comments page (Scheduling staff, audited with a reason) the Test Consultant only ever sees the
// schemes they are allocated to, and their own Save / Save and Submit.
public sealed class TestConsultantTabulationService(
    ITestConsultantTabulationRepository repository,
    IDistributionRepository distributionRepository) : ITestConsultantTabulationService
{
    public const string CommentsKey = "Comments";
    public const string CommentsRequiredMessage = "Comments required.";
    public const string CommentsTooLongMessage = "Comments must not exceed 50,000 characters. Note that rich text such as font changes and bullet points will artificially count towards this character limit.";
    public const string RequiredStatus = "Awaiting comments";
    public const string SubmittedStatus = "Comments submitted";

    private const int MaxCommentsLength = 50000;
    private const string SavedAuditReason = "Test Consultant saved draft comments.";
    private const string SubmittedAuditReason = "Test Consultant submitted comments for sign-off.";

    // fldAuditWho is varchar(50).
    private const int AuditWhoLength = 50;

    public async Task<TestConsultantResult<TestConsultantDistributionsResponse>> GetDistributionsAsync(Guid testConsultantId, CancellationToken cancellationToken = default)
    {
        if (await repository.IsInactiveAsync(testConsultantId, cancellationToken))
        {
            return TestConsultantResult.Forbidden<TestConsultantDistributionsResponse>();
        }

        var distributions = await repository.GetDistributionsAsync(testConsultantId, cancellationToken);
        return TestConsultantResult.Of(new TestConsultantDistributionsResponse(
            [.. distributions.Select(d => new TestConsultantDistributionResponse(
                d.MonthlyDistributionSchemeId,
                d.YearId,
                d.MonthId,
                $"{d.DistributionReference}{d.DistributionReferenceSuffix}/{d.ScheduleCode}: {d.SchemeIdentifier} {d.SchemeName}",
                d.TargetDate,
                d.IsCommentSubmitted,
                d.IsCommentSubmitted ? SubmittedStatus : RequiredStatus))]));
    }

    public async Task<TestConsultantResult<TestConsultantTabulationResponse>> GetTabulationAsync(
        Guid testConsultantId, Guid monthlyDistributionSchemeId, TestConsultantTabulationFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var loaded = await LoadAsync(testConsultantId, monthlyDistributionSchemeId, cancellationToken);
        return loaded.Access == TestConsultantAccess.Granted
            ? TestConsultantResult.Of(TestConsultantTabulationBuilder.Build(loaded.Value!.Results, loaded.Value.Full, filter))
            : new TestConsultantResult<TestConsultantTabulationResponse>(loaded.Access);
    }

    public async Task<TestConsultantResult<TestConsultantExportFile>> ExportAsync(
        Guid testConsultantId, Guid monthlyDistributionSchemeId, TestConsultantTabulationFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var loaded = await LoadAsync(testConsultantId, monthlyDistributionSchemeId, cancellationToken);
        if (loaded.Access != TestConsultantAccess.Granted)
        {
            return new TestConsultantResult<TestConsultantExportFile>(loaded.Access);
        }

        var response = TestConsultantTabulationBuilder.Build(loaded.Value!.Results, loaded.Value.Full, filter);
        var tables = response.FullTables.Concat(response.FullRatings is null ? [] : [response.FullRatings])
            .Concat(response.ParticipantInformation is null ? [] : [response.ParticipantInformation]);

        // Legacy: {reference}{suffix}_{schedule code}_{scheme identifier}.
        var fileName = $"{loaded.Value.Full.Source.Distribution.Replace('/', '_')}_{loaded.Value.Full.Source.SchemeIdentifier}.xlsx";
        return TestConsultantResult.Of(new TestConsultantExportFile(fileName, TabulationWorkbookWriter.Write(tables)));
    }

    public async Task<TestConsultantResult<TestConsultantTabulationSaveResult>> SaveCommentsAsync(
        Guid testConsultantId, Guid monthlyDistributionSchemeId, TestConsultantTabulationSaveRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var state = await repository.GetSchemeStateAsync(monthlyDistributionSchemeId, cancellationToken);
        if (state is null)
        {
            return TestConsultantResult.NotFound<TestConsultantTabulationSaveResult>();
        }

        if (!state.IsAllocated(testConsultantId) || await repository.IsInactiveAsync(testConsultantId, cancellationToken))
        {
            return TestConsultantResult.Forbidden<TestConsultantTabulationSaveResult>();
        }

        // Submitted comments are locked for good - the page only offers Back, and the server refuses a direct post.
        if (state.IsCommentSubmitted)
        {
            return Saved(new TestConsultantTabulationSaveResult(false, true, new Dictionary<string, string>()));
        }

        var posted = request.Comments ?? string.Empty;
        if (!SchemeInstructions.HasContent(posted))
        {
            return Saved(Invalid(CommentsRequiredMessage));
        }

        if (posted.Length > MaxCommentsLength)
        {
            return Saved(Invalid(CommentsTooLongMessage));
        }

        var comments = SchemeInstructions.Sanitise(posted);
        var commentsChanged = !string.Equals(comments, state.Comments, StringComparison.Ordinal);
        if (!commentsChanged && !request.Submit)
        {
            return Saved(new TestConsultantTabulationSaveResult(true, false, new Dictionary<string, string>()));
        }

        var name = await repository.GetTestConsultantNameAsync(testConsultantId, cancellationToken) ?? testConsultantId.ToString();
        if (name.Length > AuditWhoLength)
        {
            name = name[..AuditWhoLength];
        }

        // Legacy SetTestConsultantComments: the author only changes with the comments. Submitting always writes an audit
        // row, so the date and time of the submission are kept even when the text was already saved.
        await distributionRepository.UpdateTestConsultantCommentsAsync(
            monthlyDistributionSchemeId,
            comments,
            commentsChanged ? testConsultantId : state.AuthorId,
            request.Submit,
            new CommentsAudit(name, request.Submit ? SubmittedAuditReason : SavedAuditReason, comments, true, string.Empty, false),
            new CommentsAttachmentChanges([], []),
            cancellationToken);

        return Saved(new TestConsultantTabulationSaveResult(true, false, new Dictionary<string, string>()));
    }

    private async Task<TestConsultantResult<LoadedTabulations>> LoadAsync(
        Guid testConsultantId, Guid monthlyDistributionSchemeId, CancellationToken cancellationToken)
    {
        var state = await repository.GetSchemeStateAsync(monthlyDistributionSchemeId, cancellationToken);
        if (state is null)
        {
            return TestConsultantResult.NotFound<LoadedTabulations>();
        }

        if (!state.IsAllocated(testConsultantId) || await repository.IsInactiveAsync(testConsultantId, cancellationToken))
        {
            return TestConsultantResult.Forbidden<LoadedTabulations>();
        }

        var results = await repository.GetTabulationAsync(monthlyDistributionSchemeId, false, cancellationToken);
        var full = await repository.GetTabulationAsync(monthlyDistributionSchemeId, true, cancellationToken);
        return results is null || full is null
            ? TestConsultantResult.NotFound<LoadedTabulations>()
            : TestConsultantResult.Of(new LoadedTabulations(results, full));
    }

    private static TestConsultantResult<TestConsultantTabulationSaveResult> Saved(TestConsultantTabulationSaveResult result) =>
        TestConsultantResult.Of(result);

    private static TestConsultantTabulationSaveResult Invalid(string message) =>
        new(false, false, new Dictionary<string, string> { [CommentsKey] = message });

    // The legacy page fetched the results tabulation and the full tabulation separately (FetchMonthlyDistributionScheme(id, False/True)).
    private sealed record LoadedTabulations(TestConsultantTabulationEntity Results, TestConsultantTabulationEntity Full);
}
