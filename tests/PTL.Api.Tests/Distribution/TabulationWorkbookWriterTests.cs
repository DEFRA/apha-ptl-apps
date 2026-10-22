using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using PTL.Contracts.Distribution;
using PTL.Core.Distribution;

namespace PTL.Api.Tests.Distribution;

public class TabulationWorkbookWriterTests
{
    private static TabulationTableResponse Table(params TabulationGridRowResponse[] rows) => new("Caption", rows, null);

    private static TabulationGridRowResponse Row(bool header, params TabulationCellResponse[] cells) => new(cells, header);

    private static TabulationCellResponse Cell(string text, int colSpan = 1, int rowSpan = 1) => new(text, colSpan, rowSpan, "Cell", false);

    private static (Dictionary<string, string> Cells, List<string> Merges, List<int> Rows) Read(byte[] workbook)
    {
        using var stream = new MemoryStream(workbook);
        using var document = SpreadsheetDocument.Open(stream, false);
        var worksheet = document.WorkbookPart!.WorksheetParts.Single().Worksheet!;
        var cells = worksheet.Descendants<Cell>().ToDictionary(c => c.CellReference!.Value!, c => c.InlineString!.Text!.Text);
        var merges = worksheet.Descendants<MergeCell>().Select(m => m.Reference!.Value!).ToList();
        var rows = worksheet.Descendants<Row>().Select(r => (int)r.RowIndex!.Value).ToList();
        return (cells, merges, rows);
    }

    [Fact]
    public void Write_NullTables_Throws() => Assert.Throws<ArgumentNullException>(() => TabulationWorkbookWriter.Write(null!));

    [Fact]
    public void Write_PlainRows_PutsEachCellInItsColumnAndRow()
    {
        var bytes = TabulationWorkbookWriter.Write([Table(Row(true, Cell("A"), Cell("B")), Row(false, Cell("1"), Cell("2")))]);

        var (cells, merges, _) = Read(bytes);

        Assert.Equal(new Dictionary<string, string> { ["A1"] = "A", ["B1"] = "B", ["A2"] = "1", ["B2"] = "2" }, cells);
        Assert.Empty(merges);
    }

    [Fact]
    public void Write_SpansBecomeMergedCellsAndPushLaterCellsAlong()
    {
        // Row 1: a 2 row tall cell, a 2 column wide cell. Row 2 skips the column the tall cell still covers.
        var bytes = TabulationWorkbookWriter.Write(
        [
            Table(
                Row(true, Cell("Test", rowSpan: 2), Cell("Wide", colSpan: 2)),
                Row(true, Cell("x"), Cell("y"))),
        ]);

        var (cells, merges, _) = Read(bytes);

        Assert.Equal(["A1:A2", "B1:C1"], merges);
        Assert.Equal(("Test", "Wide", "x", "y"), (cells["A1"], cells["B1"], cells["B2"], cells["C2"]));
    }

    [Fact]
    public void Write_TablesAreStackedWithABlankRowBetween()
    {
        var bytes = TabulationWorkbookWriter.Write(
        [
            Table(Row(true, Cell("First")), Row(false, Cell("a"))),
            Table(Row(true, Cell("Second"))),
        ]);

        var (cells, _, rows) = Read(bytes);

        Assert.Equal([1, 2, 4], rows);
        Assert.Equal("Second", cells["A4"]);
    }

    [Fact]
    public void Write_TableEndingInATallCell_StartsTheNextTableBelowIt()
    {
        var bytes = TabulationWorkbookWriter.Write([Table(Row(true, Cell("Tall", rowSpan: 3))), Table(Row(true, Cell("Next")))]);

        var (cells, _, _) = Read(bytes);

        Assert.Equal("Next", cells["A5"]);
    }

    [Fact]
    public void Write_NonBreakingSpacesAreTrimmed()
    {
        var bytes = TabulationWorkbookWriter.Write([Table(Row(false, Cell("\u00a0Result\u00a0"), Cell("\u00a0")))]);

        var (cells, _, _) = Read(bytes);

        Assert.Equal(("Result", string.Empty), (cells["A1"], cells["B1"]));
    }

    [Fact]
    public void Write_ColumnsBeyondZ_UseTwoLetterReferences()
    {
        var cells = Enumerable.Range(1, 28).Select(i => Cell(i.ToString(System.Globalization.CultureInfo.InvariantCulture))).ToArray();

        var (written, _, _) = Read(TabulationWorkbookWriter.Write([Table(Row(false, cells))]));

        Assert.Equal(("26", "27", "28"), (written["Z1"], written["AA1"], written["AB1"]));
    }

    [Fact]
    public void Write_NoTables_StillMakesAValidWorkbook()
    {
        var (cells, merges, rows) = Read(TabulationWorkbookWriter.Write([]));

        Assert.Empty(cells);
        Assert.Empty(merges);
        Assert.Empty(rows);
    }

    [Fact]
    public void ContentType_IsTheSpreadsheetMimeType() =>
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", TabulationWorkbookWriter.ContentType);
}
