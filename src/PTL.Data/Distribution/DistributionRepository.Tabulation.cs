using Dapper;
using PTL.Core.Distribution;
using Rows = PTL.Data.Distribution.TabulationReportRows;

namespace PTL.Data.Distribution;

// Tabulation reads. @ReferenceDate is left to its NULL default - the procedures then use today's
// date (legacy only overrides it from the ResultsTestDate test-harness appSetting).
public sealed partial class DistributionRepository
{
    public async Task<TabulationSource?> GetTabulationSourceAsync(Guid tabulationId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        using var results = await connection.QueryMultipleAsync(
            "EXEC dbo.spgDistributionTabulation @DistributionTabulationId = @TabulationId",
            new { TabulationId = tabulationId });

        var header = await results.ReadFirstOrDefaultAsync<Rows.DraftHeader>();
        if (header is null)
        {
            return null;
        }

        var source = new TabulationSource
        {
            TabulationId = header.DistributionTabulationId,
            DistributionTabulationId = header.DistributionTabulationId,
            Distribution = $"{header.DistributionReference}{header.DistributionReferenceSuffix}/{header.ScheduleCode}",
            SchemeIdentifier = header.SchemeIdentifier ?? string.Empty,
            SchemeName = header.SchemeName ?? string.Empty,
            TabulationName = header.TabulationName ?? string.Empty,
            RequiresAssessment = header.RequiresAssessment ?? false,
            Comments = header.Comments ?? string.Empty,
            TestConsultantComments = header.TestConsultantComments ?? string.Empty,
            TestConsultantCommentsAuthor = header.TestConsultantCommentsAuthor ?? string.Empty,
            TestConsultantCommentsDept = header.TestConsultantCommentsDept ?? string.Empty,
            DistributionDate = header.DistributionDate ?? default,
            ShowUkasLogo = header.ShowUkasLogo ?? false,
            SchemeVersionDate = header.SchemeVersionDate ?? default,
            HasRatings = header.HasRatings ?? false,
            IntendedResultsOnly = header.IntendedResultsOnly ?? false,
            SingleParticipantTabulation = header.SingleParticipantTabulation ?? false,
            AvailableToViewers = header.AvailableToViewers ?? false,
            StandardTabulationText = header.StandardTabulationText ?? string.Empty,
        };

        await ReadDraftTestsAsync(results, source);
        var participants = await ReadDraftParticipantsAsync(results, source);

        foreach (var row in await results.ReadAsync<Rows.DraftIntendedMethod>())
        {
            source.IntendedMethodResults.TryAdd(row.TestMethodItemId, row.IntendedResult ?? string.Empty);
        }

        foreach (var row in await results.ReadAsync<Rows.DraftIntendedResult>())
        {
            source.IntendedResultItemResults.TryAdd((row.SampleId, row.TestResultItemId), (row.IntendedResult ?? string.Empty, row.IntendedResult2 ?? string.Empty));
        }

        await ReadViewersAsync(results, source);

        foreach (var row in await results.ReadAsync<Rows.DraftAssessment>())
        {
            participants.GetValueOrDefault(row.ParticipantId)?.Assessments.TryAdd(
                (row.SampleId, row.TestId),
                new TabulationSourceAssessment(row.Score ?? 0, row.AssessmentComment ?? string.Empty, row.SubmissionDate, row.NonSubmissionDate, row.NonCapableDate, row.AssessmentSubmitted));
        }

        source.Attachments.AddRange((await results.ReadAsync<Rows.DraftAttachment>()).Select(a =>
            new TabulationSourceAttachment(a.MonthlyDistributionAttachmentId, a.FileName ?? string.Empty, a.ContentType ?? string.Empty, a.FileData ?? [])));

        return source;
    }

    public async Task<TabulationSource?> GetPublishedTabulationSourceAsync(Guid publishedTabulationId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        using var results = await connection.QueryMultipleAsync(
            "EXEC dbo.spgPublishedTabulation @PublishedTabulationId = @PublishedTabulationId",
            new { PublishedTabulationId = publishedTabulationId });

        // The procedure returns a single empty result set when nothing was found.
        var header = await results.ReadFirstOrDefaultAsync<Rows.PublishedHeader>();
        if (header is null)
        {
            return null;
        }

        var source = new TabulationSource
        {
            TabulationId = header.PublishedTabulationId,
            DistributionTabulationId = header.DistributionTabulationId,
            IsPublished = true,
            Distribution = header.Distribution ?? string.Empty,
            SchemeIdentifier = header.SchemeIdentifier ?? string.Empty,
            SchemeName = header.SchemeName ?? string.Empty,
            TabulationName = header.Name ?? string.Empty,
            RequiresAssessment = header.RequiresAssessment ?? false,
            Comments = header.SampleComments ?? string.Empty,
            TestConsultantComments = header.TestConsultantComments ?? string.Empty,
            TestConsultantCommentsAuthor = header.TestConsultantCommentsAuthor ?? string.Empty,
            TestConsultantCommentsDept = header.TestConsultantCommentsDept ?? string.Empty,
            DistributionDate = header.DistributionDate ?? default,
            ShowUkasLogo = header.IsAccredited ?? false,
            SchemeVersionDate = header.SchemeVersionDate ?? default,
            HasRatings = header.HasRatings ?? false,
            IntendedResultsOnly = header.IntendedResultsOnly ?? false,
            SingleParticipantTabulation = header.SingleParticipantTabulation ?? false,
            AvailableToViewers = header.AvailableToViewers ?? false,
            StandardTabulationText = header.StandardTabulationText ?? string.Empty,
            Signatory = header.Signatory ?? string.Empty,
            CreationDate = header.CreationDate,
        };

        await ReadPublishedTestsAsync(results, source);
        var participants = await ReadPublishedParticipantsAsync(results, source);

        foreach (var row in await results.ReadAsync<Rows.PublishedIntendedMethod>())
        {
            source.IntendedMethodResults.TryAdd(row.PublishedTestMethodItemId, row.Result ?? string.Empty);
        }

        foreach (var row in await results.ReadAsync<Rows.PublishedIntendedResult>())
        {
            source.IntendedResultItemResults.TryAdd((row.PublishedSampleId, row.PublishedTestResultItemId), (row.Result ?? string.Empty, row.Result2 ?? string.Empty));
        }

        await ReadViewersAsync(results, source);

        foreach (var row in await results.ReadAsync<Rows.PublishedAssessment>())
        {
            participants.GetValueOrDefault(row.PublishedParticipantId)?.Assessments.TryAdd(
                (row.PublishedSampleId, row.PublishedTestId),
                new TabulationSourceAssessment(row.AssessmentScore ?? 0, row.AssessmentComment ?? string.Empty, row.SubmissionDate, row.NonSubmissionDate, row.NonCapableDate, null));
        }

        source.Attachments.AddRange((await results.ReadAsync<Rows.PublishedAttachment>()).Select(a =>
            new TabulationSourceAttachment(a.PublishedAttachmentId, a.FileName ?? string.Empty, a.ContentType ?? string.Empty, a.FileData ?? [])));

        return source;
    }

    public async Task<PublishedTabulationsSource?> GetPublishedTabulationsAsync(Guid tabulationId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        using var results = await connection.QueryMultipleAsync(
            "EXEC dbo.spgPublishedTabulations @DistributionTabulationId = @TabulationId",
            new { TabulationId = tabulationId });

        var header = await results.ReadFirstOrDefaultAsync<Rows.PublishedTabulationsHeader>();
        var rows = await results.ReadAsync<Rows.PublishedTabulationsRow>();
        if (header is null)
        {
            return null;
        }

        return new PublishedTabulationsSource(
            header.TabulationName ?? string.Empty,
            header.Distribution ?? string.Empty,
            header.SchemeIdentifier ?? string.Empty,
            header.SchemeName ?? string.Empty,
            [.. rows.Select(r => new PublishedTabulationSourceRow(r.PublishedTabulationId, r.Signatory ?? string.Empty, r.CreationDate))]);
    }

    public async Task<AssessmentScoreSource?> GetAssessmentScoresAsync(Guid testId, Guid tabulationId, bool isPublished, CancellationToken cancellationToken = default)
    {
        // Each branch is a complete, literal command text - nothing is built from a runtime value.
        var sql = isPublished
            ? "EXEC dbo.spgPublishedAssessmentScoreByTestId @TestId = @TestId, @TabulationId = @TabulationId"
            : "EXEC dbo.spgAssessmentScoreByTestId @TestId = @TestId, @TabulationId = @TabulationId";

        using var connection = connectionFactory.CreateConnection();
        using var results = await connection.QueryMultipleAsync(
            sql,
            new { TestId = testId, TabulationId = tabulationId });

        var header = await results.ReadFirstOrDefaultAsync<Rows.AssessmentScoreHeader>();
        var sampleNumbers = (await results.ReadAsync<string>()).ToList();
        var receipts = await results.ReadAsync<Rows.AssessmentScoreReceiptRow>();
        if (header is null)
        {
            return null;
        }

        return new AssessmentScoreSource(
            header.TabulationName ?? string.Empty,
            header.SchemeName ?? string.Empty,
            header.Distribution ?? string.Empty,
            header.TestName ?? string.Empty,
            header.AssessmentSubmitted,
            sampleNumbers,
            [.. receipts.Select(r => new AssessmentScoreReceipt(r.SubmissionDate, r.NonSubmissionDate, r.NonCapableDate, r.Score ?? 0, r.AssessmentDate))]);
    }

    private static async Task ReadDraftTestsAsync(SqlMapper.GridReader results, TabulationSource source)
    {
        var tests = new Dictionary<Guid, TabulationSourceTest>();
        foreach (var row in await results.ReadAsync<Rows.DraftTest>())
        {
            var test = new TabulationSourceTest { TestId = row.TestId, Name = row.TestType ?? string.Empty, IsAssessmentSubmitted = row.AssessmentSubmitted is not null };
            tests[row.TestId] = test;
            source.Tests.Add(test);
        }

        foreach (var row in await results.ReadAsync<Rows.DraftMethodItem>())
        {
            tests.GetValueOrDefault(row.TestId)?.MethodItems.Add(new(row.TestMethodItemId, row.TestMethodItemType ?? string.Empty));
        }

        foreach (var row in await results.ReadAsync<Rows.DraftResultItem>())
        {
            tests.GetValueOrDefault(row.TestId)?.ResultItems.Add(new(row.TestResultItemId, row.TestResultItemType ?? string.Empty));
        }

        foreach (var row in await results.ReadAsync<Rows.DraftCategoryItem>())
        {
            tests.GetValueOrDefault(row.TestId)?.CategoryItems.Add(new(row.CategoryItemId, row.Name ?? string.Empty));
        }

        // The scheme's criterion items are not shown on the page.
        _ = await results.ReadAsync();

        foreach (var row in await results.ReadAsync<Rows.DraftSample>())
        {
            tests.GetValueOrDefault(row.TestId)?.Samples.Add(new(row.SampleId, row.SampleTestId, row.SampleNumber ?? string.Empty, row.TabulationDescription ?? string.Empty));
        }
    }

    private static async Task<Dictionary<Guid, TabulationSourceParticipant>> ReadDraftParticipantsAsync(SqlMapper.GridReader results, TabulationSource source)
    {
        var participants = new Dictionary<Guid, TabulationSourceParticipant>();
        foreach (var row in await results.ReadAsync<Rows.DraftParticipant>())
        {
            var participant = new TabulationSourceParticipant { ParticipantId = row.ParticipantId, LabCode = row.LabCode ?? string.Empty, NilReturnReason = row.NilReturnReason ?? string.Empty };
            participants[row.ParticipantId] = participant;
            source.Participants.Add(participant);
        }

        foreach (var row in await results.ReadAsync<Rows.DraftRating>())
        {
            participants.GetValueOrDefault(row.ParticipantId)?.SampleRatings.TryAdd(row.SampleId, row.Rating ?? string.Empty);
        }

        foreach (var row in await results.ReadAsync<Rows.DraftParticipantMethod>())
        {
            participants.GetValueOrDefault(row.ParticipantId)?.MethodResults.TryAdd(row.TestMethodItemId, row.AmendedResult ?? string.Empty);
        }

        foreach (var row in await results.ReadAsync<Rows.DraftParticipantResult>())
        {
            participants.GetValueOrDefault(row.ParticipantId)?.ResultItemResults.TryAdd((row.SampleId, row.TestResultItemId), row.AmendedResult ?? string.Empty);
        }

        foreach (var row in await results.ReadAsync<Rows.DraftParticipantCategory>())
        {
            participants.GetValueOrDefault(row.ParticipantId)?.CategoryResults.TryAdd(
                (row.SampleId, row.CategoryItemId),
                new TabulationSourceCategoryResult { SampleTestId = row.SampleTestId, Score = row.CategoryScore ?? 0, IsAssessmentSubmitted = row.AssessmentSubmitted is not null });
        }

        foreach (var row in await results.ReadAsync<Rows.DraftParticipantCriterion>())
        {
            if (participants.GetValueOrDefault(row.ParticipantId)?.CategoryResults.GetValueOrDefault((row.SampleId, row.CategoryItemId)) is { } category)
            {
                category.Criteria.Add(row.Name ?? string.Empty);
            }
        }

        return participants;
    }

    private static async Task ReadPublishedTestsAsync(SqlMapper.GridReader results, TabulationSource source)
    {
        var tests = new Dictionary<Guid, TabulationSourceTest>();
        foreach (var row in await results.ReadAsync<Rows.PublishedTest>())
        {
            // Legacy PublishedTabulation.aspx.vb links every test of an assessment scheme to its results.
            var test = new TabulationSourceTest { TestId = row.PublishedTestId, Name = row.Name ?? string.Empty, IsAssessmentSubmitted = true };
            tests[row.PublishedTestId] = test;
            source.Tests.Add(test);
        }

        foreach (var row in await results.ReadAsync<Rows.PublishedSample>())
        {
            tests.GetValueOrDefault(row.PublishedTestId)?.Samples.Add(new(row.PublishedSampleId, null, row.SampleNumber ?? string.Empty, row.TabulationDescription ?? string.Empty));
        }

        foreach (var row in await results.ReadAsync<Rows.PublishedMethodItem>())
        {
            tests.GetValueOrDefault(row.PublishedTestId)?.MethodItems.Add(new(row.PublishedTestMethodItemId, row.Name ?? string.Empty));
        }

        foreach (var row in await results.ReadAsync<Rows.PublishedResultItem>())
        {
            tests.GetValueOrDefault(row.PublishedTestId)?.ResultItems.Add(new(row.PublishedTestResultItemId, row.Name ?? string.Empty));
        }

        foreach (var row in await results.ReadAsync<Rows.PublishedCategoryItem>())
        {
            tests.GetValueOrDefault(row.PublishedTestId)?.CategoryItems.Add(new(row.PublishedCategoryItemId, row.Name ?? string.Empty));
        }

        // The tabulation's criterion items are not shown on the page.
        _ = await results.ReadAsync();
    }

    private static async Task<Dictionary<Guid, TabulationSourceParticipant>> ReadPublishedParticipantsAsync(SqlMapper.GridReader results, TabulationSource source)
    {
        var participants = new Dictionary<Guid, TabulationSourceParticipant>();
        foreach (var row in await results.ReadAsync<Rows.PublishedParticipant>())
        {
            var participant = new TabulationSourceParticipant { ParticipantId = row.PublishedParticipantId, LabCode = row.LabCode ?? string.Empty, NilReturnReason = row.NilReturnReason ?? string.Empty };
            participants[row.PublishedParticipantId] = participant;
            source.Participants.Add(participant);
        }

        foreach (var row in await results.ReadAsync<Rows.PublishedRating>())
        {
            participants.GetValueOrDefault(row.PublishedParticipantId)?.SampleRatings.TryAdd(row.PublishedSampleId, row.Rating ?? string.Empty);
        }

        foreach (var row in await results.ReadAsync<Rows.PublishedParticipantMethod>())
        {
            participants.GetValueOrDefault(row.PublishedParticipantId)?.MethodResults.TryAdd(row.PublishedTestMethodItemId, row.Result ?? string.Empty);
        }

        foreach (var row in await results.ReadAsync<Rows.PublishedParticipantResult>())
        {
            participants.GetValueOrDefault(row.PublishedParticipantId)?.ResultItemResults.TryAdd((row.PublishedSampleId, row.PublishedTestResultItemId), row.Result ?? string.Empty);
        }

        foreach (var row in await results.ReadAsync<Rows.PublishedParticipantCategory>())
        {
            participants.GetValueOrDefault(row.PublishedParticipantId)?.CategoryResults.TryAdd(
                (row.PublishedSampleId, row.PublishedCategoryItemId),
                new TabulationSourceCategoryResult { Score = row.Score ?? 0, IsAssessmentSubmitted = true });
        }

        foreach (var row in await results.ReadAsync<Rows.PublishedParticipantCriterion>())
        {
            if (participants.GetValueOrDefault(row.PublishedParticipantId)?.CategoryResults.GetValueOrDefault((row.PublishedSampleId, row.PublishedCategoryItemId)) is { } category)
            {
                category.Criteria.Add(row.Name ?? string.Empty);
            }
        }

        return participants;
    }

    private static async Task ReadViewersAsync(SqlMapper.GridReader results, TabulationSource source)
    {
        var viewers = new Dictionary<Guid, TabulationSourceViewer>();
        foreach (var row in (await results.ReadAsync<Rows.Viewer>()).DistinctBy(r => r.ViewerId))
        {
            var viewer = new TabulationSourceViewer { ViewerId = row.ViewerId, Name = row.Name ?? string.Empty };
            viewers[row.ViewerId] = viewer;
            source.Viewers.Add(viewer);
        }

        foreach (var row in await results.ReadAsync<Rows.ViewerParticipant>())
        {
            viewers.GetValueOrDefault(row.ViewerId)?.ParticipantIds.Add(row.ParticipantId);
        }
    }
}
