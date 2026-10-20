using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using PTL.SharedUI.TagHelpers;
using PTL.SharedUI.Tests.TestSupport;

namespace PTL.SharedUI.Tests.TagHelpers;

public class GovUkPageHeaderTagHelperTests
{
    private sealed class NullView : IView
    {
        public string Path => string.Empty;

        public Task RenderAsync(ViewContext context) => Task.CompletedTask;
    }

    private static (GovUkPageHeaderTagHelper Helper, TagHelperContext Context, TagHelperOutput Output) Create(
        string serviceName, string? logoStyle, string childContentHtml)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRouting();
        services.AddMvcCore().AddViews();
        var provider = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = provider };
        var actionContext = new Microsoft.AspNetCore.Mvc.ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        var metadataProvider = provider.GetRequiredService<Microsoft.AspNetCore.Mvc.ModelBinding.IModelMetadataProvider>();
        var viewContext = new ViewContext(
            actionContext,
            new NullView(),
            new ViewDataDictionary(metadataProvider, new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary()),
            new TempDataDictionary(httpContext, new FakeTempDataProvider()),
            TextWriter.Null,
            new HtmlHelperOptions());

        var helper = new GovUkPageHeaderTagHelper(provider.GetRequiredService<LinkGenerator>(), provider.GetRequiredService<IUrlHelperFactory>())
        {
            ServiceName = serviceName,
            LogoStyle = logoStyle,
            ViewContext = viewContext
        };

        var context = new TagHelperContext([], new Dictionary<object, object>(), Guid.NewGuid().ToString());
        var output = new TagHelperOutput(
            "govuk-page-header",
            [],
            (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent().SetHtmlContent(childContentHtml)));

        return (helper, context, output);
    }

    [Fact]
    public async Task ProcessAsync_RendersServiceNameLogoAndChildContent()
    {
        var (helper, context, output) = Create("Proficiency Testing - Internal", "height:80px; width:auto;", "<nav>Signed in as: Jane</nav>");

        await helper.ProcessAsync(context, output);

        var html = output.Content.GetContent();
        Assert.Contains("Proficiency Testing - Internal", html, StringComparison.Ordinal);
        Assert.Contains("height:80px; width:auto;", html, StringComparison.Ordinal);
        Assert.Contains("<nav>Signed in as: Jane</nav>", html, StringComparison.Ordinal);
        Assert.Contains("AphaLogo.png", html, StringComparison.Ordinal);
    }

    // No "Home"/"Index" endpoint is registered in this minimal host, so LinkGenerator can't
    // resolve a path - exercises the "?? \"/\"" fallback rather than a real resolved route.
    [Fact]
    public async Task ProcessAsync_NoMatchingRoute_FallsBackToRootHomeLink()
    {
        var (helper, context, output) = Create("Proficiency Testing - External", null, string.Empty);

        await helper.ProcessAsync(context, output);

        var html = output.Content.GetContent();
        Assert.Contains("href=\"/\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProcessAsync_ServiceNameContainsMarkup_IsHtmlEncoded()
    {
        var (helper, context, output) = Create("<script>alert(1)</script>", null, string.Empty);

        await helper.ProcessAsync(context, output);

        var html = output.Content.GetContent();
        Assert.DoesNotContain("<script>alert(1)</script>", html, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProcessAsync_SuppressesOwnTagName()
    {
        var (helper, context, output) = Create("Proficiency Testing - Internal", null, string.Empty);

        await helper.ProcessAsync(context, output);

        Assert.Null(output.TagName);
    }
}
