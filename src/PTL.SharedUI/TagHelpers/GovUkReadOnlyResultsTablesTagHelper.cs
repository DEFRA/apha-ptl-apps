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

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = null;
        output.TagMode = TagMode.StartTagAndEndTag;

        foreach (var test in Tests)
        {
            output.Content.AppendHtml($"""<h2 class="govuk-heading-m">{Encode(test.TestName)}</h2>""");
            output.Content.AppendHtml($"""
                <div class="govuk-!-margin-bottom-6" style="overflow-x: auto;">
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

    private static string ValueOrHiddenPlaceholder(string? value) =>
        string.IsNullOrEmpty(value)
            ? """<span class="govuk-visually-hidden">No result entered</span>"""
            : Encode(value);

    private static string Encode(string? value) => WebUtility.HtmlEncode(value) ?? string.Empty;
}
