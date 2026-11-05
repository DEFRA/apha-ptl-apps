using System.Net;
using Microsoft.AspNetCore.Razor.TagHelpers;
using PTL.Contracts.Distribution;

namespace PTL.SharedUI.TagHelpers;

/// <summary>
/// Renders the read-only Actual Results tables (one per Test: a Sample x Result Item grid plus a
/// Method Items row) shared verbatim by InternalWeb's Actual Results screen and ExternalWeb's View
/// Results screen - see docs/Code-Duplication-Playbook.md. Takes the API's own
/// <see cref="ActualResultsTestResponse"/> shape directly rather than a per-app view model, since
/// read-only markup has no form-binding names to preserve.
/// </summary>
[HtmlTargetElement("govuk-readonly-results-tables")]
public class GovUkReadOnlyResultsTablesTagHelper : TagHelper
{
    public IReadOnlyList<ActualResultsTestResponse> Tests { get; set; } = [];

    /// <summary>
    /// Renders one table per sample plus a separate method items table (legacy ResultsView.aspx and the
    /// ExternalWeb Results entry page) instead of a single sample x result item matrix per test.
    /// </summary>
    public bool PerSample { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = null;
        output.TagMode = TagMode.StartTagAndEndTag;

        foreach (var test in Tests)
        {
            output.Content.AppendHtml($"""<h3 class="govuk-heading-s">{Encode(test.TestName)}</h3>""");

            if (PerSample)
            {
                RenderPerSample(output, test);
                continue;
            }

            output.Content.AppendHtml($"""
                <div class="govuk-!-margin-bottom-6 app-table-scroll">
                    <table class="govuk-table">
                        <caption class="govuk-table__caption govuk-visually-hidden">Actual Results for {Encode(test.TestName)}</caption>
                        <thead class="govuk-table__head">
                            <tr class="govuk-table__row">
                                <th scope="col" class="govuk-table__header">Sample</th>
                """);
            foreach (var resultItem in test.ResultItems)
            {
                output.Content.AppendHtml($"""<th scope="col" class="govuk-table__header">{Encode(resultItem.Name)}</th>""");
            }

            output.Content.AppendHtml("</tr></thead><tbody class=\"govuk-table__body\">");

            foreach (var sample in test.Samples)
            {
                output.Content.AppendHtml($"""<tr class="govuk-table__row"><th scope="row" class="govuk-table__header">{Encode(sample.SampleNumber)}</th>""");
                foreach (var resultItem in test.ResultItems)
                {
                    var value = sample.Values.FirstOrDefault(v => v.TestResultItemId == resultItem.TestResultItemId)?.Result;
                    output.Content.AppendHtml($"""<td class="govuk-table__cell">{ValueOrHiddenPlaceholder(value)}</td>""");
                }

                output.Content.AppendHtml("</tr>");
            }

            foreach (var methodItem in test.MethodItems)
            {
                var colspan = Math.Max(test.ResultItems.Count, 1);
                output.Content.AppendHtml($"""
                    <tr class="govuk-table__row">
                        <th scope="row" class="govuk-table__header">{Encode(methodItem.Name)}:</th>
                        <td class="govuk-table__cell" colspan="{colspan}">{ValueOrHiddenPlaceholder(methodItem.Result)}</td>
                    </tr>
                    """);
            }

            output.Content.AppendHtml("</tbody></table></div>");
        }
    }

    private static void RenderPerSample(TagHelperOutput output, ActualResultsTestResponse test)
    {
        var testName = Encode(test.TestName);

        if (test.ResultItems.Count == 0)
        {
            output.Content.AppendHtml("""<p class="govuk-body">This test has no result items.</p>""");
        }
        else
        {
            foreach (var sample in test.Samples)
            {
                var sampleNumber = Encode(sample.SampleNumber);
                output.Content.AppendHtml($"""
                    <div class="govuk-!-margin-bottom-6 app-table-scroll">
                        <table class="govuk-table app-results-table">
                            <caption class="govuk-table__caption govuk-visually-hidden">Sample {sampleNumber} results for {testName}</caption>
                            <thead class="govuk-table__head">
                                <tr class="govuk-table__row">
                                    <th scope="col" class="govuk-table__header">Sample {sampleNumber}</th>
                                    <th scope="col" class="govuk-table__header"><span class="govuk-visually-hidden">Value</span></th>
                                </tr>
                            </thead>
                            <tbody class="govuk-table__body">
                    """);
                foreach (var resultItem in test.ResultItems)
                {
                    var value = sample.Values.FirstOrDefault(v => v.TestResultItemId == resultItem.TestResultItemId)?.Result;
                    output.Content.AppendHtml(ItemRow(resultItem.Name, value));
                }

                output.Content.AppendHtml("</tbody></table></div>");
            }
        }

        if (test.MethodItems.Count == 0)
        {
            output.Content.AppendHtml("""<p class="govuk-body">This test has no method items.</p>""");
            return;
        }

        output.Content.AppendHtml($"""
            <div class="govuk-!-margin-bottom-6 app-table-scroll">
                <table class="govuk-table app-results-table">
                    <caption class="govuk-table__caption govuk-visually-hidden">Method items for {testName}</caption>
                    <thead class="govuk-table__head">
                        <tr class="govuk-table__row">
                            <th scope="col" class="govuk-table__header">Method item</th>
                            <th scope="col" class="govuk-table__header"><span class="govuk-visually-hidden">Value</span></th>
                        </tr>
                    </thead>
                    <tbody class="govuk-table__body">
            """);
        foreach (var methodItem in test.MethodItems)
        {
            output.Content.AppendHtml(ItemRow(methodItem.Name, methodItem.Result));
        }

        output.Content.AppendHtml("</tbody></table></div>");
    }

    private static string ItemRow(string name, string? value) =>
        $"""<tr class="govuk-table__row"><th scope="row" class="govuk-table__header govuk-!-font-weight-regular">{Encode(name)}</th><td class="govuk-table__cell">{ValueOrHiddenPlaceholder(value)}</td></tr>""";

    private static string ValueOrHiddenPlaceholder(string? value) =>
        string.IsNullOrEmpty(value)
            ? """<span aria-hidden="true">-</span><span class="govuk-visually-hidden">No result entered</span>"""
            : Encode(value);

    private static string Encode(string? value) => WebUtility.HtmlEncode(value) ?? string.Empty;
}
