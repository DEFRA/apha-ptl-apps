using Microsoft.AspNetCore.Razor.TagHelpers;
using PTL.Contracts.Distribution;
using PTL.SharedUI.TagHelpers;

namespace PTL.SharedUI.Tests.TagHelpers;

public class GovUkAssessmentChartTagHelperTests
{
    private static AssessmentScoreFrequencyResponse Score(int score, string label, int frequency) => new(score, label, frequency);

    private static AssessmentResultsResponse Results(params AssessmentScoreFrequencyResponse[] scores) =>
        new(true, ["Bar chart shows the frequency of assessment scores"], "Assessment Score", "Frequency", scores);

    private static string Render(GovUkAssessmentChartTagHelper helper)
    {
        var context = new TagHelperContext([], new Dictionary<object, object>(), Guid.NewGuid().ToString());
        var output = new TagHelperOutput(
            "govuk-assessment-chart",
            [],
            (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));

        helper.Process(context, output);

        Assert.Null(output.TagName);
        return output.Content.GetContent();
    }

    [Fact]
    public void Process_Defaults_RendersTheOriginalBlue500By500Chart()
    {
        var html = Render(new GovUkAssessmentChartTagHelper { Results = Results(Score(1, "1 Fail", 3), Score(5, "5 Excellent", 25)) });

        Assert.Contains("""<svg class="ptl-assessment-chart" role="img" aria-labelledby="assessment-chart-title assessment-chart-desc" focusable="false" """, html, StringComparison.Ordinal);
        Assert.Contains("""viewBox="0 0 500 500" width="500" height="500">""", html, StringComparison.Ordinal);
        Assert.Contains("""<title id="assessment-chart-title">Bar chart shows the frequency of assessment scores</title>""", html, StringComparison.Ordinal);
        Assert.Contains("Frequency by Assessment Score.", html, StringComparison.Ordinal);
        Assert.Equal(2, CountOf(html, "<rect "));
        Assert.Equal(2, CountOf(html, "fill=\"#1d70b8\""));
        Assert.Contains(">5 Excellent</text>", html, StringComparison.Ordinal);
        Assert.Contains(">27</text>", html, StringComparison.Ordinal);
        Assert.Contains(">Frequency</text>", html, StringComparison.Ordinal);
        Assert.Contains("rotate(-90)", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_CustomShapeColourAndLegend_AppliesThemAndHidesTheYAxisTitle()
    {
        var html = Render(new GovUkAssessmentChartTagHelper
        {
            Results = Results(Score(1, "1 Fail", 3)),
            Width = 1024,
            Height = 400,
            BarColour = "#00703c",
            ShowYAxisTitle = false,
            Legend = "Frequency",
            IdPrefix = "ext-chart",
        });

        Assert.Contains("""viewBox="0 0 1024 400" width="1024" height="400">""", html, StringComparison.Ordinal);
        Assert.Contains("""aria-labelledby="ext-chart-title ext-chart-desc" """, html, StringComparison.Ordinal);
        Assert.Contains("""<title id="ext-chart-title">""", html, StringComparison.Ordinal);
        Assert.DoesNotContain("rotate(-90)", html, StringComparison.Ordinal);
        Assert.DoesNotContain("#1d70b8", html, StringComparison.Ordinal);
        // One bar plus the legend swatch.
        Assert.Equal(2, CountOf(html, "fill=\"#00703c\""));
        Assert.Contains(">Frequency</text></g></svg>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_NoScoresOrTitles_StillDrawsTheAxes()
    {
        var html = Render(new GovUkAssessmentChartTagHelper
        {
            Results = new AssessmentResultsResponse(true, [], "Assessment Score", "Frequency", []),
        });

        Assert.Contains("<title id=\"assessment-chart-title\"></title>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<rect ", html, StringComparison.Ordinal);
        Assert.Contains(">1</text>", html, StringComparison.Ordinal);
        Assert.Contains("</svg>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_LabelsAndColour_AreHtmlEncoded()
    {
        var html = Render(new GovUkAssessmentChartTagHelper
        {
            Results = Results(Score(1, "<b>x</b>", 1)),
            BarColour = "\"><script>",
        });

        Assert.DoesNotContain("<b>x</b>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>", html, StringComparison.Ordinal);
        Assert.Contains("&lt;b&gt;x&lt;/b&gt;", html, StringComparison.Ordinal);
    }

    private static int CountOf(string text, string value)
    {
        var count = 0;
        for (var i = text.IndexOf(value, StringComparison.Ordinal); i >= 0; i = text.IndexOf(value, i + value.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }
}
