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
        string serviceName, string? logoStyle, string childContentHtml, bool showMenuToggle = false, string? helpLinkText = null, string? helpLinkHref = null)
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

        var helper = new GovUkPageHeaderTagHelper(provider.GetRequiredService<IUrlHelperFactory>())
        {
            ServiceName = serviceName,
            LogoStyle = logoStyle,
            ShowMenuToggle = showMenuToggle,
            HelpLinkText = helpLinkText,
            HelpLinkHref = helpLinkHref,
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

    // The Home breadcrumb is the single Home link; a second one on the logo is a WAVE "Redundant link".
    [Fact]
    public async Task ProcessAsync_LogoIsDecorativeImageWithoutLink()
    {
        var (helper, context, output) = Create("Proficiency Testing - External", null, string.Empty);

        await helper.ProcessAsync(context, output);

        var html = output.Content.GetContent();
        Assert.DoesNotContain("<a ", html, StringComparison.Ordinal);
        Assert.Matches("<img [^>]*alt=\"\"", html);
    }

    [Fact]
    public async Task ProcessAsync_CustomLogos_ReplaceTheDefaultAndRenderBothWithTheirAltText()
    {
        var (helper, context, output) = Create("Proficiency Testing - External", "width:180px;", string.Empty);
        helper.LogoUrl = "~/images/apha-scientific-logo.png";
        helper.LogoAlt = "APHA Scientific";
        helper.SecondaryLogoUrl = "~/images/vetqas-logo.jpg";
        helper.SecondaryLogoAlt = "VETQAS";
        helper.SecondaryLogoStyle = "width:70px;";

        await helper.ProcessAsync(context, output);

        var html = output.Content.GetContent();
        Assert.DoesNotContain("AphaLogo.png", html, StringComparison.Ordinal);
        Assert.Contains("/images/apha-scientific-logo.png", html, StringComparison.Ordinal);
        Assert.Contains("alt=\"APHA Scientific\"", html, StringComparison.Ordinal);
        Assert.Contains("/images/vetqas-logo.jpg", html, StringComparison.Ordinal);
        Assert.Contains("alt=\"VETQAS\"", html, StringComparison.Ordinal);
        Assert.Contains("width:70px;", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProcessAsync_NoSecondaryLogo_RendersASingleImage()
    {
        var (helper, context, output) = Create("Proficiency Testing - Internal", null, string.Empty);

        await helper.ProcessAsync(context, output);

        Assert.Equal(1, System.Text.RegularExpressions.Regex.Count(output.Content.GetContent(), "<img "));
    }

    [Fact]
    public async Task ProcessAsync_AccountLink_RendersInTheActionsAreaBetweenMenuAndHelp()
    {
        var (helper, context, output) = Create("Proficiency Testing - External", null, string.Empty, showMenuToggle: true, helpLinkText: "User Manual", helpLinkHref: "/help.pdf");
        helper.AccountLinkText = "CIDM account";
        helper.AccountLinkHref = "https://account.example/portal";

        await helper.ProcessAsync(context, output);

        var html = output.Content.GetContent();
        var menu = html.IndexOf(">Menu</button>", StringComparison.Ordinal);
        var account = html.IndexOf("CIDM account", StringComparison.Ordinal);
        var help = html.IndexOf("User Manual", StringComparison.Ordinal);
        Assert.True(menu >= 0 && menu < account && account < help);
        Assert.Contains("href=\"https://account.example/portal\" target=\"_blank\" rel=\"noopener noreferrer\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProcessAsync_NoAccountLink_RendersNone()
    {
        var (helper, context, output) = Create("Proficiency Testing - External", null, string.Empty, showMenuToggle: true);

        await helper.ProcessAsync(context, output);

        Assert.DoesNotContain("opens in new tab)", output.Content.GetContent(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProcessAsync_ExtraActions_RenderAfterTheHelpLinkInTheActionsArea()
    {
        var (helper, context, output) = Create("Proficiency Testing - External", null, "<nav>Lab</nav>", showMenuToggle: true, helpLinkText: "User Manual", helpLinkHref: "/help.pdf");
        helper.ExtraActions = new Microsoft.AspNetCore.Html.HtmlString("<form id=\"sign-out\">Sign out</form>");

        await helper.ProcessAsync(context, output);

        var html = output.Content.GetContent();
        var help = html.IndexOf("User Manual", StringComparison.Ordinal);
        var extra = html.IndexOf("<form id=\"sign-out\">Sign out</form>", StringComparison.Ordinal);
        var serviceNavigation = html.IndexOf("govuk-service-navigation\"", StringComparison.Ordinal);
        Assert.True(help >= 0 && help < extra && extra < serviceNavigation);
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

    // GOV.UK's markup nests the two: a width container (which has side margins) around the service-navigation container.
    // On one element, the container's own width:100% adds to those margins and the bar overshoots the right-hand edge,
    // pushing the "Signed in as" content out of line with the rest of the page.
    [Fact]
    public async Task ProcessAsync_NestsTheServiceNavigationContainerInsideTheWidthContainer()
    {
        var (helper, context, output) = Create("Proficiency Testing - Internal", null, "<nav>Signed in as: Jane</nav>");

        await helper.ProcessAsync(context, output);

        var html = output.Content.GetContent();
        Assert.DoesNotMatch("class=\"[^\"]*govuk-width-container[^\"]*govuk-service-navigation__container", html);
        Assert.Matches("<div class=\"govuk-width-container\">\\s*<div class=\"govuk-service-navigation__container\">[\\s\\S]*<nav>Signed in as: Jane</nav>\\s*</div>\\s*</div>\\s*</section>", html);
    }

    [Fact]
    public async Task ProcessAsync_SuppressesOwnTagName()
    {
        var (helper, context, output) = Create("Proficiency Testing - Internal", null, string.Empty);

        await helper.ProcessAsync(context, output);

        Assert.Null(output.TagName);
    }

    // Proves PTL.InternalWeb (which never sets ShowMenuToggle) gets no toggle markup at all.
    [Fact]
    public async Task ProcessAsync_ShowMenuToggleDefaultsFalse_NoToggleButtonRendered()
    {
        var (helper, context, output) = Create("Proficiency Testing - Internal", null, string.Empty);

        await helper.ProcessAsync(context, output);

        var html = output.Content.GetContent();
        Assert.DoesNotContain("app-menu-toggle", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProcessAsync_ShowMenuToggleTrue_RendersAccessibleToggleButtonBeforeChildContent()
    {
        var (helper, context, output) = Create("Proficiency Testing - External", null, "<nav>Signed in as: Jane</nav>", showMenuToggle: true);

        await helper.ProcessAsync(context, output);

        var html = output.Content.GetContent();
        Assert.Contains("id=\"app-menu-toggle\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-controls=\"app-menu-panel\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-expanded=\"false\"", html, StringComparison.Ordinal);
        Assert.True(html.IndexOf("app-menu-toggle", StringComparison.Ordinal) < html.IndexOf("Signed in as: Jane", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ProcessAsync_HelpLinkTextAndHrefSet_RendersVisibleLinkAfterToggle()
    {
        var (helper, context, output) = Create("Proficiency Testing - External", null, string.Empty, showMenuToggle: true, helpLinkText: "User Manual", helpLinkHref: "/documents/User Manual.pdf");

        await helper.ProcessAsync(context, output);

        var html = output.Content.GetContent();
        Assert.Contains("app-header__help-link", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/documents/User Manual.pdf\"", html, StringComparison.Ordinal);
        Assert.Contains("User Manual", html, StringComparison.Ordinal);
        Assert.True(html.IndexOf("app-menu-toggle", StringComparison.Ordinal) < html.IndexOf("app-header__help-link", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ProcessAsync_HelpLinkNotSet_NoHelpLinkRendered()
    {
        var (helper, context, output) = Create("Proficiency Testing - External", null, string.Empty, showMenuToggle: true);

        await helper.ProcessAsync(context, output);

        var html = output.Content.GetContent();
        Assert.DoesNotContain("app-header__help-link", html, StringComparison.Ordinal);
    }
}
