using System.Globalization;
using PTL.Contracts.Distribution;
using PTL.Core.Scheme;

namespace PTL.Core.Distribution;

// Ports the table building in legacy Scheduling/Tabulation.aspx.vb and PublishedTabulation.aspx.vb
// (BuildTables, the Write*Columns print data for TabulationTables.js) and the ratings arithmetic in
// ResultsTabulation/Published Tabulation.vb (CalculateSampleMean, CalculateNumberReportingIntendedResult,
// CalculateSamplePassRate, CalculateNoOfParticipants). Legacy quirks are kept on purpose so the
// tables match the legacy output cell for cell.
public static class TabulationReportBuilder
{
    public const string Header1 = "Header1";
    public const string Header2 = "Header2";
    public const string Cell = "Cell";
    public const string Plain = "Plain";

    private const int NotScored = -2;
    private const string NotScoredText = "Not Scored";
    private const string NotTestedText = "Not Tested";
    private const string NonBreakingSpace = "\u00a0";

    private static readonly CultureInfo UkCulture = CultureInfo.GetCultureInfo("en-GB");

    // Legacy AssessmentScoreTexts enum names with "_" replaced by a space.
    private static readonly string[] ScoreNames = ["Fail", "Borderline Fail", "Pass", "Good", "Excellent"];

    // Hard-coded in legacy PublishedTabulation.aspx.vb.
    public static readonly IReadOnlyList<string> SignatoryAddress =
    [
        "Animal and Plant Health Agency",
        "Quality Assurance Unit",
        "The Elms, College Road",
        "Sutton Bonington",
        "Loughborough",
        "Leicestershire",
        "LE12 5RB",
        "UK",
    ];

    public static TabulationReportResponse Build(TabulationSource source, Guid participantId, Guid viewerId)
    {
        ArgumentNullException.ThrowIfNull(source);

        var participantOptions = new List<TabulationFilterOptionResponse>();
        var viewerOptions = new List<TabulationFilterOptionResponse>();

        // Legacy: the participant/viewer drop-downs only exist when more than the intended results
        // can be seen; a single-participant tabulation never offers the viewer view.
        if (!source.IntendedResultsOnly)
        {
            if (source.SingleParticipantTabulation)
            {
                participantOptions.Add(new(Guid.Empty, "All Participants"));
                participantOptions.AddRange(source.Participants.Select(p => new TabulationFilterOptionResponse(p.ParticipantId, p.LabCode)));
            }
            else if (source.AvailableToViewers && source.Viewers.Count > 0)
            {
                viewerOptions.Add(new(Guid.Empty, "Normal View"));
                viewerOptions.AddRange(source.Viewers.Select(v => new TabulationFilterOptionResponse(v.ViewerId, v.Name)));
            }
        }

        var selectedParticipantId = participantOptions.Any(o => o.Value == participantId) ? participantId : Guid.Empty;
        var selectedViewer = viewerOptions.Any(o => o.Value == viewerId)
            ? source.Viewers.FirstOrDefault(v => v.ViewerId == viewerId)
            : null;

        var visibleParticipants = source.IntendedResultsOnly
            ? []
            : source.Participants
                .Where(p => selectedViewer is null || selectedViewer.ParticipantIds.Contains(p.ParticipantId))
                .Where(p => !source.SingleParticipantTabulation || selectedParticipantId == Guid.Empty || p.ParticipantId == selectedParticipantId)
                .ToList();

        var renderedTests = source.Tests.Where(IsRendered).ToList();
        var usedSamples = UsedSamples(source);

        return new TabulationReportResponse(
            source.TabulationId,
            source.IsPublished,
            source.DistributionTabulationId,
            $"FINAL REPORT for {source.Distribution} {source.SchemeIdentifier}: {source.SchemeName}",
            source.TabulationName,
            (source.IsPublished ? "Distribution Date: " : "Date Of Distribution: ") + FormatDate(source.DistributionDate),
            SchemeInstructions.Sanitise(source.StandardTabulationText),
            SchemeInstructions.Sanitise(source.Comments),
            SchemeInstructions.Sanitise(source.TestConsultantComments),
            CommentsTitle(source),
            source.ShowUkasLogo,
            source.IsPublished
                ? new TabulationSignatoryResponse(
                    "Version: " + FormatDate(source.SchemeVersionDate),
                    source.Signatory,
                    SignatoryAddress,
                    "Issued: " + FormatDate(source.CreationDate ?? default))
                : null,
            participantOptions,
            selectedParticipantId,
            viewerOptions,
            selectedViewer?.ViewerId ?? Guid.Empty,
            [.. renderedTests.Select(t => BuildTestTable(source, t, visibleParticipants))],
            source.HasRatings ? BuildRatingsTable(source, usedSamples, visibleParticipants) : null,
            source.Attachments.Select(a => new TabulationAttachmentResponse(a.AttachmentId, a.FileName)).FirstOrDefault(),
            BuildPrint(source, renderedTests, usedSamples, visibleParticipants),
            $"{source.Distribution} {source.SchemeIdentifier}: {source.SchemeName}");
    }

    private static bool IsRendered(TabulationSourceTest test) =>
        test.Samples.Count > 0 && (test.MethodItems.Count > 0 || test.ResultItems.Count > 0 || test.CategoryItems.Count > 0);

    private static string FormatDate(DateTime date) => date.ToString("dd/MM/yyyy", UkCulture);

    private static string CommentsTitle(TabulationSource source)
    {
        var title = "Comments ";
        if (source.TestConsultantCommentsAuthor.Length > 0)
        {
            title += "provided by " + source.TestConsultantCommentsAuthor;
        }

        if (source.TestConsultantCommentsDept.Length > 0)
        {
            title += ", " + source.TestConsultantCommentsDept;
        }

        return title + ":";
    }

    private static TabulationTableResponse BuildTestTable(
        TabulationSource source, TabulationSourceTest test, IReadOnlyList<TabulationSourceParticipant> participants)
    {
        var assess = source.RequiresAssessment;
        var results = test.ResultItems.Count;
        var categories = test.CategoryItems.Count;
        var rows = new List<TabulationGridRowResponse>();

        // Header row 1: test name, method items, then one spanning cell per sample.
        var header1 = new List<TabulationCellResponse> { new(test.Name, 1, 2, Header1, false) };
        header1.AddRange(test.MethodItems.Select(m => new TabulationCellResponse(m.Name, 1, 2, Header2, false)));
        if (assess ? results > 0 || categories > 0 : results > 0)
        {
            var span = assess ? results + categories + 2 : results;
            header1.AddRange(test.Samples.Select(s => new TabulationCellResponse(s.SampleNumber, Span(span), 1, Header1, false)));
        }

        rows.Add(new(header1, true));

        // Header row 2: result items, then (assessment schemes) category items and the two assessment columns.
        var header2 = new List<TabulationCellResponse>();
        foreach (var _ in test.Samples)
        {
            header2.AddRange(test.ResultItems.Select(r => new TabulationCellResponse(r.Name, 1, 1, Header2, false)));
            if (assess)
            {
                header2.AddRange(test.CategoryItems.Select(c => new TabulationCellResponse(c.Name, 1, 1, Header2, false)));
                header2.Add(new("Assessment comment", 1, 1, Header2, false));
                header2.Add(new("Overall Score", 1, 1, Header2, false));
            }
        }

        rows.Add(new(header2, true));

        // Intended results: sample descriptions, then the intended values.
        var intended1 = new List<TabulationCellResponse> { new("Intended", 1, 2, Cell, false) };
        intended1.AddRange(test.MethodItems.Select(_ => new TabulationCellResponse(string.Empty, 1, 1, Plain, false)));
        var intendedSpan = results + (assess ? categories + 2 : 0);
        intended1.AddRange(test.Samples.Select(s => new TabulationCellResponse(s.TabulationDescription, Span(intendedSpan), 1, Cell, false)));
        rows.Add(new(intended1, false));

        var intended2 = new List<TabulationCellResponse>();
        intended2.AddRange(test.MethodItems.Select(m => new TabulationCellResponse(IntendedMethod(source, m.ItemId), 1, 1, Cell, false)));
        foreach (var sample in test.Samples)
        {
            intended2.AddRange(test.ResultItems.Select(r => new TabulationCellResponse(IntendedResult(source, sample.SampleId, r.ItemId), 1, 1, Cell, false)));
            if (assess)
            {
                intended2.AddRange(Enumerable.Range(0, categories + 2).Select(_ => new TabulationCellResponse(string.Empty, 1, 1, Cell, false)));
            }
        }

        rows.Add(new(intended2, false));

        rows.AddRange(participants.Select(p => new TabulationGridRowResponse(ParticipantCells(source, test, p), false)));

        return new TabulationTableResponse(
            test.Name,
            rows,
            assess && test.IsAssessmentSubmitted ? test.TestId : null);
    }

    private static List<TabulationCellResponse> ParticipantCells(TabulationSource source, TabulationSourceTest test, TabulationSourceParticipant participant)
    {
        var cells = new List<TabulationCellResponse> { new(participant.LabCode, 1, 1, Cell, false) };

        if (HasAnyResults(participant))
        {
            cells.AddRange(test.MethodItems.Select(m => ResultCell(participant.MethodResults.GetValueOrDefault(m.ItemId))));
            foreach (var sample in test.Samples)
            {
                cells.AddRange(SampleResultCells(source, test, participant, sample));
            }
        }
        else if (source.RequiresAssessment)
        {
            cells.AddRange(NoResultAssessmentCells(source, test, participant));
        }
        else
        {
            // Legacy adds the assessment column count here even for non-assessment schemes.
            var span = test.MethodItems.Count + ((test.ResultItems.Count + test.CategoryItems.Count + 2) * test.Samples.Count);
            cells.Add(new(participant.NilReturnReason, Span(span), 1, Cell, true));
        }

        return cells;
    }

    private static List<TabulationCellResponse> SampleResultCells(
        TabulationSource source, TabulationSourceTest test, TabulationSourceParticipant participant, TabulationSourceSample sample)
    {
        var cells = new List<TabulationCellResponse>();
        cells.AddRange(test.ResultItems.Select(r =>
            ResultCell(participant.ResultItemResults.GetValueOrDefault((sample.SampleId, r.ItemId)))));

        if (!source.RequiresAssessment)
        {
            return cells;
        }

        var assessment = Assessment(participant, sample.SampleId, test.TestId);
        if (!IsScored(source, assessment))
        {
            cells.Add(new(ScoreText(source, assessment), Span(test.CategoryItems.Count + 2), 1, Cell, false));
            return cells;
        }

        if (participant.CategoryResults.Count > 0)
        {
            cells.AddRange(test.CategoryItems.Select(c => ResultCell(ScreenCategoryText(source, participant, sample, c.ItemId))));
        }

        cells.Add(ResultCell(AssessmentComment(source, assessment)));
        cells.Add(ResultCell(ScoreText(source, assessment)));
        return cells;
    }

    private static List<TabulationCellResponse> NoResultAssessmentCells(
        TabulationSource source, TabulationSourceTest test, TabulationSourceParticipant participant)
    {
        var cells = new List<TabulationCellResponse>();
        if (test.MethodItems.Count > 0)
        {
            cells.Add(new(participant.NilReturnReason, test.MethodItems.Count, 1, Cell, true));
        }

        // Legacy only draws a cell for samples whose raw score is outside Fail..Excellent.
        var span = Span(test.ResultItems.Count + test.CategoryItems.Count + 2);
        foreach (var sample in test.Samples)
        {
            var assessment = Assessment(participant, sample.SampleId, test.TestId);
            if (!IsValidScore(assessment.Score))
            {
                cells.Add(new(ScoreText(source, assessment), span, 1, Cell, false));
            }
        }

        return cells;
    }

    private static TabulationCellResponse ResultCell(string? text) =>
        new(text ?? string.Empty, 1, 1, Cell, string.IsNullOrEmpty(text));

    // ASP.NET omitted a zero ColumnSpan, which the browser treats as 1.
    private static int Span(int span) => Math.Max(1, span);

    private static bool HasAnyResults(TabulationSourceParticipant participant) =>
        participant.MethodResults.Count > 0 || participant.ResultItemResults.Count > 0 || participant.CategoryResults.Count > 0;

    private static bool HasRatingResults(TabulationSourceParticipant participant) =>
        participant.MethodResults.Count > 0 || participant.ResultItemResults.Count > 0;

    private static string IntendedMethod(TabulationSource source, Guid methodItemId) =>
        source.IntendedMethodResults.GetValueOrDefault(methodItemId) ?? string.Empty;

    private static string IntendedResult(TabulationSource source, Guid sampleId, Guid resultItemId)
    {
        if (!source.IntendedResultItemResults.TryGetValue((sampleId, resultItemId), out var intended))
        {
            return string.Empty;
        }

        return string.IsNullOrEmpty(intended.Result2) ? intended.Result : $"{intended.Result} to {intended.Result2}";
    }

    // Legacy dereferenced a missing assessment row and failed; an absent row is shown as unaccounted.
    private static TabulationSourceAssessment Assessment(TabulationSourceParticipant participant, Guid sampleId, Guid testId) =>
        participant.Assessments.GetValueOrDefault((sampleId, testId)) ?? new TabulationSourceAssessment(0, string.Empty, null, null, null, null);

    private static bool IsValidScore(int score) => score is >= 1 and <= 5;

    // Draft: ResultsTabulation.ParticipantAssessmentInfo.IsScored also needs the test's assessment
    // submitted; the published copy only stores submitted assessments.
    internal static bool IsScored(TabulationSource source, TabulationSourceAssessment assessment) =>
        assessment.SubmissionDate is not null
        && (source.IsPublished || assessment.AssessmentSubmitted is not null)
        && IsValidScore(assessment.Score);

    internal static string ScoreText(TabulationSource source, TabulationSourceAssessment assessment)
    {
        if (assessment.SubmissionDate is not null)
        {
            if (!source.IsPublished && assessment.AssessmentSubmitted is null)
            {
                return "Submitted";
            }

            return IsValidScore(assessment.Score)
                ? $"{ScoreNames[assessment.Score - 1]} [{assessment.Score}/5]"
                : "Not Assessed";
        }

        if (assessment.NonSubmissionDate is not null)
        {
            return "Not Submitted";
        }

        return assessment.NonCapableDate is not null ? "Not In Repertoire" : "Unaccounted";
    }

    private static string AssessmentComment(TabulationSource source, TabulationSourceAssessment assessment) =>
        source.IsPublished || assessment.AssessmentSubmitted is not null ? assessment.Comment : string.Empty;

    private static string ScreenCategoryText(TabulationSource source, TabulationSourceParticipant participant, TabulationSourceSample sample, Guid categoryItemId)
    {
        if (!participant.CategoryResults.TryGetValue((sample.SampleId, categoryItemId), out var result))
        {
            return string.Empty;
        }

        // The draft page only shows the result recorded against this sample's own sample test.
        if (result.SampleTestId is not null && result.SampleTestId != sample.SampleTestId)
        {
            return string.Empty;
        }

        return CategoryText(source, result);
    }

    private static string CategoryText(TabulationSource source, TabulationSourceCategoryResult result) =>
        source.IsPublished || result.IsAssessmentSubmitted
            ? $"{string.Join(", ", result.Criteria).Trim()} [{result.Score}/5]"
            : string.Empty;

    private static List<TabulationSourceSample> UsedSamples(TabulationSource source)
    {
        var used = source.Tests.SelectMany(t => t.Samples).DistinctBy(s => s.SampleId).ToList();

        // Legacy SampleInfo.CompareTo is a binary comparison of the sample numbers.
        used.Sort((a, b) => string.CompareOrdinal(a.SampleNumber, b.SampleNumber));
        return used;
    }

    private static TabulationTableResponse BuildRatingsTable(
        TabulationSource source, IReadOnlyList<TabulationSourceSample> samples, IReadOnlyList<TabulationSourceParticipant> participants)
    {
        var rows = new List<TabulationGridRowResponse>();

        var header = new List<TabulationCellResponse> { new("QAU Ratings", 1, 1, Header1, false) };
        header.AddRange(samples.Select(s => new TabulationCellResponse(s.SampleNumber, 1, 1, Header2, false)));
        rows.Add(new(header, true));

        foreach (var participant in participants)
        {
            var cells = new List<TabulationCellResponse> { new(participant.LabCode, 1, 1, Cell, false) };
            cells.AddRange(samples.Select(s => HasRatingResults(participant)
                ? new TabulationCellResponse(Rating(participant, s.SampleId), 1, 1, Cell, false)
                : new TabulationCellResponse(string.Empty, 1, 1, Cell, true)));
            rows.Add(new(cells, false));
        }

        rows.Add(StatisticRow("Mean score of all laboratories: ", samples, s => SampleMeanText(source, s.SampleId)));
        rows.Add(StatisticRow("No. of labs reporting intended result: ", samples, s => NumberReportingText(source, s.SampleId)));
        rows.Add(StatisticRow("No. of Participants: ", samples, s => NumberOfParticipants(source, s.SampleId).ToString("N0", UkCulture)));
        rows.Add(StatisticRow("Percent Correct:", samples, s => PassRateText(source, s.SampleId)));

        return new TabulationTableResponse("QAU Ratings", rows, null);
    }

    private static TabulationGridRowResponse StatisticRow(string label, IReadOnlyList<TabulationSourceSample> samples, Func<TabulationSourceSample, string> value)
    {
        var cells = new List<TabulationCellResponse> { new(label, 1, 1, Cell, false) };
        cells.AddRange(samples.Select(s => new TabulationCellResponse(value(s), 1, 1, Cell, false)));
        return new(cells, false);
    }

    private static string Rating(TabulationSourceParticipant participant, Guid sampleId) =>
        participant.SampleRatings.GetValueOrDefault(sampleId) ?? string.Empty;

    // The statistics always use every participant, whatever participant/viewer filter is applied.
    internal static double CalculateSampleMean(TabulationSource source, Guid sampleId)
    {
        double sum = 0;
        var count = 0;
        var isScored = true;

        foreach (var participant in source.Participants.Where(p => p.ResultItemResults.Count > 0))
        {
            var rating = Rating(participant, sampleId);
            if (rating is NotScoredText or NotTestedText)
            {
                isScored &= rating != NotScoredText;
            }
            else if (double.TryParse(rating, NumberStyles.Float, UkCulture, out var value))
            {
                sum += value;
                count++;
            }
        }

        if (!isScored)
        {
            return NotScored;
        }

        return count != 0 ? sum / count : 0;
    }

    internal static int CalculateNumberReportingIntendedResult(TabulationSource source, Guid sampleId)
    {
        var count = 0;
        var isScored = true;

        foreach (var participant in source.Participants.Where(p => p.ResultItemResults.Count > 0))
        {
            var rating = Rating(participant, sampleId);
            if (rating is NotScoredText or NotTestedText)
            {
                isScored &= rating != NotScoredText;
            }
            else if (rating == "2")
            {
                count++;
            }
        }

        return isScored ? count : NotScored;
    }

    internal static int NumberOfParticipants(TabulationSource source, Guid sampleId) =>
        source.Participants.Count(p => p.ResultItemResults.Count > 0
            && Rating(p, sampleId) is "-1" or "0" or "1" or "2" or NotScoredText);

    internal static double CalculateSamplePassRate(TabulationSource source, Guid sampleId)
    {
        var participants = NumberOfParticipants(source, sampleId);
        var reporting = CalculateNumberReportingIntendedResult(source, sampleId);

        if (reporting == NotScored)
        {
            return NotScored;
        }

        return participants == 0 ? 0 : (double)reporting / participants;
    }

    private static string SampleMeanText(TabulationSource source, Guid sampleId)
    {
        var mean = CalculateSampleMean(source, sampleId);
        return IsNotScored(mean) ? NotScoredText : mean.ToString("N1", UkCulture);
    }

    private static string NumberReportingText(TabulationSource source, Guid sampleId)
    {
        var reporting = CalculateNumberReportingIntendedResult(source, sampleId);
        return reporting == NotScored ? NotScoredText : reporting.ToString("N0", UkCulture);
    }

    private static string PassRateText(TabulationSource source, Guid sampleId)
    {
        var rate = CalculateSamplePassRate(source, sampleId);
        return IsNotScored(rate) ? NotScoredText : (rate * 100).ToString("N1", UkCulture) + "%";
    }

    // NotScored is a sentinel stored in a double; the ratings are whole numbers, so a range compare is exact in practice.
    private static bool IsNotScored(double value) => Math.Abs(value - NotScored) < 0.0001;

    private static TabulationPrintResponse BuildPrint(
        TabulationSource source,
        IReadOnlyList<TabulationSourceTest> tests,
        IReadOnlyList<TabulationSourceSample> usedSamples,
        IReadOnlyList<TabulationSourceParticipant> participants)
    {
        var printParticipants = participants
            .Select(p => new TabulationPrintParticipantResponse(
                p.LabCode, source.RequiresAssessment || HasRatingResults(p), p.NilReturnReason))
            .ToList();

        var printTests = tests.Select(t => new TabulationPrintTestResponse(
            t.Name,
            [.. t.MethodItems.Select(m => new TabulationPrintMethodColumnResponse(
                m.Name,
                IntendedMethod(source, m.ItemId),
                [.. participants.Select(p => p.MethodResults.GetValueOrDefault(m.ItemId) ?? string.Empty)]))],
            [.. t.Samples.Select(s => new TabulationPrintSampleColumnResponse(
                s.SampleNumber, s.TabulationDescription, PrintResultColumns(source, t, s, participants)))])).ToList();

        var ratingColumns = source.HasRatings
            ? usedSamples.Select(s => new TabulationPrintRatingColumnResponse(
                s.SampleNumber,
                [.. participants.Select(p => p.SampleRatings.Count > 0 ? Rating(p, s.SampleId) : NonBreakingSpace)],
                SampleMeanText(source, s.SampleId),
                PassRateText(source, s.SampleId),
                NumberOfParticipants(source, s.SampleId).ToString("N0", UkCulture),
                NumberReportingText(source, s.SampleId))).ToList()
            : [];

        return new TabulationPrintResponse(printParticipants, printTests, ratingColumns);
    }

    private static List<TabulationPrintResultColumnResponse> PrintResultColumns(
        TabulationSource source, TabulationSourceTest test, TabulationSourceSample sample, IReadOnlyList<TabulationSourceParticipant> participants)
    {
        var columns = test.ResultItems.Select(r => new TabulationPrintResultColumnResponse(
            r.Name,
            IntendedResult(source, sample.SampleId, r.ItemId),
            [.. participants.Select(p => new TabulationPrintResultResponse(
                p.ResultItemResults.GetValueOrDefault((sample.SampleId, r.ItemId)) ?? string.Empty, 1))])).ToList();

        if (!source.RequiresAssessment)
        {
            return columns;
        }

        var categories = test.CategoryItems.Count;
        var assessments = participants.Select(p => (Participant: p, Assessment: Assessment(p, sample.SampleId, test.TestId))).ToList();

        // An unscored participant's text spans the category and assessment columns: the first
        // category column carries the span and the rest are covered (span 0).
        columns.AddRange(test.CategoryItems.Select((c, index) =>
        {
            var unscoredSpan = index > 0 ? 0 : categories + 2;
            return new TabulationPrintResultColumnResponse(
                c.Name,
                string.Empty,
                [.. assessments.Select(a => IsScored(source, a.Assessment)
                    ? new TabulationPrintResultResponse(PrintCategoryText(source, a.Participant, sample.SampleId, c.ItemId), 1)
                    : new TabulationPrintResultResponse(ScoreText(source, a.Assessment), unscoredSpan))]);
        }));

        var unscoredCommentSpan = categories > 0 ? 0 : 2;
        columns.Add(new TabulationPrintResultColumnResponse(
            "Assessment Comment",
            string.Empty,
            [.. assessments.Select(a => IsScored(source, a.Assessment)
                ? new TabulationPrintResultResponse(AssessmentComment(source, a.Assessment), 1)
                : new TabulationPrintResultResponse(ScoreText(source, a.Assessment), unscoredCommentSpan))]));

        columns.Add(new TabulationPrintResultColumnResponse(
            "Overall score",
            string.Empty,
            [.. assessments.Select(a => new TabulationPrintResultResponse(ScoreText(source, a.Assessment), IsScored(source, a.Assessment) ? 1 : 0))]));

        return columns;
    }

    private static string PrintCategoryText(TabulationSource source, TabulationSourceParticipant participant, Guid sampleId, Guid categoryItemId) =>
        participant.CategoryResults.TryGetValue((sampleId, categoryItemId), out var result) ? CategoryText(source, result) : string.Empty;
}
