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

        public decimal? Price { get; set; }
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
        var helper = new GovUkInputTagHelper(generator) { For = expression, Label = "Name", Disabled = true, Type = "email", ViewContext = viewContext };
        var (context, output) = CreateTagHelperContext("govuk-input");

        helper.Process(context, output);

        var html = output.Content.GetContent();
        Assert.Contains("disabled=\"disabled\"", html, StringComparison.Ordinal);
        Assert.Contains("type=\"email\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Input_DateEntryWithDateTimeValue_RendersDdMmYyyyInATextBox()
    {
        var model = new TestModel { StartDate = new DateTime(2027, 4, 8, 0, 0, 0, DateTimeKind.Utc) };
        var (viewContext, expression, generator) = TagHelperRenderContext.Create(model, nameof(TestModel.StartDate));
        var helper = new GovUkInputTagHelper(generator) { For = expression, Label = "Start date", DateEntry = true, ViewContext = viewContext };
        var (context, output) = CreateTagHelperContext("govuk-input");

        helper.Process(context, output);

        var html = output.Content.GetContent();
        Assert.Contains("value=\"08/04/2027\"", html, StringComparison.Ordinal);
        Assert.Contains("type=\"text\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("type=\"date\"", html, StringComparison.Ordinal);
        Assert.Contains("autocomplete=\"off\"", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("10.5000", "10.50")]
    [InlineData("5", "5.00")]
    [InlineData("123.4500", "123.45")]
    [InlineData("10.5050", "10.505")]
    public void Input_WithFormat_RendersTheValueInThatFormatWithoutRoundingAwayDigits(string stored, string expected)
    {
        var model = new TestModel { Price = decimal.Parse(stored, System.Globalization.CultureInfo.InvariantCulture) };
        var (viewContext, expression, generator) = TagHelperRenderContext.Create(model, nameof(TestModel.Price));
        var helper = new GovUkInputTagHelper(generator) { For = expression, Label = "Price", Format = "{0:0.00##}", ViewContext = viewContext };
        var (context, output) = CreateTagHelperContext("govuk-input");

        using (new CultureScope("en-GB"))
        {
            helper.Process(context, output);
        }

        Assert.Contains($"value=\"{expected}\"", output.Content.GetContent(), StringComparison.Ordinal);
    }

    [Fact]
    public void Input_WithoutFormat_RendersTheValuesDefaultString()
    {
        var model = new TestModel { Price = 10.5000m };
        var (viewContext, expression, generator) = TagHelperRenderContext.Create(model, nameof(TestModel.Price));
        var helper = new GovUkInputTagHelper(generator) { For = expression, Label = "Price", ViewContext = viewContext };
        var (context, output) = CreateTagHelperContext("govuk-input");

        using (new CultureScope("en-GB"))
        {
            helper.Process(context, output);
        }

        Assert.Contains("value=\"10.5000\"", output.Content.GetContent(), StringComparison.Ordinal);
    }

    private sealed class CultureScope : IDisposable
    {
        private readonly System.Globalization.CultureInfo _previous = System.Globalization.CultureInfo.CurrentCulture;

        public CultureScope(string name) => System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo(name);

        public void Dispose() => System.Globalization.CultureInfo.CurrentCulture = _previous;
    }

    [Fact]
    public void Input_DateEntryWithNoValue_RendersEmptyValue()
    {
        var model = new TestModel();
        var (viewContext, expression, generator) = TagHelperRenderContext.Create(model, nameof(TestModel.StartDate));
        var helper = new GovUkInputTagHelper(generator) { For = expression, Label = "Start date", DateEntry = true, ViewContext = viewContext };
        var (context, output) = CreateTagHelperContext("govuk-input");

        helper.Process(context, output);

        var html = output.Content.GetContent();
        Assert.Contains("type=\"text\"", html, StringComparison.Ordinal);
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

    [Fact]
    public void Input_WithMaxLength_RendersMaxLengthAttribute()
    {
        var model = new TestModel();
        var (viewContext, expression, generator) = TagHelperRenderContext.Create(model, nameof(TestModel.Name));
        var helper = new GovUkInputTagHelper(generator) { For = expression, Label = "Name", MaxLength = 50, ViewContext = viewContext };
        var (context, output) = CreateTagHelperContext("govuk-input");

        helper.Process(context, output);

        Assert.Contains("maxlength=\"50\"", output.Content.GetContent(), StringComparison.Ordinal);
    }

    [Fact]
    public void Input_WithDescribedBy_LinksTheExternalHintViaAriaDescribedBy()
    {
        var model = new TestModel();
        var (viewContext, expression, generator) = TagHelperRenderContext.Create(model, nameof(TestModel.Name));
        var helper = new GovUkInputTagHelper(generator) { For = expression, Label = "Name", DescribedBy = "column-hint", ViewContext = viewContext };
        var (context, output) = CreateTagHelperContext("govuk-input");

        helper.Process(context, output);

        var html = output.Content.GetContent();
        Assert.Contains("aria-describedby=\"column-hint\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("title=", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Input_WithDescribedByAndError_ListsBothIds()
    {
        var model = new TestModel();
        var modelState = new ModelStateDictionary();
        modelState.AddModelError(nameof(TestModel.Name), "Enter a name");
        var (viewContext, expression, generator) = TagHelperRenderContext.Create(model, nameof(TestModel.Name), modelState);
        var helper = new GovUkInputTagHelper(generator) { For = expression, Label = "Name", DescribedBy = "column-hint", ViewContext = viewContext };
        var (context, output) = CreateTagHelperContext("govuk-input");

        helper.Process(context, output);

        Assert.Matches("aria-describedby=\"column-hint [^\"]*error[^\"]*\"", output.Content.GetContent());
    }

    [Fact]
    public void Input_WithoutDescribedBy_OmitsAriaDescribedBy()
    {
        var model = new TestModel();
        var (viewContext, expression, generator) = TagHelperRenderContext.Create(model, nameof(TestModel.Name));
        var helper = new GovUkInputTagHelper(generator) { For = expression, Label = "Name", ViewContext = viewContext };
        var (context, output) = CreateTagHelperContext("govuk-input");

        helper.Process(context, output);

        Assert.DoesNotContain("aria-describedby", output.Content.GetContent(), StringComparison.Ordinal);
    }

    [Fact]
    public void Input_WithoutMaxLength_OmitsMaxLengthAttribute()
    {
        var model = new TestModel();
        var (viewContext, expression, generator) = TagHelperRenderContext.Create(model, nameof(TestModel.Name));
        var helper = new GovUkInputTagHelper(generator) { For = expression, Label = "Name", ViewContext = viewContext };
        var (context, output) = CreateTagHelperContext("govuk-input");

        helper.Process(context, output);

        Assert.DoesNotContain("maxlength", output.Content.GetContent(), StringComparison.Ordinal);
    }

    [Fact]
    public void Input_WithVisuallyHiddenLabelClass_RendersThatLabelClass()
    {
        var model = new TestModel();
        var (viewContext, expression, generator) = TagHelperRenderContext.Create(model, nameof(TestModel.Name));
        var helper = new GovUkInputTagHelper(generator)
        {
            For = expression,
            Label = "Sample No.",
            LabelCssClass = "govuk-label govuk-visually-hidden",
            ViewContext = viewContext,
        };
        var (context, output) = CreateTagHelperContext("govuk-input");

        helper.Process(context, output);

        Assert.Contains("govuk-label govuk-visually-hidden", output.Content.GetContent(), StringComparison.Ordinal);
    }

    [Fact]
    public void Textarea_WithMaxLength_RendersMaxLengthAttribute()
    {
        var model = new TestModel();
        var (viewContext, expression, generator) = TagHelperRenderContext.Create(model, nameof(TestModel.Comments));
        var helper = new GovUkTextareaTagHelper(generator) { For = expression, Label = "Comments", MaxLength = 8000, ViewContext = viewContext };
        var (context, output) = CreateTagHelperContext("govuk-textarea");

        helper.Process(context, output);

        Assert.Contains("maxlength=\"8000\"", output.Content.GetContent(), StringComparison.Ordinal);
    }
}
