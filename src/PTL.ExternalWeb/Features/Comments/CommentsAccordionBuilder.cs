using PTL.Contracts.Distribution;
using PTL.ExternalWeb.Features.Tabulation;

namespace PTL.ExternalWeb.Features.Comments;

public static class CommentsAccordionBuilder
{
    public static IReadOnlyList<TabulationTestSections>? Build(
        IReadOnlyList<TabulationTableResponse> tables,
        bool showMethodItems,
        IReadOnlyCollection<string> shownSamples)
    {
        ArgumentNullException.ThrowIfNull(tables);

        var tests = tables
            .Select(table => BuildTest(table, showMethodItems, shownSamples))
            .Where(test => test.Sections.Count > 0)
            .ToList();

        return tests.Count == 0 ? null : tests;
    }

    private static TabulationTestSections BuildTest(
        TabulationTableResponse table,
        bool showMethodItems,
        IReadOnlyCollection<string> shownSamples)
    {
        var sections = new List<TabulationAccordionSection>();
        var headerRows = table.Rows.Where(r => r.IsHeader).ToList();
        var bodyRows = table.Rows.Where(r => !r.IsHeader).ToList();

        if (showMethodItems && bodyRows.Count > 0)
        {
            sections.Add(new TabulationAccordionSection("Method items", BuildMethodItemsTable(table.Caption, headerRows, bodyRows)));
        }

        foreach (var sample in shownSamples.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var sampleRows = bodyRows
                .Where(r => ContainsSample(r, sample))
                .ToList();

            sections.Add(new TabulationAccordionSection($"Sample {sample}", BuildSampleTable(table.Caption, sample, headerRows, sampleRows)));
        }

        return new TabulationTestSections(table.Caption, table.AssessmentTestId, sections);
    }

    private static bool ContainsSample(TabulationGridRowResponse row, string sample) =>
        row.Cells.Any(c => c.Text.Contains(sample, StringComparison.OrdinalIgnoreCase));

    private static TabulationTableResponse BuildSampleTable(
        string caption,
        string sample,
        IReadOnlyList<TabulationGridRowResponse> headerRows,
        List<TabulationGridRowResponse> sampleRows)
    {
        var rows = new List<TabulationGridRowResponse>();
        rows.AddRange(headerRows);

        if (sampleRows.Count == 0)
        {
            rows.Add(new TabulationGridRowResponse([new TabulationCellResponse(sample, 1, 1, "Cell", false)], false));
        }
        else
        {
            rows.AddRange(sampleRows);
        }

        return new TabulationTableResponse($"Sample {sample} results for {caption}", rows, null);
    }

    private static TabulationTableResponse BuildMethodItemsTable(
        string caption,
        IReadOnlyList<TabulationGridRowResponse> headerRows,
        IReadOnlyList<TabulationGridRowResponse> rows)
    {
        var combined = new List<TabulationGridRowResponse>();
        combined.AddRange(headerRows);
        combined.AddRange(rows);
        return new TabulationTableResponse($"Method items for {caption}", combined, null);
    }
}
