using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Razor.TagHelpers;
using PTL.SharedUI.TagHelpers;
using PTL.SharedUI.Tests.TestSupport;

namespace PTL.SharedUI.Tests.TagHelpers;

public class GovUkFormTagHelpersTests
{
    private sealed class TestModel
    {
        public string? Name { get; set; }

        public string? CountryId { get; set; }

        public string? Comments { get; set; }

        public DateTime? StartDate { get; set; }
    }

    private static (TagHelperContext Context, TagHelperOutput Output) CreateTagHelperContext(string tagName) =>
        (new TagHelperContext([], new Dictionary<object, object>(), Guid.NewGuid().ToString()),
         new TagHelperOutput(tagName, [], (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent())));

    [Fact]
    public void Input_NoError_RendersLabelAndTextbox()
    {
        var model = new TestModel { Name = "Jane" };
        var (viewContext, expression, generator) = TagHelperRenderContext.Create(model, nameof(TestModel.Name));
        var helper = new GovUkInputTagHelper(generator) { For = expression, Label = "Name", ViewContext = viewContext };
        var (context, output) = CreateTagHelperContext("govuk-input");

        helper.Process(context, output);

        Assert.Equal("div", output.TagName);
        Assert.Equal("govuk-form-group", output.Attributes["class"].Value);
        var html = output.Content.GetContent();
        Assert.Contains("Name", html, StringComparison.Ordinal);
        Assert.Contains("value=\"Jane\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Input_WithHintAndError_RendersHintAndErrorMessage()
    {
        var model = new TestModel();
        var modelState = new ModelStateDictionary();
        modelState.AddModelError(nameof(TestModel.Name), "Enter a name");
        var (viewContext, expression, generator) = TagHelperRenderContext.Create(model, nameof(TestModel.Name), modelState);
        var helper = new GovUkInputTagHelper(generator) { For = expression, Label = "Name", Hint = "As shown on file", ViewContext = viewContext };
        var (context, output) = CreateTagHelperContext("govuk-input");

        helper.Process(context, output);

        Assert.Equal("govuk-form-group govuk-form-group--error", output.Attributes["class"].Value);
        var html = output.Content.GetContent();
        Assert.Contains("govuk-hint", html, StringComparison.Ordinal);
        Assert.Contains("Enter a name", html, StringComparison.Ordinal);
        Assert.Contains("govuk-input--error", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Input_DisabledWithType_RendersDisabledAndTypeAttributes()
    {
        var model = new TestModel();
        var (viewContext, expression, generator) = TagHelperRenderContext.Create(model, nameof(TestModel.Name));
        var helper = new GovUkInputTagHelper(generator) { For = expression, Label = "Name", Disabled = true, Type = "date", ViewContext = viewContext };
        var (context, output) = CreateTagHelperContext("govuk-input");

        helper.Process(context, output);

        var html = output.Content.GetContent();
        Assert.Contains("disabled=\"disabled\"", html, StringComparison.Ordinal);
        Assert.Contains("type=\"date\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Input_DateTypeWithDateTimeValue_RendersIsoValueTheDatePickerAccepts()
    {
        var model = new TestModel { StartDate = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc) };
        var (viewContext, expression, generator) = TagHelperRenderContext.Create(model, nameof(TestModel.StartDate));
        var helper = new GovUkInputTagHelper(generator) { For = expression, Label = "Start date", Type = "date", ViewContext = viewContext };
        var (context, output) = CreateTagHelperContext("govuk-input");

        helper.Process(context, output);

        var html = output.Content.GetContent();
        Assert.Contains("value=\"2026-04-01\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Input_DateTypeWithNoValue_RendersEmptyValue()
    {
        var model = new TestModel();
        var (viewContext, expression, generator) = TagHelperRenderContext.Create(model, nameof(TestModel.StartDate));
        var helper = new GovUkInputTagHelper(generator) { For = expression, Label = "Start date", Type = "date", ViewContext = viewContext };
        var (context, output) = CreateTagHelperContext("govuk-input");

        helper.Process(context, output);

        var html = output.Content.GetContent();
        Assert.Contains("type=\"date\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("value=\"2026", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Select_NoError_RendersOptionsAndOptionLabel()
    {
        var model = new TestModel { CountryId = "GB" };
        var (viewContext, expression, generator) = TagHelperRenderContext.Create(model, nameof(TestModel.CountryId));
        var items = new List<SelectListItem> { new("United Kingdom", "GB"), new("France", "FR") };
        var helper = new GovUkSelectTagHelper(generator) { For = expression, Label = "Country", Items = items, OptionLabel = "- Please Select -", ViewContext = viewContext };
        var (context, output) = CreateTagHelperContext("govuk-select");

        helper.Process(context, output);

        var html = output.Content.GetContent();
        Assert.Contains("- Please Select -", html, StringComparison.Ordinal);
        Assert.Contains("United Kingdom", html, StringComparison.Ordinal);
        Assert.DoesNotContain("govuk-select--error", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Select_WithError_RendersErrorStyling()
    {
        var model = new TestModel();
        var modelState = new ModelStateDictionary();
        modelState.AddModelError(nameof(TestModel.CountryId), "Select a country");
        var (viewContext, expression, generator) = TagHelperRenderContext.Create(model, nameof(TestModel.CountryId), modelState);
        var helper = new GovUkSelectTagHelper(generator) { For = expression, Label = "Country", Items = [], ViewContext = viewContext };
        var (context, output) = CreateTagHelperContext("govuk-select");

        helper.Process(context, output);

        var html = output.Content.GetContent();
        Assert.Contains("govuk-select--error", html, StringComparison.Ordinal);
        Assert.Contains("Select a country", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Textarea_NoError_RendersTextareaWithRows()
    {
        var model = new TestModel { Comments = "Some notes" };
        var (viewContext, expression, generator) = TagHelperRenderContext.Create(model, nameof(TestModel.Comments));
        var helper = new GovUkTextareaTagHelper(generator) { For = expression, Label = "Comments", Rows = 5, ViewContext = viewContext };
        var (context, output) = CreateTagHelperContext("govuk-textarea");

        helper.Process(context, output);

        var html = output.Content.GetContent();
        Assert.Contains("rows=\"5\"", html, StringComparison.Ordinal);
        Assert.Contains("Some notes", html, StringComparison.Ordinal);
        Assert.DoesNotContain("govuk-textarea--error", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Textarea_WithErrorAndLabelClass_RendersErrorAndBoldLabel()
    {
        var model = new TestModel();
        var modelState = new ModelStateDictionary();
        modelState.AddModelError(nameof(TestModel.Comments), "Enter comments");
        var (viewContext, expression, generator) = TagHelperRenderContext.Create(model, nameof(TestModel.Comments), modelState);
        var helper = new GovUkTextareaTagHelper(generator) { For = expression, Label = "Comments", LabelCssClass = "govuk-label govuk-label--s", ViewContext = viewContext };
        var (context, output) = CreateTagHelperContext("govuk-textarea");

        helper.Process(context, output);

        var html = output.Content.GetContent();
        Assert.Contains("govuk-textarea--error", html, StringComparison.Ordinal);
        Assert.Contains("govuk-label govuk-label--s", html, StringComparison.Ordinal);
        Assert.Contains("Enter comments", html, StringComparison.Ordinal);
    }
}
