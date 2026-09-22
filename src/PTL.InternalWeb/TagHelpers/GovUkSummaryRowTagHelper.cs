using System.Net;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace PTL.InternalWeb.TagHelpers;

/// <summary>
/// Renders a single GOV.UK Design System summary-list row (dt/dd pair) from a single element -
/// replaces the 4-line `govuk-summary-list__row` block that was otherwise hand-written and
/// duplicated for every field across the Customer and Participant Details pages. The tag's inner
/// content becomes the `<dd>` value verbatim, so multi-line values (e.g. an address built from
/// several lines joined with `&lt;br /&gt;`) and inline markup (e.g. a status `&lt;strong&gt;` tag) both
/// work unchanged.
/// </summary>
[HtmlTargetElement("govuk-summary-row")]
public class GovUkSummaryRowTagHelper : TagHelper
{
    public string Label { get; set; } = string.Empty;

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var childContent = await output.GetChildContentAsync();

        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("class", "govuk-summary-list__row");
        output.PreContent.SetHtmlContent($"<dt class=\"govuk-summary-list__key\">{WebUtility.HtmlEncode(Label)}</dt><dd class=\"govuk-summary-list__value\">");
        output.Content.SetHtmlContent(childContent);
        output.PostContent.SetHtmlContent("</dd>");
    }
}
