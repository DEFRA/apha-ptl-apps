using Microsoft.AspNetCore.Razor.TagHelpers;
using PTL.InternalWeb.TagHelpers;

namespace PTL.InternalWeb.Tests.TagHelpers;

public class GovUkErrorSummaryTagHelperTests
{
    private static (TagHelperContext Context, TagHelperOutput Output) CreateTagHelperContext() =>
        (new TagHelperContext([], new Dictionary<object, object>(), Guid.NewGuid().ToString()),
         new TagHelperOutput("govuk-error-summary", [], (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent())));

    private static TagHelperOutput Render(IEnumerable<(string Field, string Message)> errors)
    {
        var helper = new GovUkErrorSummaryTagHelper { Errors = errors };
        var (context, output) = CreateTagHelperContext();

        helper.Process(context, output);

        return output;
    }

    [Fact]
    public void Process_NoErrors_SuppressesOutput()
    {
        var output = Render([]);

        Assert.True(output.IsContentModified);
        Assert.Equal(string.Empty, output.Content.GetContent());
    }

    [Fact]
    public void Process_FieldError_RendersLinkedListItem()
    {
        var output = Render([("Name", "Enter a name")]);

        var html = output.Content.GetContent();
        Assert.Contains("<a href=\"#Name\">Enter a name</a>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_ErrorWithoutField_RendersPlainListItemWithoutLink()
    {
        var output = Render([(string.Empty, "A general problem occurred")]);

        var html = output.Content.GetContent();
        Assert.Contains("<li>A general problem occurred</li>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<a href=", html, StringComparison.Ordinal);
    }
}
