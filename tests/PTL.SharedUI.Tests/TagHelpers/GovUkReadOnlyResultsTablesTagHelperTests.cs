using Microsoft.AspNetCore.Razor.TagHelpers;
using PTL.Contracts.Distribution;
using PTL.SharedUI.TagHelpers;

namespace PTL.SharedUI.Tests.TagHelpers;

public class GovUkReadOnlyResultsTablesTagHelperTests
{
    private static string Render(IReadOnlyList<ActualResultsTestResponse> tests, bool perSample = false)
    {
        var helper = new GovUkReadOnlyResultsTablesTagHelper { Tests = tests, PerSample = perSample };
        var context = new TagHelperContext([], new Dictionary<object, object>(), Guid.NewGuid().ToString());
        var output = new TagHelperOutput(
            "govuk-readonly-results-tables",
            [],
            (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));

        helper.Process(context, output);

        return output.Content.GetContent();
    }

    private static ActualResultsTestResponse SampleTest() => new(
        Guid.NewGuid(),
        "Culture test",
        [new ActualResultsResultItemResponse(Guid.NewGuid(), "Growth", "Positive|Negative", "Text")],
        [new ActualResultsMethodItemResponse(Guid.NewGuid(), "Incubation", "^.*$", "Text", "37C")],
        [new ActualResultsSampleRowResponse(Guid.NewGuid(), "S001", [])]);

    [Fact]
    public void Process_RendersOneHeadingAndTablePerTest()
    {
        var html = Render([SampleTest()]);

        Assert.Contains(">Culture test</h3>", html, StringComparison.Ordinal);
        Assert.Contains("<caption class=\"govuk-table__caption govuk-visually-hidden\">Actual Results for Culture test</caption>", html, StringComparison.Ordinal);
        Assert.Contains(">Growth</th>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_SampleRowWithNoValueForResultItem_RendersHiddenPlaceholder()
    {
        var test = SampleTest();

        var html = Render([test]);

        Assert.Contains("""<span aria-hidden="true">-</span><span class="govuk-visually-hidden">No result entered</span>""", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_WideTable_UsesScrollClassNotInlineStyleAndTestHeadingIsH3()
    {
        var html = Render([SampleTest()]);

        Assert.Contains("app-table-scroll", html, StringComparison.Ordinal);
        Assert.DoesNotContain("style=", html, StringComparison.Ordinal);
        Assert.Contains("<h3 class=\"govuk-heading-s\">Culture test</h3>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_MethodItemRow_RendersNameAndResultWithColspan()
    {
        var html = Render([SampleTest()]);

        Assert.Contains(">Incubation:</th>", html, StringComparison.Ordinal);
        Assert.Contains("colspan=\"1\"", html, StringComparison.Ordinal);
        Assert.Contains(">37C</td>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_NameContainingHtml_IsEncoded()
    {
        var test = new ActualResultsTestResponse(
            Guid.NewGuid(),
            "<script>",
            [],
            [],
            []);

        var html = Render([test]);

        Assert.DoesNotContain("<script>", html, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_PerSample_RendersOneTablePerSampleWithResultItemRowsAndHiddenValueHeader()
    {
        var growth = new ActualResultsResultItemResponse(Guid.NewGuid(), "Growth", "Positive|Negative", "Text");
        var count = new ActualResultsResultItemResponse(Guid.NewGuid(), "Count", "^\\d+$", "Text");
        var test = new ActualResultsTestResponse(
            Guid.NewGuid(),
            "Culture test",
            [growth, count],
            [],
            [
                new ActualResultsSampleRowResponse(Guid.NewGuid(), "S001", [new ActualResultsSampleValueResponse(Guid.NewGuid(), growth.TestResultItemId, "Positive")]),
                new ActualResultsSampleRowResponse(Guid.NewGuid(), "S002", [])
            ]);

        var html = Render([test], perSample: true);

        Assert.Equal(2, System.Text.RegularExpressions.Regex.Count(html, "<table class=\"govuk-table app-results-table\">"));
        Assert.Contains(">Sample S001 results for Culture test</caption>", html, StringComparison.Ordinal);
        Assert.Contains(">Sample S002</th>", html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"govuk-visually-hidden\">Value</span>", html, StringComparison.Ordinal);
        Assert.Contains("govuk-!-font-weight-regular\">Growth</th><td class=\"govuk-table__cell\">Positive</td>", html, StringComparison.Ordinal);
        Assert.Contains("No result entered", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Actual Results for", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_PerSample_MethodItemsRenderInTheirOwnTable()
    {
        var html = Render([SampleTest()], perSample: true);

        Assert.Contains(">Method items for Culture test</caption>", html, StringComparison.Ordinal);
        Assert.Contains(">Method item</th>", html, StringComparison.Ordinal);
        Assert.Contains("govuk-!-font-weight-regular\">Incubation</th><td class=\"govuk-table__cell\">37C</td>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_PerSample_NoResultItemsOrMethodItems_ShowsEmptyStateMessages()
    {
        var test = new ActualResultsTestResponse(Guid.NewGuid(), "Culture test", [], [], [new ActualResultsSampleRowResponse(Guid.NewGuid(), "S001", [])]);

        var html = Render([test], perSample: true);

        Assert.Contains("This test has no result items.", html, StringComparison.Ordinal);
        Assert.Contains("This test has no method items.", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<table", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_PerSample_NamesContainingHtml_AreEncoded()
    {
        var item = new ActualResultsResultItemResponse(Guid.NewGuid(), "<b>Item</b>", "^.*$", "Text");
        var test = new ActualResultsTestResponse(
            Guid.NewGuid(),
            "<i>Test</i>",
            [item],
            [new ActualResultsMethodItemResponse(Guid.NewGuid(), "<u>Method</u>", "^.*$", "Text", "<s>Value</s>")],
            [new ActualResultsSampleRowResponse(Guid.NewGuid(), "<em>1</em>", [])]);

        var html = Render([test], perSample: true);

        Assert.DoesNotContain("<b>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<i>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<u>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<s>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<em>", html, StringComparison.Ordinal);
        Assert.Contains("&lt;s&gt;Value&lt;/s&gt;", html, StringComparison.Ordinal);
    }
}
