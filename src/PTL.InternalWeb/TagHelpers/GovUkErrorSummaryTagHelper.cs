using System.Net;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace PTL.InternalWeb.TagHelpers;

/// <summary>
/// Renders the GOV.UK Design System error summary (title + linked list) from a precomputed list
/// of (Field, Message) pairs - replaces the ~15-line wrapper markup that was otherwise hand-written
/// and duplicated across the Customer/Participant/Contract/Scheme forms. Each form still computes
/// its own <c>errors</c> list (including its own field-order sorting, which differs per form), so
/// this only dedups the rendering, not the ordering rules.
/// </summary>
[HtmlTargetElement("govuk-error-summary", Attributes = ErrorsAttributeName)]
public class GovUkErrorSummaryTagHelper : TagHelper
{
    private const string ErrorsAttributeName = "errors";

    [HtmlAttributeName(ErrorsAttributeName)]
    public IEnumerable<(string Field, string Message)> Errors { get; set; } = [];

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var errors = Errors.ToList();
        if (errors.Count == 0)
        {
            output.SuppressOutput();
            return;
        }

        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("class", "govuk-error-summary");
        output.Attributes.SetAttribute("data-module", "govuk-error-summary");

        var items = string.Concat(errors.Select(error =>
            string.IsNullOrEmpty(error.Field)
                ? $"<li>{WebUtility.HtmlEncode(error.Message)}</li>"
                : $"<li><a href=\"#{WebUtility.HtmlEncode(error.Field)}\">{WebUtility.HtmlEncode(error.Message)}</a></li>"));

        output.Content.SetHtmlContent(
            "<div role=\"alert\">" +
            "<h2 class=\"govuk-error-summary__title\">There is a problem</h2>" +
            "<div class=\"govuk-error-summary__body\">" +
            $"<ul class=\"govuk-list govuk-error-summary__list\">{items}</ul>" +
            "</div></div>");
    }
}
