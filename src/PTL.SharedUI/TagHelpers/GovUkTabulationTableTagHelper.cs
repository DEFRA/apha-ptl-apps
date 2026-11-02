using System.Net;
using Microsoft.AspNetCore.Razor.TagHelpers;
using PTL.Contracts.Distribution;

namespace PTL.SharedUI.TagHelpers;

/// <summary>
/// Renders a single Tabulation/Issued Tabulation table (header rows, row/column spans, nil-return
/// styling) shared by InternalWeb's Tabulation screens and ExternalWeb's View Reports screen - see
/// docs/Code-Duplication-Playbook.md. Ported line-for-line from
/// src/PTL.InternalWeb/Views/Shared/_TabulationTable.cshtml, which now delegates to this tag
/// helper rather than keeping its own copy of the row/cell layout logic.
/// </summary>
[HtmlTargetElement("govuk-tabulation-table")]
public class GovUkTabulationTableTagHelper : TagHelper
{
    public required TabulationTableResponse Table { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = null;
        output.TagMode = TagMode.StartTagAndEndTag;

        var table = Table;
        var headerRowCount = table.Rows.TakeWhile(r => r.IsHeader).Count();

        // A row starts with a row header only when column 0 is not still covered by a rowspan above it.
        var rowHeaderFlags = new List<bool>(table.Rows.Count);
        var covered = 0;
        foreach (var row in table.Rows)
        {
            if (covered > 0)
            {
                rowHeaderFlags.Add(false);
                covered--;
                continue;
            }

            var first = row.Cells.Count > 0 ? row.Cells[0] : null;
            rowHeaderFlags.Add(!row.IsHeader && first is not null);
            covered = first is null ? 0 : first.RowSpan - 1;
        }

        output.Content.AppendHtml($"""
            <section class="ptl-table-scroll ptl-tabulation-scroll" aria-label="{Encode(table.Caption)}">
                <table class="TabulationTable">
                    <caption class="govuk-visually-hidden">{Encode(table.Caption)}</caption>
            """);

        if (headerRowCount > 0)
        {
            output.Content.AppendHtml("<thead>");
            for (var r = 0; r < headerRowCount; r++)
            {
                output.Content.AppendHtml("<tr>");
                foreach (var cell in table.Rows[r].Cells)
                {
                    AppendCell(output, cell, "th", HeaderScope(cell));
                }

                output.Content.AppendHtml("</tr>");
            }

            output.Content.AppendHtml("</thead>");
        }

        output.Content.AppendHtml("<tbody>");
        for (var r = headerRowCount; r < table.Rows.Count; r++)
        {
            RenderRow(output, table.Rows[r], rowHeaderFlags[r]);
        }

        output.Content.AppendHtml("</tbody></table></section>");
    }

    private static void RenderRow(TagHelperOutput output, TabulationGridRowResponse row, bool firstIsRowHeader)
    {
        output.Content.AppendHtml("<tr>");
        for (var c = 0; c < row.Cells.Count; c++)
        {
            var cell = row.Cells[c];
            if (row.IsHeader)
            {
                AppendCell(output, cell, "th", HeaderScope(cell));
            }
            else if (c == 0 && firstIsRowHeader)
            {
                AppendCell(output, cell, "th", "row");
            }
            else if (cell.Kind is "Header1" or "Header2")
            {
                AppendCell(output, cell, "th", HeaderScope(cell));
            }
            else
            {
                AppendCell(output, cell, "td", null);
            }
        }

        output.Content.AppendHtml("</tr>");
    }

    private static void AppendCell(TagHelperOutput output, TabulationCellResponse cell, string tagName, string? scope)
    {
        var scopeAttr = scope is not null ? $" scope=\"{scope}\"" : string.Empty;
        var classAttr = CellClass(cell) is { } cls ? $" class=\"{cls}\"" : string.Empty;
        var colspanAttr = cell.ColSpan > 1 ? $" colspan=\"{cell.ColSpan}\"" : string.Empty;
        var rowspanAttr = cell.RowSpan > 1 ? $" rowspan=\"{cell.RowSpan}\"" : string.Empty;

        output.Content.AppendHtml($"<{tagName}{scopeAttr}{classAttr}{colspanAttr}{rowspanAttr}>");
        output.Content.AppendHtml(CellContent(cell));
        output.Content.AppendHtml($"</{tagName}>");
    }

    private static string? CellClass(TabulationCellResponse cell)
    {
        var kind = cell.Kind switch
        {
            "Header1" => "TabulationHeader1",
            "Header2" => "TabulationHeader2",
            "Cell" => "TabulationCell",
            _ => null,
        };

        if (!cell.IsNilReturn)
        {
            return kind;
        }

        return kind is null ? "NilReturn" : kind + " NilReturn";
    }

    private static string CellContent(TabulationCellResponse cell)
    {
        if (!string.IsNullOrEmpty(cell.Text))
        {
            return Encode(cell.Text);
        }

        // An empty nil-return cell is only grey on screen, so say so for assistive technology.
        return cell.IsNilReturn ? """<span class="govuk-visually-hidden">No result</span>""" : "&nbsp;";
    }

    // A heading that spans several columns heads the whole group (as AmendResults does), not just its first column.
    private static string HeaderScope(TabulationCellResponse cell) => cell.ColSpan > 1 ? "colgroup" : "col";

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
