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

    // Opt-in only (defaults false) so PTL.InternalWeb's _Header.cshtml, which never sets this,
    // renders byte-identical markup to before - only PTL.ExternalWeb sets this to true, pairing
    // the button with a <govuk-menu-panel> the caller renders separately (see that tag helper's
    // remarks for why the panel isn't produced here too).
    public bool ShowMenuToggle { get; set; }

    // Optional - shown next to the Menu toggle (visible text, not an icon, per GDS content design
    // guidance against unlabelled icon-only links) separated by a vertical divider. Both must be
    // set for anything to render; neither is set by PTL.InternalWeb.
    public string? HelpLinkText { get; set; }

    public string? HelpLinkHref { get; set; }

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

        var helpLink = !string.IsNullOrWhiteSpace(HelpLinkText) && !string.IsNullOrWhiteSpace(HelpLinkHref)
            ? $"""
                <span class="app-header__divider" aria-hidden="true"></span>
                <a class="app-header__help-link" href="{WebUtility.HtmlEncode(HelpLinkHref)}" target="_blank" rel="noopener noreferrer">
                    {WebUtility.HtmlEncode(HelpLinkText)}
                    <span class="govuk-visually-hidden"> (opens in new tab, PDF)</span>
                </a>
            """
            : "";

        var menuToggle = ShowMenuToggle
            ? $"""
                <div class="app-header__actions">
                    <nav aria-label="Menu" class="app-header__menu">
                        <button type="button" id="app-menu-toggle" class="govuk-service-navigation__toggle" aria-controls="app-menu-panel" aria-expanded="false" aria-label="Show or hide menu" data-app-menu-toggle>Menu</button>
                    </nav>
                    {helpLink}
                </div>
            """
            : "";

        output.TagName = null;
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Content.SetHtmlContent($"""
            <header class="govuk-template__header">
                <div class="govuk-header">
                    <div class="govuk-header__container govuk-width-container">
                        <div class="govuk-header__content">
                            <span class="govuk-header__product-name">{encodedServiceName}</span>
                        </div>
                        {menuToggle}
                    </div>
                </div>
                <section aria-label="Service information" class="govuk-service-navigation" data-module="govuk-service-navigation">
                    <div class="govuk-width-container">
                        <div class="govuk-service-navigation__container">
                            <span class="govuk-service-navigation__service-name">
                                <a href="{encodedHomeUrl}" class="govuk-service-navigation__link govuk-link-image" aria-label="Home">
                                    <img src="{encodedLogoUrl}" alt="APHA" style="{encodedLogoStyle}" />
                                </a>
                            </span>
            """);
        output.Content.AppendHtml(childContent);
        output.Content.AppendHtml("""
                        </div>
                    </div>
                </section>
            </header>
            """);
    }
}
