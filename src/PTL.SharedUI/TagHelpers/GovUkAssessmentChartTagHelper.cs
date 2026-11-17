using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Razor.TagHelpers;
using PTL.Contracts.Distribution;

namespace PTL.SharedUI.TagHelpers;

/// <summary>
/// Renders the assessment score frequency bar chart (legacy AssessmentResultsChart.ascx) as an
/// accessible SVG, shared by InternalWeb's Assessment Results pages and ExternalWeb's. The defaults
/// reproduce InternalWeb's original 500x500 blue chart; ExternalWeb passes the legacy wide shape.
/// </summary>
[HtmlTargetElement("govuk-assessment-chart")]
public class GovUkAssessmentChartTagHelper : TagHelper
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private const string Ink = "#0b0c0c";
    private const double Left = 70, Right = 20, Top = 20;

    public required AssessmentResultsResponse Results { get; set; }

    public double Width { get; set; } = 500;

    public double Height { get; set; } = 500;

    public string BarColour { get; set; } = "#1d70b8";

    public bool ShowYAxisTitle { get; set; } = true;

    /// <summary>When set, a colour swatch with this text is drawn under the x-axis title.</summary>
    public string? Legend { get; set; }

    /// <summary>Prefix of the SVG title/description ids; make it unique if a page has several charts.</summary>
    public string IdPrefix { get; set; } = "assessment-chart";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = null;
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Content.SetHtmlContent(Render());
    }

    private static string Enc(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    private static string F(double value) => value.ToString("0.##", Inv);

    private string Render()
    {
        var scores = Results.Scores;
        var bottom = Legend is null ? 120 : 150;
        var plotWidth = Width - Left - Right;
        var plotHeight = Height - Top - bottom;
        var axisY = Top + plotHeight;

        var maxFrequency = Math.Max(1, scores.Count == 0 ? 0 : scores.Max(s => s.Frequency));
        var step = Math.Max(1, (int)Math.Ceiling(maxFrequency / 10.0));
        var yMax = (int)Math.Ceiling(maxFrequency / (double)step) * step;
        double Y(int value) => axisY - (plotHeight * value / yMax);

        var title = Results.Titles.Count > 0 ? Results.Titles[0] : string.Empty;
        var sb = new StringBuilder();
        sb.Append(Inv, $"<svg class=\"ptl-assessment-chart\" role=\"img\" aria-labelledby=\"{IdPrefix}-title {IdPrefix}-desc\" focusable=\"false\" ")
          .Append(Inv, $"xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {F(Width)} {F(Height)}\" width=\"{F(Width)}\" height=\"{F(Height)}\">")
          .Append(Inv, $"<title id=\"{IdPrefix}-title\">{Enc(title)}</title>")
          .Append(Inv, $"<desc id=\"{IdPrefix}-desc\">{Enc(Results.YAxisTitle)} by {Enc(Results.XAxisTitle)}. The same data is shown in the table below the chart.</desc>");

        for (var tick = 0; tick <= yMax; tick += step)
        {
            var y = F(Y(tick));
            sb.Append(Inv, $"<g><line x1=\"{F(Left)}\" y1=\"{y}\" x2=\"{F(Width - Right)}\" y2=\"{y}\" stroke=\"#b1b4b6\" stroke-width=\"1\" />")
              .Append(Inv, $"<text x=\"{F(Left - 8)}\" y=\"{y}\" text-anchor=\"end\" dominant-baseline=\"middle\" font-size=\"14\" fill=\"{Ink}\">{tick}</text></g>");
        }

        var band = plotWidth / Math.Max(1, scores.Count);
        var barWidth = band * 0.6;
        for (var i = 0; i < scores.Count; i++)
        {
            var centre = Left + (band * i) + (band / 2);
            var barTop = Y(scores[i].Frequency);
            sb.Append(Inv, $"<g><rect x=\"{F(centre - (barWidth / 2))}\" y=\"{F(barTop)}\" width=\"{F(barWidth)}\" height=\"{F(axisY - barTop)}\" fill=\"{Enc(BarColour)}\" />")
              .Append(Inv, $"<text transform=\"translate({F(centre)} {F(axisY + 10)}) rotate(-60)\" text-anchor=\"end\" dominant-baseline=\"middle\" font-size=\"14\" fill=\"{Ink}\">{Enc(scores[i].Label)}</text></g>");
        }

        AppendAxes(sb, plotWidth, plotHeight, axisY);
        AppendLegend(sb, plotWidth);
        return sb.Append("</svg>").ToString();
    }

    private void AppendAxes(StringBuilder sb, double plotWidth, double plotHeight, double axisY)
    {
        sb.Append(Inv, $"<line x1=\"{F(Left)}\" y1=\"{F(Top)}\" x2=\"{F(Left)}\" y2=\"{F(axisY)}\" stroke=\"{Ink}\" stroke-width=\"1\" />")
          .Append(Inv, $"<line x1=\"{F(Left)}\" y1=\"{F(axisY)}\" x2=\"{F(Width - Right)}\" y2=\"{F(axisY)}\" stroke=\"{Ink}\" stroke-width=\"1\" />");

        var titleY = Legend is null ? Height - 8 : Height - 34;
        sb.Append(Inv, $"<text x=\"{F(Left + (plotWidth / 2))}\" y=\"{F(titleY)}\" text-anchor=\"middle\" font-size=\"16\" font-weight=\"bold\" fill=\"{Ink}\">{Enc(Results.XAxisTitle)}</text>");

        if (ShowYAxisTitle)
        {
            sb.Append(Inv, $"<text transform=\"translate(18 {F(Top + (plotHeight / 2))}) rotate(-90)\" text-anchor=\"middle\" font-size=\"16\" font-weight=\"bold\" fill=\"{Ink}\">{Enc(Results.YAxisTitle)}</text>");
        }
    }

    private void AppendLegend(StringBuilder sb, double plotWidth)
    {
        if (Legend is null)
        {
            return;
        }

        var x = Left + (plotWidth / 2) - 40;
        sb.Append(Inv, $"<g><rect x=\"{F(x)}\" y=\"{F(Height - 22)}\" width=\"14\" height=\"14\" fill=\"{Enc(BarColour)}\" />")
          .Append(Inv, $"<text x=\"{F(x + 20)}\" y=\"{F(Height - 15)}\" dominant-baseline=\"middle\" font-size=\"14\" fill=\"{Ink}\">{Enc(Legend)}</text></g>");
    }
}
