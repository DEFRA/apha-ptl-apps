using PTL.Contracts.Distribution;

namespace PTL.InternalWeb.Features.Distribution;

// One Results Entry table column - a Test Result Item heading, its packed pixel width, and
// whether it is a Range item (legacy ResultsTable: c.IsRange appends " to " + IntendedResult2).
public sealed record PrintableResultsColumn(Guid TestResultItemId, string Name, int Width, bool IsRange);

// One sample's row of cells for one packed table. Values is null per cell when the worksheet is
// showing the "Blank" version (ShowValues = false) - legacy's BuildTable never sets cellResult.Text
// at all in that case, leaving the cell for the lab to write into by hand.
public sealed record PrintableResultsSampleRow(string SampleNumber, IReadOnlyList<string?> Values);

// One rendered Results Entry table - legacy ResultsTable. A Test's Result Items are packed across
// one or more of these; only the first carries the Test Name heading row.
public sealed record PrintableResultsTable(
    bool ShowTestName, string TestName, int SampleColumnWidth, IReadOnlyList<PrintableResultsColumn> Columns, IReadOnlyList<PrintableResultsSampleRow> Rows);

// One Method Item row - legacy RenderMethodTable. Value is null when ShowValues = false (legacy
// always writes "Name: " but only appends IntendedResult when mShowValues is true).
public sealed record PrintableResultsMethodRow(string Name, string? Value);

// Legacy: a Test with zero samples applicable to it (RenderTable returns Nothing for every one of
// its chunks) renders NEITHER Results tables NOR a Method table at all - both collections are then
// empty rather than merely blank.
public sealed record PrintableResultsTestWorksheet(string TestName, IReadOnlyList<PrintableResultsTable> ResultsTables, IReadOnlyList<PrintableResultsMethodRow> MethodRows);

public sealed record PrintableSampleInstructionRow(string SampleNumber, string ExternalDescription);

public sealed record PrintableResultsWorksheet(
    int YearId,
    int MonthId,
    Guid MonthlyDistributionSchemeId,
    string LabIdLabel,
    string DistributionHeading,
    // Legacy LblDate - "DATE OF DISTRIBUTION: " + MonthlyDistributionScheme.MonthlyDistributionDate,
    // shown on the right of DistributionHeading on the same row.
    string DateOfDistributionLabel,
    string SchemeHeading,
    string VersionLabel,
    string Instructions,
    bool ShowDistributionItems,
    bool DateOfReceipt,
    bool ConditionOnReceipt,
    bool StorageConditions,
    IReadOnlyList<PrintableSampleInstructionRow> SampleInstructions,
    bool HasIntendedResults,
    bool ShowValues,
    IReadOnlyList<PrintableResultsTestWorksheet> Tests,
    // Footer (legacy tblFooter) - IsAccredited gates the UKAS logo panel; DeadlineMessage is
    // legacy LblDeadline; Address/Contact1/Contact2 are legacy's own hardcoded label text
    // (LitAddress/LblContact1/LblContact2 string literals in PrintableResults.aspx.vb, not
    // fetched from any data source).
    bool IsAccredited = false,
    string DeadlineMessage = "");

// Port of legacy PrintableResults.aspx.vb's BuildTable()/ResultsTable - the column-width packing
// is genuine business logic (ExpectedLength drives the budget) and is preserved faithfully,
// reusing the exact same constants and algorithm as the sibling PrintableSchemeWorksheetBuilder
// (legacy's PrintableScheme.aspx.vb ResultsTable is a byte-for-byte duplicate of this one). The
// pixel-measurement/page-break JavaScript hack and the hidden second sample-instruction grid it
// measures against are NOT ported - CSS break-inside/page-break-inside: avoid replaces them, same
// as the Scheme worksheet.
public static class PrintableResultsWorksheetBuilder
{
    private const int TableWidth = 600;
    private const int SampleColumnWidth = 100;
    private const double Tolerance = 0.05;

    // distributionDate/deadlineDate/isAccredited only exist on the Scheduling-level
    // MonthlyDistributionSchemeEntity (spgMonthlyDistribution), a different stored procedure from
    // spgIntendedResults - legacy fetches them via a second business object
    // (MonthlyDistributionScheme.FetchMonthlyDistributionSchemeChild), which the caller mirrors
    // with its own already-available GetScheduleAsync call.
    public static PrintableResultsWorksheet Build(
        int yearId, int monthId, IntendedResultsResponse page, bool showValues, DateTime distributionDate = default, DateTime deadlineDate = default, bool isAccredited = false)
    {
        ArgumentNullException.ThrowIfNull(page);

        // Legacy GVsampleInstruction binds to IntendedResultsSet.Samples - the distribution's one
        // shared sample list. Every Test carries the identical set (just with per-test
        // IsApplicable/Values), so the first Test (if any) supplies it unfiltered, in fetch order.
        var sampleInstructions = (page.Tests.Count > 0 ? page.Tests[0].Samples : [])
            .Select(s => new PrintableSampleInstructionRow(s.SampleNumber, s.ExternalDescription))
            .ToList();

        return new PrintableResultsWorksheet(
            yearId,
            monthId,
            page.MonthlyDistributionSchemeId,
            LabIdLabel: "LAB. IDENTIFICATION NO.",
            DistributionHeading: $"DISTRIBUTION {page.DistributionReferenceFull}",
            DateOfDistributionLabel: $"DATE OF DISTRIBUTION: {distributionDate:dd/MM/yyyy}",
            SchemeHeading: $"{page.SchemeIdentifier}: {page.SchemeName}",
            VersionLabel: $"Version: {page.SchemeVersionDate:dd/MM/yyyy}",
            Instructions: page.Instructions,
            ShowDistributionItems: page.DateOfReceipt || page.ConditionOnReceipt || page.StorageConditions,
            DateOfReceipt: page.DateOfReceipt,
            ConditionOnReceipt: page.ConditionOnReceipt,
            StorageConditions: page.StorageConditions,
            SampleInstructions: sampleInstructions,
            HasIntendedResults: page.HasIntendedResults,
            ShowValues: showValues,
            Tests: [.. page.Tests.Select(t => BuildTestWorksheet(t, showValues))],
            IsAccredited: isAccredited,
            DeadlineMessage: $"Results should be entered online no later than 23:00 GMT on {deadlineDate:dd/MM/yyyy}. After this time the result entry page will be locked and results cannot be entered.");
    }

    private static PrintableResultsTestWorksheet BuildTestWorksheet(IntendedResultsTestResponse test, bool showValues)
    {
        // Legacy: s.SampleTests.GetSampleTestByTestId(mTest.TestId).IsApplicable, preserving the
        // distribution's natural (fetch) order - never re-sorted by sample number text.
        var applicableSamples = test.Samples.Where(s => s.IsApplicable).ToList();
        if (applicableSamples.Count == 0)
        {
            return new PrintableResultsTestWorksheet(test.TestName, [], []);
        }

        var resultsTables = PackColumns(test.TestName, test.ResultItems, applicableSamples, showValues);
        var methodRows = test.MethodItems
            .Select(m => new PrintableResultsMethodRow(m.Name, showValues ? m.IntendedResult : null))
            .ToList();

        return new PrintableResultsTestWorksheet(test.TestName, resultsTables, methodRows);
    }

    // Legacy ResultsTable.AddColumn: pack columns left-to-right, starting a new table once adding
    // the next column would exceed the width budget by more than the tolerance - but never split a
    // table that has no columns yet (an oversized single column still gets its own table).
    private static List<PrintableResultsTable> PackColumns(
        string testName, IReadOnlyList<IntendedResultsResultItemResponse> resultItems, List<IntendedResultsSampleRowResponse> applicableSamples, bool showValues)
    {
        var budget = (TableWidth - SampleColumnWidth) * (1 + Tolerance);
        var chunks = new List<(List<IntendedResultsResultItemResponse> Items, int RawWidth)>();
        var current = new List<IntendedResultsResultItemResponse>();
        var currentWidth = 0;

        foreach (var item in resultItems)
        {
            var columnWidth = item.ExpectedLength * 20;
            if (current.Count > 0 && currentWidth + columnWidth > budget)
            {
                chunks.Add((current, currentWidth));
                current = [];
                currentWidth = 0;
            }

            current.Add(item);
            currentWidth += columnWidth;
        }

        // A test with zero Result Items still renders one (empty) table, showing the "This test
        // has no Result Items" placeholder - legacy's loop runs zero times but the table/header
        // rows are still built, with one blank value cell per sample row.
        chunks.Add((current, currentWidth));

        var rendered = new List<PrintableResultsTable>();
        for (var chunkIndex = 0; chunkIndex < chunks.Count; chunkIndex++)
        {
            var (items, rawWidth) = chunks[chunkIndex];
            var columns = StretchColumnWidths(items, rawWidth);
            rendered.Add(new PrintableResultsTable(
                ShowTestName: chunkIndex == 0,
                TestName: chunkIndex == 0 ? testName : string.Empty,
                SampleColumnWidth: SampleColumnWidth,
                Columns: columns,
                Rows: [.. applicableSamples.Select(s => BuildSampleRow(s, columns, showValues))]));
        }

        return rendered;
    }

    // Legacy RenderTable: stretch each column's raw (ExpectedLength * 20) width proportionally so
    // the table fills the full available width; the last column absorbs any rounding remainder
    // instead of being computed from the stretch factor, and every column loses one pixel for its
    // border.
    private static List<PrintableResultsColumn> StretchColumnWidths(List<IntendedResultsResultItemResponse> items, int rawWidth)
    {
        if (items.Count == 0)
        {
            return [];
        }

        var available = TableWidth - SampleColumnWidth;
        var stretchFactor = rawWidth == 0 ? 0 : available / (double)rawWidth;
        var columns = new List<PrintableResultsColumn>();
        var usedWidth = 0;

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            int width;
            if (i < items.Count - 1)
            {
                width = (int)Math.Floor(item.ExpectedLength * 20 * stretchFactor);
                usedWidth += width;
            }
            else
            {
                width = available - usedWidth;
            }

            columns.Add(new PrintableResultsColumn(item.TestResultItemId, item.Name, width - 1, item.IsRange));
        }

        return columns;
    }

    // Legacy: cellResult.Text = ...IntendedResult, and (unconditionally, whenever IsRange) += " to
    // " + IntendedResult2 - even when IntendedResult2 is still blank - only ever set at all when
    // mShowValues is true.
    private static PrintableResultsSampleRow BuildSampleRow(IntendedResultsSampleRowResponse sample, List<PrintableResultsColumn> columns, bool showValues)
    {
        if (columns.Count == 0)
        {
            return new PrintableResultsSampleRow(sample.SampleNumber, [null]);
        }

        var valueByResultItemId = sample.Values.ToDictionary(v => v.TestResultItemId);
        var values = columns.Select(column =>
        {
            if (!showValues)
            {
                return (string?)null;
            }

            if (!valueByResultItemId.TryGetValue(column.TestResultItemId, out var value))
            {
                return string.Empty;
            }

            return column.IsRange ? $"{value.IntendedResult} to {value.IntendedResult2}" : value.IntendedResult;
        }).ToList();

        return new PrintableResultsSampleRow(sample.SampleNumber, values);
    }
}
