using System.Net;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace PTL.SharedUI.TagHelpers;

/// <summary>
/// Renders the GOV.UK header markup shared by InternalWeb and ExternalWeb's _Header.cshtml
/// partials (logo, service name) - only the authenticated-user info section (different
/// per app: InternalWeb's simple "Signed in as", ExternalWeb's lab-code/sign-out nav) is left to
/// the caller as child content, spliced into the still-open service-navigation wrapper markup.
/// </summary>
[HtmlTargetElement("govuk-page-header")]
public class GovUkPageHeaderTagHelper(IUrlHelperFactory urlHelperFactory) : TagHelper
{
    public string ServiceName { get; set; } = string.Empty;

    public string? LogoStyle { get; set; }

    // Optional overrides, set only by PTL.ExternalWeb (APHA Scientific and VETQAS logos); unset keeps the shared APHA logo.
    public string? LogoUrl { get; set; }

    public string LogoAlt { get; set; } = string.Empty;

    public string? SecondaryLogoUrl { get; set; }

    public string SecondaryLogoAlt { get; set; } = string.Empty;

    public string? SecondaryLogoStyle { get; set; }

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

    // Optional - another link in the actions area, shown between Menu and the help link; set only by PTL.ExternalWeb.
    public string? AccountLinkText { get; set; }

    public string? AccountLinkHref { get; set; }

    // Optional - markup for the end of the actions area (after the links), e.g. a sign-out form; set only by PTL.ExternalWeb.
    public IHtmlContent? ExtraActions { get; set; }

    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = default!;

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var childContent = await output.GetChildContentAsync();

        // IUrlHelper (rather than a hardcoded absolute path) so the logo image still resolves
        // behind a reverse proxy with a path prefix.
        var urlHelper = urlHelperFactory.GetUrlHelper(ViewContext);
        var logoUrl = urlHelper.Content(LogoUrl ?? "~/assets/images/AphaLogo.png");

        // HTML-encoded like Razor's @Model.ServiceName would auto-encode it, since this is built
        // as a raw HTML string rather than going through Razor's output encoding.
        var encodedServiceName = WebUtility.HtmlEncode(ServiceName);
        var encodedLogoStyle = WebUtility.HtmlEncode(LogoStyle);
        var encodedLogoUrl = WebUtility.HtmlEncode(logoUrl);
        var encodedLogoAlt = WebUtility.HtmlEncode(LogoAlt);

        var secondaryLogo = string.IsNullOrWhiteSpace(SecondaryLogoUrl)
            ? ""
            : $"""<img src="{WebUtility.HtmlEncode(urlHelper.Content(SecondaryLogoUrl))}" alt="{WebUtility.HtmlEncode(SecondaryLogoAlt)}" style="{WebUtility.HtmlEncode(SecondaryLogoStyle)}" />""";

        var helpLink = !string.IsNullOrWhiteSpace(HelpLinkText) && !string.IsNullOrWhiteSpace(HelpLinkHref)
            ? $"""
                <span class="app-header__divider" aria-hidden="true"></span>
                <a class="app-header__help-link" href="{WebUtility.HtmlEncode(HelpLinkHref)}" target="_blank" rel="noopener noreferrer">
                    {WebUtility.HtmlEncode(HelpLinkText)}
                    <span class="govuk-visually-hidden"> (opens in new tab, PDF)</span>
                </a>
            """
            : "";

        var accountLink = !string.IsNullOrWhiteSpace(AccountLinkText) && !string.IsNullOrWhiteSpace(AccountLinkHref)
            ? $"""
                <span class="app-header__divider" aria-hidden="true"></span>
                <a class="app-header__help-link" href="{WebUtility.HtmlEncode(AccountLinkHref)}" target="_blank" rel="noopener noreferrer">
                    {WebUtility.HtmlEncode(AccountLinkText)}
                    <span class="govuk-visually-hidden"> (opens in new tab)</span>
                </a>
            """
            : "";

        var extraActions = "";
        if (ExtraActions is not null)
        {
            using var writer = new StringWriter();
            ExtraActions.WriteTo(writer, HtmlEncoder.Default);
            extraActions = $"""<span class="app-header__divider" aria-hidden="true"></span>{writer}""";
        }

        var menuToggle = ShowMenuToggle
            ? $"""
                <div class="app-header__actions">
                    <nav aria-label="Menu" class="app-header__menu">
                        <button type="button" id="app-menu-toggle" class="govuk-service-navigation__toggle" aria-controls="app-menu-panel" aria-expanded="false" aria-label="Show or hide menu" data-app-menu-toggle>Menu</button>
                    </nav>
                    {accountLink}
                    {helpLink}
                    {extraActions}
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
                                <img src="{encodedLogoUrl}" alt="{encodedLogoAlt}" style="{encodedLogoStyle}" />{secondaryLogo}
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
