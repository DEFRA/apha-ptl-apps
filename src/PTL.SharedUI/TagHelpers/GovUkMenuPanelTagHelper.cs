using Microsoft.AspNetCore.Razor.TagHelpers;

namespace PTL.SharedUI.TagHelpers;

/// <summary>
/// Renders the hidden dropdown panel toggled by <see cref="GovUkPageHeaderTagHelper"/>'s
/// <c>show-menu-toggle</c> button (<c>#app-menu-toggle</c>/<c>aria-controls="app-menu-panel"</c>).
/// Deliberately a separate, standalone tag helper rather than a second child-content slot on
/// <see cref="GovUkPageHeaderTagHelper"/> - that type is shared with PTL.InternalWeb, which never
/// uses a menu panel, so keeping this entirely separate means PTL.InternalWeb's markup is
/// unaffected regardless of what this renders.
/// </summary>
[HtmlTargetElement("govuk-menu-panel")]
public class GovUkMenuPanelTagHelper : TagHelper
{
    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var childContent = await output.GetChildContentAsync();

        output.TagName = "div";
        output.Attributes.SetAttribute("id", "app-menu-panel");
        output.Attributes.SetAttribute("class", "app-header__menu-panel");
        output.Attributes.SetAttribute("hidden", "hidden");
        output.Content.SetHtmlContent(childContent);
    }
}
