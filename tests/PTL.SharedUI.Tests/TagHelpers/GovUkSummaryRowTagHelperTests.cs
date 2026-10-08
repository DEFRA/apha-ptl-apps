using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Razor.TagHelpers;
using PTL.SharedUI.TagHelpers;

namespace PTL.SharedUI.Tests.TagHelpers;

public class GovUkSummaryRowTagHelperTests
{
    private static TagHelperContext CreateContext() => new([], new Dictionary<object, object>(), Guid.NewGuid().ToString());

    private static TagHelperOutput CreateOutput(string childContent) =>
        new("govuk-summary-row", [], (_, _) =>
        {
            var content = new DefaultTagHelperContent();
            content.SetHtmlContent(childContent);
            return Task.FromResult<TagHelperContent>(content);
        });

    [Fact]
    public async Task ProcessAsync_RendersLabelAndChildContentAsDd()
    {
        var helper = new GovUkSummaryRowTagHelper { Label = "Country" };
        var context = CreateContext();
        var output = CreateOutput("United Kingdom");

        await helper.ProcessAsync(context, output);

        Assert.Equal("div", output.TagName);
        Assert.Equal("govuk-summary-list__row", output.Attributes["class"].Value);
        var html = output.PreContent.GetContent() + output.Content.GetContent() + output.PostContent.GetContent();
        Assert.Contains("<dt class=\"govuk-summary-list__key\">Country</dt>", html, StringComparison.Ordinal);
        Assert.Contains("<dd class=\"govuk-summary-list__value\">United Kingdom</dd>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProcessAsync_LabelIsHtmlEncoded()
    {
        var helper = new GovUkSummaryRowTagHelper { Label = "A < B" };
        var context = CreateContext();
        var output = CreateOutput("value");

        await helper.ProcessAsync(context, output);

        var html = output.PreContent.GetContent();
        Assert.Contains("A &lt; B", html, StringComparison.Ordinal);
    }
}
