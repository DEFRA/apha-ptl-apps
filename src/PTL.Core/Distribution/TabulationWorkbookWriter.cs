using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using PTL.Contracts.Distribution;

namespace PTL.Core.Distribution;

// "Export to Excel" of the Test Consultant Tabulation (legacy TableExportUtil wrote the HTML tables out as .xls).
// The tables are stacked on one sheet, one blank row apart, and the grid's column/row spans become merged cells.
public static class TabulationWorkbookWriter
{
    public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public static byte[] Write(IEnumerable<TabulationTableResponse> tables)
    {
        ArgumentNullException.ThrowIfNull(tables);

        var cells = new SortedDictionary<int, SortedDictionary<int, string>>();
        var merges = new List<string>();
        var nextRow = 1;

        foreach (var table in tables)
        {
            nextRow = PlaceTable(table, nextRow, cells, merges) + 2;
        }

        using var stream = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
        {
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();
            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            worksheetPart.Worksheet = new Worksheet(BuildSheetData(cells));
            if (merges.Count > 0)
            {
                worksheetPart.Worksheet.Append(new MergeCells(merges.Select(reference => new MergeCell { Reference = reference })));
            }

            workbookPart.Workbook.AppendChild(new Sheets(new Sheet
            {
                Id = workbookPart.GetIdOfPart(worksheetPart),
                SheetId = 1,
                Name = "Tabulation",
            }));
            workbookPart.Workbook.Save();
        }

        return stream.ToArray();
    }

    // Returns the last row used. A cell takes the next column not already covered by a span from a row above.
    private static int PlaceTable(
        TabulationTableResponse table, int firstRow, SortedDictionary<int, SortedDictionary<int, string>> cells, List<string> merges)
    {
        var covered = new HashSet<(int Row, int Column)>();
        var row = firstRow;

        foreach (var gridRow in table.Rows)
        {
            var column = 1;
            foreach (var cell in gridRow.Cells)
            {
                while (covered.Contains((row, column)))
                {
                    column++;
                }

                column += PlaceCell(cell, row, column, cells, merges, covered);
            }

            row++;
        }

        return Math.Max(row - 1, covered.Count == 0 ? firstRow : covered.Max(c => c.Row));
    }

    // Writes one cell, marks the area it spans as taken and records its merge. Returns how many columns it spans.
    private static int PlaceCell(
        TabulationCellResponse cell,
        int row,
        int column,
        SortedDictionary<int, SortedDictionary<int, string>> cells,
        List<string> merges,
        HashSet<(int Row, int Column)> covered)
    {
        var colSpan = Math.Max(1, cell.ColSpan);
        var rowSpan = Math.Max(1, cell.RowSpan);

        if (!cells.TryGetValue(row, out var rowCells))
        {
            rowCells = [];
            cells[row] = rowCells;
        }

        rowCells[column] = cell.Text.Replace('\u00a0', ' ').Trim();

        for (var r = 0; r < rowSpan; r++)
        {
            for (var c = 0; c < colSpan; c++)
            {
                covered.Add((row + r, column + c));
            }
        }

        if (colSpan > 1 || rowSpan > 1)
        {
            merges.Add($"{ColumnName(column)}{row}:{ColumnName(column + colSpan - 1)}{row + rowSpan - 1}");
        }

        return colSpan;
    }

    private static SheetData BuildSheetData(SortedDictionary<int, SortedDictionary<int, string>> cells)
    {
        var sheetData = new SheetData();
        foreach (var (rowIndex, rowCells) in cells)
        {
            var row = new Row { RowIndex = (uint)rowIndex };
            foreach (var (columnIndex, text) in rowCells)
            {
                row.Append(new Cell
                {
                    CellReference = $"{ColumnName(columnIndex)}{rowIndex}",
                    DataType = CellValues.InlineString,
                    InlineString = new InlineString(new Text(text)),
                });
            }

            sheetData.Append(row);
        }

        return sheetData;
    }

    // 1 = A, 27 = AA.
    private static string ColumnName(int index)
    {
        var name = string.Empty;
        while (index > 0)
        {
            var remainder = (index - 1) % 26;
            name = (char)('A' + remainder) + name;
            index = (index - 1) / 26;
        }

        return name;
    }
}
