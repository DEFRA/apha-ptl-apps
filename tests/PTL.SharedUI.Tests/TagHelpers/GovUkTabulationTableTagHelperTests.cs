using Microsoft.AspNetCore.Razor.TagHelpers;
using PTL.Contracts.Distribution;
using PTL.SharedUI.TagHelpers;

namespace PTL.SharedUI.Tests.TagHelpers;

public class GovUkTabulationTableTagHelperTests
{
    private static string Render(TabulationTableResponse table)
    {
        var helper = new GovUkTabulationTableTagHelper { Table = table };
        var context = new TagHelperContext([], new Dictionary<object, object>(), Guid.NewGuid().ToString());
        var output = new TagHelperOutput(
            "govuk-tabulation-table",
            [],
            (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));

        helper.Process(context, output);

        return output.Content.GetContent();
    }

    private static TabulationCellResponse Cell(string text, int colSpan = 1, int rowSpan = 1, string kind = "Cell", bool isNilReturn = false) =>
        new(text, colSpan, rowSpan, kind, isNilReturn);

    [Fact]
    public void Process_HeaderRow_RendersThCellsWithColAndColgroupScope()
    {
        var table = new TabulationTableResponse(
            "Culture",
            [
                new TabulationGridRowResponse([Cell("Sample", kind: "Header1"), Cell("Growth", colSpan: 2, kind: "Header1")], IsHeader: true),
                new TabulationGridRowResponse([Cell("S001", kind: "Header1"), Cell("Positive"), Cell("Negative")], IsHeader: false),
            ],
            AssessmentTestId: null);

        var html = Render(table);

        Assert.Contains("""<caption class="govuk-visually-hidden">Culture</caption>""", html, StringComparison.Ordinal);
        Assert.Contains("""<th scope="col" class="TabulationHeader1">Sample</th>""", html, StringComparison.Ordinal);
        Assert.Contains("""<th scope="colgroup" class="TabulationHeader1" colspan="2">Growth</th>""", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_FirstCellOfNonHeaderRow_RendersAsRowHeader()
    {
        var table = new TabulationTableResponse(
            "Culture",
            [new TabulationGridRowResponse([Cell("S001"), Cell("Positive")], IsHeader: false)],
            AssessmentTestId: null);

        var html = Render(table);

        Assert.Contains("""<th scope="row" class="TabulationCell">S001</th>""", html, StringComparison.Ordinal);
        Assert.Contains("""<td class="TabulationCell">Positive</td>""", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_NilReturnCell_AddsNilReturnClass()
    {
        var table = new TabulationTableResponse(
            "Culture",
            [new TabulationGridRowResponse([Cell("S001"), Cell("", isNilReturn: true)], IsHeader: false)],
            AssessmentTestId: null);

        var html = Render(table);

        Assert.Contains("class=\"TabulationCell NilReturn\"", html, StringComparison.Ordinal);
        Assert.Contains("""<span class="govuk-visually-hidden">No result</span>""", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_BlankNonNilReturnCell_RendersNbsp()
    {
        var table = new TabulationTableResponse(
            "Culture",
            [new TabulationGridRowResponse([Cell("S001"), Cell("")], IsHeader: false)],
            AssessmentTestId: null);

        var html = Render(table);

        Assert.Contains("&nbsp;", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_CaptionAndCellText_AreHtmlEncoded()
    {
        var table = new TabulationTableResponse(
            "<script>alert(1)</script>",
            [new TabulationGridRowResponse([Cell("<b>bold</b>")], IsHeader: false)],
            AssessmentTestId: null);

        var html = Render(table);

        Assert.DoesNotContain("<script>alert(1)</script>", html, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", html, StringComparison.Ordinal);
        Assert.Contains("&lt;b&gt;bold&lt;/b&gt;", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_RowHeaderWithRowSpan_SubsequentCoveredRowsHaveNoRowHeaderCell()
    {
        // Row 0's first cell spans down into row 1, so row 1 must not also render a row-header cell.
        var table = new TabulationTableResponse(
            "Culture",
            [
                new TabulationGridRowResponse([Cell("S001", rowSpan: 2), Cell("Positive")], IsHeader: false),
                new TabulationGridRowResponse([Cell("Negative")], IsHeader: false),
            ],
            AssessmentTestId: null);

        var html = Render(table);

        Assert.Contains("""<th scope="row" class="TabulationCell" rowspan="2">S001</th>""", html, StringComparison.Ordinal);
        Assert.DoesNotContain("""scope="row" class="TabulationCell">Negative""", html, StringComparison.Ordinal);
        Assert.Contains("""<td class="TabulationCell">Negative</td>""", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_NonFirstCellWithHeaderKindInDataRow_RendersAsThNotTd()
    {
        var table = new TabulationTableResponse(
            "Culture",
            [new TabulationGridRowResponse([Cell("S001"), Cell("Growth", kind: "Header2")], IsHeader: false)],
            AssessmentTestId: null);

        var html = Render(table);

        Assert.Contains("""<th scope="col" class="TabulationHeader2">Growth</th>""", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Header2", """<th scope="col" class="TabulationHeader2">Value</th>""")]
    [InlineData("Unrecognised", """<th scope="col">Value</th>""")]
    public void Process_CellKind_MapsToExpectedClass(string kind, string expectedMarkup)
    {
        var table = new TabulationTableResponse(
            "Culture",
            [new TabulationGridRowResponse([Cell("Value", kind: kind)], IsHeader: true)],
            AssessmentTestId: null);

        var html = Render(table);

        Assert.Contains(expectedMarkup, html, StringComparison.Ordinal);
    }
}
