using Microsoft.AspNetCore.Razor.TagHelpers;
using PTL.Contracts.Distribution;
using PTL.SharedUI.TagHelpers;

namespace PTL.SharedUI.Tests.TagHelpers;

public class GovUkReadOnlyResultsTablesTagHelperTests
{
    private static string Render(IReadOnlyList<ActualResultsTestResponse> tests)
    {
        var helper = new GovUkReadOnlyResultsTablesTagHelper { Tests = tests };
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

        Assert.Contains(">Culture test</h2>", html, StringComparison.Ordinal);
        Assert.Contains("<caption class=\"govuk-table__caption govuk-visually-hidden\">Actual Results for Culture test</caption>", html, StringComparison.Ordinal);
        Assert.Contains(">Growth</th>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_SampleRowWithNoValueForResultItem_RendersHiddenPlaceholder()
    {
        var test = SampleTest();

        var html = Render([test]);

        Assert.Contains("""<span class="govuk-visually-hidden">No result entered</span>""", html, StringComparison.Ordinal);
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
}
