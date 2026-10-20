using System.Net;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.AspNetCore.Routing;

namespace PTL.SharedUI.TagHelpers;

/// <summary>
/// Renders the GOV.UK header markup shared by InternalWeb and ExternalWeb's _Header.cshtml
/// partials (logo, service name, Home link) - only the authenticated-user info section (different
/// per app: InternalWeb's simple "Signed in as", ExternalWeb's lab-code/sign-out nav) is left to
/// the caller as child content, spliced into the still-open service-navigation wrapper markup.
/// </summary>
[HtmlTargetElement("govuk-page-header")]
public class GovUkPageHeaderTagHelper(LinkGenerator linkGenerator, IUrlHelperFactory urlHelperFactory) : TagHelper
{
    public string ServiceName { get; set; } = string.Empty;

    public string? LogoStyle { get; set; }

    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = default!;

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var childContent = await output.GetChildContentAsync();

        // LinkGenerator/IUrlHelper (rather than hardcoded absolute paths) so the Home link and
        // logo image still resolve correctly if the app is ever hosted behind a reverse proxy
        // with a path prefix.
        var homeUrl = linkGenerator.GetPathByAction(ViewContext.HttpContext, action: "Index", controller: "Home") ?? "/";
        var logoUrl = urlHelperFactory.GetUrlHelper(ViewContext).Content("~/assets/images/AphaLogo.png");

        // HTML-encoded like Razor's @Model.ServiceName would auto-encode it, since this is built
        // as a raw HTML string rather than going through Razor's output encoding.
        var encodedServiceName = WebUtility.HtmlEncode(ServiceName);
        var encodedLogoStyle = WebUtility.HtmlEncode(LogoStyle);
        var encodedHomeUrl = WebUtility.HtmlEncode(homeUrl);
        var encodedLogoUrl = WebUtility.HtmlEncode(logoUrl);

        output.TagName = null;
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Content.SetHtmlContent($"""
            <header class="govuk-template__header">
                <div class="govuk-header">
                    <div class="govuk-header__container govuk-width-container">
                        <div class="govuk-header__content">
                            <span class="govuk-header__product-name">{encodedServiceName}</span>
                        </div>
                    </div>
                </div>
                <section aria-label="Service information" class="govuk-service-navigation" data-module="govuk-service-navigation">
                    <div class="govuk-width-container govuk-service-navigation__container">
                        <span class="govuk-service-navigation__service-name">
                            <a href="{encodedHomeUrl}" class="govuk-service-navigation__link govuk-link-image" aria-label="Home">
                                <img src="{encodedLogoUrl}" alt="APHA" style="{encodedLogoStyle}" />
                            </a>
                        </span>
            """);
        output.Content.AppendHtml(childContent);
        output.Content.AppendHtml("""
                    </div>
                </section>
            </header>
            """);
    }
}
