using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Razor.TagHelpers;
using PTL.SharedUI.TagHelpers;
using PTL.SharedUI.Tests.TestSupport;

namespace PTL.SharedUI.Tests.TagHelpers;

public class GovUkRichTextTagHelperTests
{
    private sealed class TestModel
    {
        public string? Instructions { get; set; }
    }

    private static (TagHelperContext Context, TagHelperOutput Output) CreateTagHelperContext(string tagName) =>
        (new TagHelperContext([], new Dictionary<object, object>(), Guid.NewGuid().ToString()),
         new TagHelperOutput(tagName, [], (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent())));

    [Fact]
    public void Process_Default_RendersEditableTextareaWithToolbarAndServerValue()
    {
        var model = new TestModel { Instructions = "<p>Sample</p>" };
        var (viewContext, expression, generator) = TagHelperRenderContext.Create(model, nameof(TestModel.Instructions));
        var helper = new GovUkRichTextTagHelper(generator) { For = expression, Label = "Instructions", ViewContext = viewContext };
        var (context, output) = CreateTagHelperContext("govuk-rich-text");

        helper.Process(context, output);

        var html = output.Content.GetContent();
        Assert.Contains("ptl-rich-text", html, StringComparison.Ordinal);
        Assert.Contains("data-toolbar=\"" + GovUkRichTextTagHelper.DefaultToolbar + "\"", html, StringComparison.Ordinal);
        Assert.Contains("data-height=\"400\"", html, StringComparison.Ordinal);
        Assert.Contains("data-ptl-server-value=\"&lt;p&gt;Sample&lt;/p&gt;\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-readonly", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_WithErrorAndReadOnly_RendersErrorAndReadOnlyAttributes()
    {
        var model = new TestModel();
        var modelState = new ModelStateDictionary();
        modelState.AddModelError(nameof(TestModel.Instructions), "Enter instructions");
        var (viewContext, expression, generator) = TagHelperRenderContext.Create(model, nameof(TestModel.Instructions), modelState);
        var helper = new GovUkRichTextTagHelper(generator) { For = expression, Label = "Instructions", IsReadOnly = true, ViewContext = viewContext };
        var (context, output) = CreateTagHelperContext("govuk-rich-text");

        helper.Process(context, output);

        var html = output.Content.GetContent();
        Assert.Contains("govuk-textarea--error", html, StringComparison.Ordinal);
        Assert.Contains("Enter instructions", html, StringComparison.Ordinal);
        Assert.Contains("data-readonly=\"true\"", html, StringComparison.Ordinal);
        Assert.Contains("readonly=\"readonly\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_ViewMode_RendersPlainHtmlWithNoToolbar()
    {
        var model = new TestModel { Instructions = "<p>Already saved</p>" };
        var (viewContext, expression, generator) = TagHelperRenderContext.Create(model, nameof(TestModel.Instructions));
        var helper = new GovUkRichTextTagHelper(generator) { For = expression, Label = "Instructions", ViewMode = true, ViewContext = viewContext };
        var (context, output) = CreateTagHelperContext("govuk-rich-text");

        helper.Process(context, output);

        Assert.Equal("div", output.TagName);
        var html = output.Content.GetContent();
        Assert.Contains("Instructions", html, StringComparison.Ordinal);
        Assert.Contains("<p>Already saved</p>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("ptl-rich-text", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_ViewModeWithNullValue_RendersEmptyContentWithoutThrowing()
    {
        var model = new TestModel { Instructions = null };
        var (viewContext, expression, generator) = TagHelperRenderContext.Create(model, nameof(TestModel.Instructions));
        var helper = new GovUkRichTextTagHelper(generator) { For = expression, Label = "Instructions", ViewMode = true, ViewContext = viewContext };
        var (context, output) = CreateTagHelperContext("govuk-rich-text");

        helper.Process(context, output);

        var html = output.Content.GetContent();
        Assert.Contains("""<div class="govuk-body"></div>""", html, StringComparison.Ordinal);
    }
}
