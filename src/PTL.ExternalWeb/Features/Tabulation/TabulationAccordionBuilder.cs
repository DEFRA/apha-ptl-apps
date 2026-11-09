using PTL.Contracts.Distribution;

namespace PTL.ExternalWeb.Features.Tabulation;

// Legacy TabulationTableHelper.BuildTables: per test an optional "Method items" accordion, one accordion per sample, then
// the ratings. Sections come only from what the scheme's report holds, never from a fixed scheme layout.
public static class TabulationAccordionBuilder
{
    private const string Header1 = "Header1";
    private const string Header2 = "Header2";
    private const string Cell = "Cell";

    // Null when the report carries no print data (or it does not line up with the tables), so the page keeps the one-table-per-test layout.
    public static IReadOnlyList<TabulationTestSections>? Build(TabulationReportResponse report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var tests = report.Print.Tests;
        if (tests.Count == 0 || tests.Count != report.Tables.Count)
        {
            return null;
        }

        var built = tests
            .Select((test, index) => BuildTest(test, report.Tables[index].AssessmentTestId, report.Print.Participants))
            .Where(test => test.Sections.Count > 0)
            .ToList();

        return built.Count == 0 ? null : built;
    }

    private static TabulationTestSections BuildTest(
        TabulationPrintTestResponse test, Guid? assessmentTestId, IReadOnlyList<TabulationPrintParticipantResponse> participants)
    {
        var sections = new List<TabulationAccordionSection>();

        if (test.MethodColumns.Count > 0)
        {
            sections.Add(new TabulationAccordionSection("Method items", MethodTable(test, participants)));
        }

        // A sample with no result columns has nothing to tabulate.
        sections.AddRange(test.SampleColumns
            .Where(sample => sample.ResultColumns.Count > 0)
            .Select(sample => new TabulationAccordionSection(sample.SampleNumber, SampleTable(test, sample, participants))));

        return new TabulationTestSections(test.Name, assessmentTestId, sections);
    }

    private static TabulationTableResponse MethodTable(TabulationPrintTestResponse test, IReadOnlyList<TabulationPrintParticipantResponse> participants)
    {
        var columns = test.MethodColumns;
        var rows = new List<TabulationGridRowResponse>
        {
            new([new(test.Name, 1, 1, Header1, false), .. columns.Select(c => new TabulationCellResponse(c.HeaderText, 1, 1, Header2, false))], true),
            new([new("Intended", 1, 1, Cell, false), .. columns.Select(c => new TabulationCellResponse(c.IntendedResult, 1, 1, Cell, false))], false),
        };

        for (var p = 0; p < participants.Count; p++)
        {
            var index = p;
            rows.Add(ParticipantRow(participants[p], columns.Count, [.. columns.Select(c => (ResultAt(c.Results, index), 1))]));
        }

        return new TabulationTableResponse($"Method items for {test.Name}", rows, null);
    }

    private static TabulationTableResponse SampleTable(
        TabulationPrintTestResponse test, TabulationPrintSampleColumnResponse sample, IReadOnlyList<TabulationPrintParticipantResponse> participants)
    {
        var columns = sample.ResultColumns;
        var rows = new List<TabulationGridRowResponse>
        {
            new([new(test.Name, 1, 1, Header1, false), .. columns.Select(c => new TabulationCellResponse(c.HeaderText, 1, 1, Header2, false))], true),
            new([new("Intended", 1, 2, Cell, false), new(sample.TabulationDescription, columns.Count, 1, Cell, false)], false),
            new([.. columns.Select(c => new TabulationCellResponse(c.IntendedResult, 1, 1, Cell, false))], false),
        };

        for (var p = 0; p < participants.Count; p++)
        {
            var index = p;
            rows.Add(ParticipantRow(participants[p], columns.Count, [.. columns.Select(c => ResultAt(c.Results, index))]));
        }

        return new TabulationTableResponse($"Sample {sample.SampleNumber} results for {test.Name}", rows, null);
    }

    // A participant with no results at all gets one grey cell holding the nil-return reason. Span 0 marks a cell covered by the one before it.
    private static TabulationGridRowResponse ParticipantRow(
        TabulationPrintParticipantResponse participant, int columnCount, (string Text, int Span)[] values)
    {
        var cells = new List<TabulationCellResponse> { new(participant.LabCode, 1, 1, Cell, false) };

        if (participant.HasResults)
        {
            cells.AddRange(values
                .Where(value => value.Span != 0)
                .Select(value => new TabulationCellResponse(value.Text, Math.Max(1, value.Span), 1, Cell, value.Text.Length == 0)));
        }
        else
        {
            cells.Add(new TabulationCellResponse(participant.NilReturnReason, Math.Max(1, columnCount), 1, Cell, true));
        }

        return new TabulationGridRowResponse(cells, false);
    }

    private static (string Text, int Span) ResultAt(IReadOnlyList<TabulationPrintResultResponse> results, int index) =>
        index < results.Count ? (results[index].Result, results[index].Span) : (string.Empty, 1);

    private static string ResultAt(IReadOnlyList<string> results, int index) =>
        index < results.Count ? results[index] : string.Empty;
}
