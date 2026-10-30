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
}
