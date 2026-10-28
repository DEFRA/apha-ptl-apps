using Microsoft.AspNetCore.Razor.TagHelpers;
using PTL.SharedUI.TagHelpers;

namespace PTL.SharedUI.Tests.TagHelpers;

public class GovUkMenuPanelTagHelperTests
{
    private static (GovUkMenuPanelTagHelper Helper, TagHelperContext Context, TagHelperOutput Output) Create(string childContentHtml)
    {
        var helper = new GovUkMenuPanelTagHelper();
        var context = new TagHelperContext([], new Dictionary<object, object>(), Guid.NewGuid().ToString());
        var output = new TagHelperOutput(
            "govuk-menu-panel",
            [],
            (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent().SetHtmlContent(childContentHtml)));

        return (helper, context, output);
    }

    [Fact]
    public async Task ProcessAsync_RendersHiddenPanelWithIdAndClassMatchingTheToggleButton()
    {
        var (helper, context, output) = Create("<p>Menu content</p>");

        await helper.ProcessAsync(context, output);

        Assert.Equal("div", output.TagName);
        Assert.Equal("app-menu-panel", output.Attributes["id"].Value);
        Assert.Equal("app-header__menu-panel", output.Attributes["class"].Value);
        Assert.True(output.Attributes.ContainsName("hidden"));
        Assert.Contains("<p>Menu content</p>", output.Content.GetContent(), StringComparison.Ordinal);
    }
}
