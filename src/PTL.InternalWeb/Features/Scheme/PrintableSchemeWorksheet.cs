using PTL.Contracts.Scheme;

namespace PTL.InternalWeb.Features.Scheme;

// One Results Entry table column - a Test Result Item heading plus the pixel width it's allotted
// once GetColumnWidth's raw widths are stretched to fill the table (legacy ResultsTable.RenderTable).
public sealed record PrintableResultsColumn(string Name, int Width);

// One rendered Results Entry table. A Test's result items are packed across one or more of these
// (legacy ResultsTable) - only the first carries the Test Type heading row.
public sealed record PrintableResultsTable(bool ShowTestName, string TestTypeName, int SampleColumnWidth, IReadOnlyList<PrintableResultsColumn> Columns);

// The Method table that follows a Test's Results Entry table(s) - one row per Test Method Item.
public sealed record PrintableMethodTable(IReadOnlyList<string> MethodItemNames);

public sealed record PrintableTestWorksheet(string TestTypeName, IReadOnlyList<PrintableResultsTable> ResultsTables, PrintableMethodTable MethodTable);

public sealed record PrintableSchemeWorksheet(
    string LabIdLabel,
    string SchemeHeading,
    string Instructions,
    bool ShowDistributionItems,
    bool DateOfReceipt,
    bool ConditionOnReceipt,
    bool StorageConditions,
    int NumberOfSamples,
    IReadOnlyList<string> SampleNumbers,
    IReadOnlyList<PrintableTestWorksheet> Tests);

// Port of legacy PrintableScheme.aspx.vb's BuildTable()/ResultsTable - the column-width packing is
// genuine business logic (legacy comment: ExpectedLength drives the budget) and is preserved
// faithfully. The pixel-measurement/page-break JavaScript hack is NOT ported - CSS
// break-inside/page-break-inside: avoid replaces it (see printable-scheme.css).
public static class PrintableSchemeWorksheetBuilder
{
    private const int TableWidth = 600;
    private const int SampleColumnWidth = 100;
    private const double Tolerance = 0.05;

    public static PrintableSchemeWorksheet Build(SchemeResponse scheme)
    {
        ArgumentNullException.ThrowIfNull(scheme);

        var sampleNumbers = BuildSampleNumbers(scheme.NumberOfSamples);

        // Legacy RenderTable returns Nothing whenever NumberOfSamples = 0, and since that check
        // runs per rendered chunk with the same scheme-wide value, a zero-sample scheme ends up
        // with neither Results Entry nor Method tables for any Test - only the worksheet header.
        var tests = scheme.NumberOfSamples <= 0
            ? []
            : (scheme.Tests ?? []).OrderBy(t => t.Order).Select(BuildTestWorksheet).ToList();

        return new PrintableSchemeWorksheet(
            LabIdLabel: "LAB. IDENTIFICATION NO.",
            SchemeHeading: $"{scheme.Identifier}: {scheme.Name}",
            Instructions: scheme.Instructions,
            ShowDistributionItems: scheme.DateOfReceipt || scheme.ConditionOnReceipt || scheme.StorageConditions,
            DateOfReceipt: scheme.DateOfReceipt,
            ConditionOnReceipt: scheme.ConditionOnReceipt,
            StorageConditions: scheme.StorageConditions,
            NumberOfSamples: scheme.NumberOfSamples,
            SampleNumbers: sampleNumbers,
            Tests: tests);
    }

    // List, not IReadOnlyList: this is a private helper with one call site that immediately wraps
    // the result into the public record's IReadOnlyList<string> property, so the concrete type
    // avoids an interface indirection with no abstraction lost at the actual public boundary.
    // Legacy: "00/" + "000" + i for i < 10, "00/" + "00" + i otherwise - preserved exactly,
    // including its lack of a third digit tier for i >= 100.
    private static List<string> BuildSampleNumbers(int numberOfSamples)
    {
        var numbers = new List<string>();
        for (var i = 1; i <= numberOfSamples; i++)
        {
            numbers.Add(i < 10 ? $"00/000{i}" : $"00/00{i}");
        }

        return numbers;
    }

    private static PrintableTestWorksheet BuildTestWorksheet(SchemeTestResponse test)
    {
        var resultsTables = PackColumns(test.TestType, (test.ResultItems ?? []).OrderBy(i => i.Order).ToList());
        var methodTable = new PrintableMethodTable([.. (test.MethodItems ?? []).OrderBy(i => i.Order).Select(i => i.Name)]);

        return new PrintableTestWorksheet(test.TestType, resultsTables, methodTable);
    }

    // Legacy ResultsTable.AddColumn: pack columns left-to-right, starting a new table once adding
    // the next column would exceed the width budget by more than the tolerance - but never split a
    // table that has no columns yet (an oversized single column still gets its own table).
    // List, not IReadOnlyList for the parameter and return type: the sole call site already passes
    // a List (BuildTestWorksheet's .ToList()) and stores the result straight into the public
    // record's IReadOnlyList<PrintableResultsTable> property - the real abstraction boundary for
    // consumers of this builder - so narrowing here avoids interface indirection with no caller impact.
    private static List<PrintableResultsTable> PackColumns(string testTypeName, List<SchemeTestItemResponse> resultItems)
    {
        var budget = (TableWidth - SampleColumnWidth) * (1 + Tolerance);
        var tables = new List<(List<SchemeTestItemResponse> Items, int RawWidth)>();
        var current = new List<SchemeTestItemResponse>();
        var currentWidth = 0;

        foreach (var item in resultItems)
        {
            var columnWidth = item.ExpectedLength * 20;
            if (current.Count > 0 && currentWidth + columnWidth > budget)
            {
                tables.Add((current, currentWidth));
                current = [];
                currentWidth = 0;
            }

            current.Add(item);
            currentWidth += columnWidth;
        }

        // A test with zero Result Items still renders one (empty) table, showing the
        // "This test has no Result Items" placeholder - legacy's loop runs zero times but the
        // table/header rows are still built.
        tables.Add((current, currentWidth));

        var rendered = new List<PrintableResultsTable>();
        for (var tableIndex = 0; tableIndex < tables.Count; tableIndex++)
        {
            var (items, rawWidth) = tables[tableIndex];
            rendered.Add(new PrintableResultsTable(
                ShowTestName: tableIndex == 0,
                TestTypeName: tableIndex == 0 ? testTypeName : string.Empty,
                SampleColumnWidth: SampleColumnWidth,
                Columns: StretchColumnWidths(items, rawWidth)));
        }

        return rendered;
    }

    // Legacy RenderTable: stretch each column's raw (ExpectedLength * 20) width proportionally so
    // the table fills the full available width; the last column absorbs any rounding remainder
    // instead of being computed from the stretch factor, and every column loses one pixel for its
    // border.
    // List, not IReadOnlyList: the sole call site (above) always passes a List, and the indexer
    // in the loop below benefits from the concrete type - again no caller ever needed the interface.
    private static List<PrintableResultsColumn> StretchColumnWidths(List<SchemeTestItemResponse> items, int rawWidth)
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

            columns.Add(new PrintableResultsColumn(item.Name, width - 1));
        }

        return columns;
    }
}
