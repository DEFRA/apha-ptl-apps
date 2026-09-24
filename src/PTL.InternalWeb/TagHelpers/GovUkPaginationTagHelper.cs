using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using PTL.InternalWeb.Pagination;

namespace PTL.InternalWeb.TagHelpers;

/// <summary>
/// Renders the GOV.UK Design System pagination pattern (Previous/Next + numbered links, with
/// ellipses for a large page count) from a single <see cref="PaginationModel"/> - replaces the
/// hand-written, duplicated <c>&lt;nav class="govuk-pagination"&gt;</c> block previously repeated
/// across Customer/Participant/Contract/Scheme's Index.cshtml. Renders nothing when there is only
/// one page (matches the previous per-page "if (totalPages > 1)" guard).
/// </summary>
[HtmlTargetElement("govuk-pagination", Attributes = ModelAttributeName)]
public class GovUkPaginationTagHelper(IUrlHelperFactory urlHelperFactory) : TagHelper
{
    private const string ModelAttributeName = "model";

    [HtmlAttributeName(ModelAttributeName)]
    public PaginationModel Model { get; set; } = default!;

    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = default!;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (Model.TotalPages <= 1)
        {
            output.SuppressOutput();
            return;
        }

        var urlHelper = urlHelperFactory.GetUrlHelper(ViewContext);
        string LinkFor(int page) => urlHelper.Action(Model.Action, MergeRouteValues(page)) ?? "#";

        output.TagName = "nav";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("class", "govuk-pagination");
        output.Attributes.SetAttribute("aria-label", "Pagination");
        output.Attributes.RemoveAll(ModelAttributeName);

        var html = new System.Text.StringBuilder();

        if (Model.HasPreviousPage)
        {
            html.Append($"""
                <div class="govuk-pagination__prev">
                    <a class="govuk-link govuk-pagination__link" href="{WebUtility.HtmlEncode(LinkFor(Model.CurrentPage - 1))}" rel="prev">
                        <svg class="govuk-pagination__icon govuk-pagination__icon--prev" xmlns="http://www.w3.org/2000/svg" height="13" width="15" aria-hidden="true" focusable="false" viewBox="0 0 15 13">
                            <path d="m6.5938-0.0078125-6.7266 6.7266 6.7441 6.4062 1.377-1.4492-4.1856-3.9768h12.896v-2h-12.984l4.2931-4.293-1.414-1.414z"></path>
                        </svg>
                        <span class="govuk-pagination__link-title">Previous<span class="govuk-visually-hidden"> page</span></span>
                    </a>
                </div>
                """);
        }

        html.Append("""<ul class="govuk-pagination__list">""");
        foreach (var item in BuildPageItems(Model.CurrentPage, Model.TotalPages))
        {
            if (item is null)
            {
                html.Append("""<li class="govuk-pagination__item govuk-pagination__item--ellipses">&ctdot;</li>""");
                continue;
            }

            var pageNumber = item.Value;
            var isCurrent = pageNumber == Model.CurrentPage;
            var currentClass = isCurrent ? " govuk-pagination__item--current" : "";
            var ariaCurrent = isCurrent ? " aria-current=\"page\"" : "";
            html.Append($"""
                <li class="govuk-pagination__item{currentClass}">
                    <a class="govuk-link govuk-pagination__link" href="{WebUtility.HtmlEncode(LinkFor(pageNumber))}" aria-label="Page {pageNumber}"{ariaCurrent}>{pageNumber}</a>
                </li>
                """);
        }
        html.Append("</ul>");

        if (Model.HasNextPage)
        {
            html.Append($"""
                <div class="govuk-pagination__next">
                    <a class="govuk-link govuk-pagination__link" href="{WebUtility.HtmlEncode(LinkFor(Model.CurrentPage + 1))}" rel="next">
                        <span class="govuk-pagination__link-title">Next<span class="govuk-visually-hidden"> page</span></span>
                        <svg class="govuk-pagination__icon govuk-pagination__icon--next" xmlns="http://www.w3.org/2000/svg" height="13" width="15" aria-hidden="true" focusable="false" viewBox="0 0 15 13">
                            <path d="m8.107-0.0078125-1.4136 1.4136 4.2926 4.293h-12.986v2h12.896l-4.1855 3.9768 1.377 1.4492 6.7441-6.4062-6.7246-6.7266z"></path>
                        </svg>
                    </a>
                </div>
                """);
        }

        output.Content.SetHtmlContent(html.ToString());
    }

    // Standard GOV.UK truncation: always show first/last page, the current page +/-1, and an
    // ellipsis for any gap - shows every page when there are 7 or fewer.
    private static List<int?> BuildPageItems(int current, int total)
    {
        var items = new List<int?>();
        if (total <= 7)
        {
            for (var i = 1; i <= total; i++)
            {
                items.Add(i);
            }

            return items;
        }

        items.Add(1);
        var start = Math.Max(2, current - 1);
        var end = Math.Min(total - 1, current + 1);
        if (start > 2)
        {
            items.Add(null);
        }

        for (var i = start; i <= end; i++)
        {
            items.Add(i);
        }

        if (end < total - 1)
        {
            items.Add(null);
        }

        items.Add(total);
        return items;
    }

    private Dictionary<string, object?> MergeRouteValues(int page)
    {
        var routeValues = new Dictionary<string, object?>(Model.RouteValues)
        {
            ["page"] = page,
            ["pageSize"] = Model.PageSize
        };
        return routeValues;
    }
}

/// <summary>
/// Renders the "items per page" dropdown (top-right, above the table) from the same
/// <see cref="PaginationModel"/> used by <see cref="GovUkPaginationTagHelper"/> - an auto-submitting
/// GET form that resets to page 1 and preserves every other current filter/sort value as a hidden
/// input.
/// </summary>
[HtmlTargetElement("govuk-page-size-selector", Attributes = ModelAttributeName)]
public class GovUkPageSizeSelectorTagHelper(IUrlHelperFactory urlHelperFactory) : TagHelper
{
    private const string ModelAttributeName = "model";

    [HtmlAttributeName(ModelAttributeName)]
    public PaginationModel Model { get; set; } = default!;

    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = default!;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var urlHelper = urlHelperFactory.GetUrlHelper(ViewContext);

        output.TagName = "form";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("method", "get");
        output.Attributes.SetAttribute("action", urlHelper.Action(Model.Action) ?? string.Empty);
        output.Attributes.SetAttribute("class", "ptl-page-size-selector");
        output.Attributes.RemoveAll(ModelAttributeName);

        var html = new System.Text.StringBuilder();
        html.Append("""<input type="hidden" name="page" value="1" />""");
        foreach (var (key, value) in Model.RouteValues)
        {
            html.Append($"""<input type="hidden" name="{WebUtility.HtmlEncode(key)}" value="{WebUtility.HtmlEncode(value?.ToString())}" />""");
        }

        html.Append("""
            <div class="govuk-form-group govuk-!-margin-bottom-0">
                <label class="govuk-label" for="pageSize">Items per page</label>
                <select class="govuk-select" id="pageSize" name="pageSize" onchange="this.form.submit()">
            """);
        foreach (var size in PaginationModel.AvailablePageSizes)
        {
            var selected = size == Model.PageSize ? " selected" : "";
            html.Append($"""<option value="{size}"{selected}>{size}</option>""");
        }
        html.Append("""
                </select>
                <noscript><button class="govuk-button govuk-button--secondary govuk-!-margin-left-2" type="submit">Apply</button></noscript>
            </div>
            """);

        output.Content.SetHtmlContent(html.ToString());
    }
}
