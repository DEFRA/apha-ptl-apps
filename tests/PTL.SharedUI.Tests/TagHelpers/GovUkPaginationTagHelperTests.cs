using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Razor.TagHelpers;
using PTL.SharedUI.Pagination;
using PTL.SharedUI.TagHelpers;

namespace PTL.SharedUI.Tests.TagHelpers;

public class GovUkPaginationTagHelperTests
{
    // Minimal stub so the tag helper can build page links without a full MVC routing context -
    // just echoes back the requested page/pageSize as a query string.
    private sealed class StubUrlHelper : IUrlHelper
    {
        public ActionContext ActionContext { get; } = new();

        public string? Action(UrlActionContext actionContext)
        {
            var values = actionContext.Values as IDictionary<string, object?>;
            var page = values is not null && values.TryGetValue("page", out var p) ? p : null;
            return $"/Index?page={page}";
        }

        public string? Content(string? contentPath) => contentPath;

        public bool IsLocalUrl(string? url) => true;

        public string? Link(string? routeName, object? values) => null;

        public string? RouteUrl(UrlRouteContext routeContext) => null;
    }

    private sealed class StubUrlHelperFactory : IUrlHelperFactory
    {
        public IUrlHelper GetUrlHelper(ActionContext context) => new StubUrlHelper();
    }

    private static (TagHelperContext Context, TagHelperOutput Output) CreateTagHelperContext(string tagName) =>
        (new TagHelperContext([], new Dictionary<object, object>(), Guid.NewGuid().ToString()),
         new TagHelperOutput(tagName, [], (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent())));

    private static string Render(PaginationModel model)
    {
        var helper = new GovUkPaginationTagHelper(new StubUrlHelperFactory())
        {
            Model = model,
            ViewContext = new ViewContext()
        };
        var (context, output) = CreateTagHelperContext("govuk-pagination");

        helper.Process(context, output);

        return output.Content.GetContent();
    }

    [Fact]
    public void Process_SinglePage_SuppressesOutput()
    {
        var model = new PaginationModel { CurrentPage = 1, PageSize = 25, TotalRecords = 5 };
        var helper = new GovUkPaginationTagHelper(new StubUrlHelperFactory())
        {
            Model = model,
            ViewContext = new ViewContext()
        };
        var (context, output) = CreateTagHelperContext("govuk-pagination");

        helper.Process(context, output);

        Assert.True(output.IsContentModified);
        Assert.Equal(string.Empty, output.Content.GetContent());
        Assert.Null(output.TagName);
    }

    [Fact]
    public void Process_FirstPage_OmitsPreviousLink()
    {
        var model = new PaginationModel { CurrentPage = 1, PageSize = 10, TotalRecords = 50 };

        var html = Render(model);

        Assert.DoesNotContain("govuk-pagination__prev", html, StringComparison.Ordinal);
        Assert.Contains("govuk-pagination__next", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_LastPage_OmitsNextLink()
    {
        var model = new PaginationModel { CurrentPage = 5, PageSize = 10, TotalRecords = 50 };

        var html = Render(model);

        Assert.Contains("govuk-pagination__prev", html, StringComparison.Ordinal);
        Assert.DoesNotContain("govuk-pagination__next", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_ManyPages_RendersEllipsesAndCurrentPage()
    {
        var model = new PaginationModel { CurrentPage = 5, PageSize = 10, TotalRecords = 200 };

        var html = Render(model);

        Assert.Contains("govuk-pagination__item--ellipses", html, StringComparison.Ordinal);
        Assert.Contains("govuk-pagination__item--current", html, StringComparison.Ordinal);
        Assert.Contains("aria-current=\"page\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_FewPages_RendersAllPageNumbers()
    {
        var model = new PaginationModel { CurrentPage = 2, PageSize = 25, TotalRecords = 100 };

        var html = Render(model);

        Assert.DoesNotContain("govuk-pagination__item--ellipses", html, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Page 1\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Page 4\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_ManyPagesCurrentPageNearStart_OmitsLeadingEllipsisButKeepsTrailingOne()
    {
        // 20 pages, current=2: start=max(2, 1)=2 (no gap after page 1), end=min(19,3)=3 (gap before page 20).
        var model = new PaginationModel { CurrentPage = 2, PageSize = 10, TotalRecords = 200 };

        var html = Render(model);

        Assert.Single([.. System.Text.RegularExpressions.Regex.Matches(html, "govuk-pagination__item--ellipses")]);
    }

    [Fact]
    public void Process_ManyPagesCurrentPageNearEnd_OmitsTrailingEllipsisButKeepsLeadingOne()
    {
        // 20 pages, current=19: start=max(2,18)=18 (gap after page 1), end=min(19,20)=19 (no gap before page 20).
        var model = new PaginationModel { CurrentPage = 19, PageSize = 10, TotalRecords = 200 };

        var html = Render(model);

        Assert.Single([.. System.Text.RegularExpressions.Regex.Matches(html, "govuk-pagination__item--ellipses")]);
    }
}
