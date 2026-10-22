using PTL.Contracts.Distribution;
using PTL.Core.Scheme;

namespace PTL.Core.Distribution;

// Ports legacy Test Consultant/Tabulation.aspx.vb: the tabulation tables are drawn by TabulationReportBuilder (the
// same grids as the Tabulation page, which already ports the legacy BuildTables), this adds the Test Consultant
// extras - the sample filter (HiddenColumnBlocks), the participant information table, the sample information tab
// and the state of the comments editor.
public static class TestConsultantTabulationBuilder
{
    private const string NonBreakingSpace = "\u00a0";

    public static TestConsultantTabulationResponse Build(
        TestConsultantTabulationEntity results, TestConsultantTabulationEntity full, TestConsultantTabulationFilter filter)
    {
        ArgumentNullException.ThrowIfNull(results);
        ArgumentNullException.ThrowIfNull(full);
        ArgumentNullException.ThrowIfNull(filter);

        var resultsReport = TabulationReportBuilder.Build(Filter(results.Source, filter), Guid.Empty, Guid.Empty);
        var fullReport = TabulationReportBuilder.Build(Filter(full.Source, filter), Guid.Empty, Guid.Empty);

        return new TestConsultantTabulationResponse(
            resultsReport.TabulationId,
            resultsReport.Title,
            resultsReport.DistributionDateText,
            resultsReport.StandardTabulationTextHtml,
            resultsReport.CommentsHtml,
            HasMethodItems(full.Source),
            SampleNumbers(full.Source),
            resultsReport.Tables,
            resultsReport.Ratings,
            fullReport.Tables,
            fullReport.Ratings,
            BuildParticipantInformation(full.Participants),
            [.. full.SampleDetails.Select(s => new TestConsultantSampleInformationResponse(s.SampleNumber, s.TabulationDescription, s.TestConsultantsDescription))],
            BuildComments(results));
    }

    // Legacy list of the check boxes: every sample number of the distribution, in order.
    public static IReadOnlyList<string> SampleNumbers(TabulationSource source) =>
        [.. source.Tests.SelectMany(t => t.Samples).Select(s => s.SampleNumber).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];

    public static bool HasMethodItems(TabulationSource source) => source.Tests.Exists(t => t.MethodItems.Count > 0);

    // A copy of the source without the columns the Test Consultant un-ticked. Legacy kept a test's table when all
    // its samples were hidden; here a test with no sample left is not drawn, as on the Tabulation page.
    internal static TabulationSource Filter(TabulationSource source, TestConsultantTabulationFilter filter)
    {
        if (filter.HiddenSamples.Count == 0 && !filter.HideMethodItems)
        {
            return source;
        }

        var hidden = new HashSet<string>(filter.HiddenSamples, StringComparer.Ordinal);
        var filtered = new TabulationSource
        {
            TabulationId = source.TabulationId,
            DistributionTabulationId = source.DistributionTabulationId,
            Distribution = source.Distribution,
            SchemeIdentifier = source.SchemeIdentifier,
            SchemeName = source.SchemeName,
            TabulationName = source.TabulationName,
            RequiresAssessment = source.RequiresAssessment,
            Comments = source.Comments,
            TestConsultantComments = source.TestConsultantComments,
            TestConsultantCommentsAuthor = source.TestConsultantCommentsAuthor,
            TestConsultantCommentsDept = source.TestConsultantCommentsDept,
            DistributionDate = source.DistributionDate,
            HasRatings = source.HasRatings,
            StandardTabulationText = source.StandardTabulationText,
        };

        foreach (var test in source.Tests)
        {
            var copy = new TabulationSourceTest { TestId = test.TestId, Name = test.Name, IsAssessmentSubmitted = test.IsAssessmentSubmitted };
            copy.Samples.AddRange(test.Samples.Where(s => !hidden.Contains(s.SampleNumber)));
            copy.ResultItems.AddRange(test.ResultItems);
            copy.CategoryItems.AddRange(test.CategoryItems);
            if (!filter.HideMethodItems)
            {
                copy.MethodItems.AddRange(test.MethodItems);
            }

            filtered.Tests.Add(copy);
        }

        filtered.Participants.AddRange(source.Participants);
        foreach (var (itemId, result) in source.IntendedMethodResults)
        {
            filtered.IntendedMethodResults[itemId] = result;
        }

        foreach (var (key, result) in source.IntendedResultItemResults)
        {
            filtered.IntendedResultItemResults[key] = result;
        }

        return filtered;
    }

    // Legacy only drew this table on the full tabulation, and the filter never applied to it.
    private static TabulationTableResponse? BuildParticipantInformation(List<TestConsultantParticipantInformation> participants)
    {
        if (participants.Count == 0)
        {
            return null;
        }

        var rows = new List<TabulationGridRowResponse>
        {
            new(
                [
                    new("Participant", 1, 1, TabulationReportBuilder.Header1, false),
                    new("Extra Documents Returned", 1, 1, TabulationReportBuilder.Header1, false),
                    new("Deadline Extension Reason", 1, 1, TabulationReportBuilder.Header1, false),
                ],
                true),
        };

        rows.AddRange(participants.Select(p => new TabulationGridRowResponse(
            [
                new(p.LabCode + NonBreakingSpace, 1, 1, TabulationReportBuilder.Cell, false),
                new(p.ExtraDocumentsReturned ? "Yes" : "No", 1, 1, TabulationReportBuilder.Cell, false),
                new(NonBreakingSpace + p.DeadlineChangeReason, 1, 1, TabulationReportBuilder.Cell, false),
            ],
            false)));

        return new TabulationTableResponse("Participant information", rows, null);
    }

    private static TestConsultantCommentsStateResponse BuildComments(TestConsultantTabulationEntity results)
    {
        var source = results.Source;
        var hasComments = SchemeInstructions.HasContent(source.TestConsultantComments);
        var html = hasComments ? SchemeInstructions.Sanitise(source.TestConsultantComments) : string.Empty;

        if (!results.IsCommentSubmitted)
        {
            return new TestConsultantCommentsStateResponse(false, "Add your comments here:", html, hasComments);
        }

        var author = source.TestConsultantCommentsAuthor.Trim();
        var heading = !hasComments
            ? "No comments were submitted before the deadline"
            : author.Length == 0 ? "Comments:" : $"Comments provided by {author}:";
        return new TestConsultantCommentsStateResponse(true, heading, html, hasComments);
    }
}
