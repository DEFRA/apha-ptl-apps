using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace PTL.InternalWeb.TagHelpers;

/// <summary>
/// Renders a complete GOV.UK Design System text-input form group (label, optional hint, error
/// message, and the input itself) from a single element - replaces the label/error-check/input
/// block that was otherwise hand-written and duplicated for every field across the Customer,
/// Participant, Contract, and Scheme forms. Delegates the actual input markup to
/// <see cref="IHtmlGenerator"/> (the same service <c>asp-for</c> uses internally) so value
/// binding/formatting behaves identically to the native tag helpers.
/// </summary>
[HtmlTargetElement("govuk-input", Attributes = ForAttributeName)]
public class GovUkInputTagHelper(IHtmlGenerator generator) : TagHelper
{
    private const string ForAttributeName = "asp-for";

    [HtmlAttributeName(ForAttributeName)]
    public ModelExpression For { get; set; } = default!;

    public string Label { get; set; } = string.Empty;

    public string? Hint { get; set; }

    [HtmlAttributeName("input-class")]
    public string InputCssClass { get; set; } = "govuk-input";

    public bool Disabled { get; set; }

    /// <summary>Overrides the rendered &lt;input&gt;'s type attribute (e.g. "date"); defaults to "text".</summary>
    public string? Type { get; set; }

    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = default!;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var field = For.Name;
        var hasError = ViewContext.ViewData.ModelState.TryGetValue(field, out var entry) && entry.Errors.Count > 0;
        var describedBy = GovUkFormGroupMarkup.Render(output, ViewContext, field, Label, Hint, hasError, entry);

        var htmlAttributes = new Dictionary<string, object>
        {
            ["class"] = hasError ? $"{InputCssClass} {InputCssClass}--error" : InputCssClass
        };
        if (describedBy is not null)
        {
            htmlAttributes["aria-describedby"] = describedBy;
        }
        if (Disabled)
        {
            htmlAttributes["disabled"] = "disabled";
        }
        if (!string.IsNullOrEmpty(Type))
        {
            htmlAttributes["type"] = Type;
        }

        var input = generator.GenerateTextBox(ViewContext, For.ModelExplorer, field, For.Model, format: null, htmlAttributes);
        output.Content.AppendHtml(input);
    }
}

/// <summary>
/// Same purpose as <see cref="GovUkInputTagHelper"/> but for a GOV.UK select (dropdown) field.
/// </summary>
[HtmlTargetElement("govuk-select", Attributes = ForAttributeName)]
public class GovUkSelectTagHelper(IHtmlGenerator generator) : TagHelper
{
    private const string ForAttributeName = "asp-for";

    [HtmlAttributeName(ForAttributeName)]
    public ModelExpression For { get; set; } = default!;

    public string Label { get; set; } = string.Empty;

    public string? Hint { get; set; }

    [HtmlAttributeName("asp-items")]
    public IEnumerable<SelectListItem> Items { get; set; } = [];

    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = default!;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var field = For.Name;
        var hasError = ViewContext.ViewData.ModelState.TryGetValue(field, out var entry) && entry.Errors.Count > 0;
        var describedBy = GovUkFormGroupMarkup.Render(output, ViewContext, field, Label, Hint, hasError, entry);

        var cssClass = hasError ? "govuk-select govuk-select--error" : "govuk-select";
        var htmlAttributes = new Dictionary<string, object> { ["class"] = cssClass };
        if (describedBy is not null)
        {
            htmlAttributes["aria-describedby"] = describedBy;
        }

        var select = generator.GenerateSelect(ViewContext, For.ModelExplorer, optionLabel: null, field, Items, allowMultiple: false, htmlAttributes);
        output.Content.AppendHtml(select);
    }
}

/// <summary>
/// Same purpose as <see cref="GovUkInputTagHelper"/> but for a GOV.UK textarea field - replaces
/// the label/error-check/textarea block that was otherwise hand-written and duplicated across the
/// Customer and Participant forms' free-text fields (Comments, Postage arrangements, etc.).
/// </summary>
[HtmlTargetElement("govuk-textarea", Attributes = ForAttributeName)]
public class GovUkTextareaTagHelper(IHtmlGenerator generator) : TagHelper
{
    private const string ForAttributeName = "asp-for";

    [HtmlAttributeName(ForAttributeName)]
    public ModelExpression For { get; set; } = default!;

    public string Label { get; set; } = string.Empty;

    public string? Hint { get; set; }

    /// <summary>Rendered &lt;textarea&gt; rows; 0 (the default) omits the attribute, matching the browser default.</summary>
    public int Rows { get; set; }

    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = default!;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var field = For.Name;
        var hasError = ViewContext.ViewData.ModelState.TryGetValue(field, out var entry) && entry.Errors.Count > 0;
        var describedBy = GovUkFormGroupMarkup.Render(output, ViewContext, field, Label, Hint, hasError, entry);

        var htmlAttributes = new Dictionary<string, object>
        {
            ["class"] = hasError ? "govuk-textarea govuk-textarea--error" : "govuk-textarea"
        };
        if (describedBy is not null)
        {
            htmlAttributes["aria-describedby"] = describedBy;
        }

        var textarea = generator.GenerateTextArea(ViewContext, For.ModelExplorer, field, rows: Rows, columns: 0, htmlAttributes);
        output.Content.AppendHtml(textarea);
    }
}

/// <summary>
/// Shared "govuk-form-group + label + optional hint + optional error message" wrapper markup used
/// by <see cref="GovUkInputTagHelper"/>, <see cref="GovUkSelectTagHelper"/> and
/// <see cref="GovUkTextareaTagHelper"/> - the only difference between them is the control itself,
/// generated by each caller.
/// </summary>
internal static class GovUkFormGroupMarkup
{
    /// <summary>Renders the wrapper markup into <paramref name="output"/> and returns the aria-describedby id to apply to the control, or null.</summary>
    public static string? Render(TagHelperOutput output, ViewContext viewContext, string field, string label, string? hint, bool hasError, ModelStateEntry? entry)
    {
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("class", hasError ? "govuk-form-group govuk-form-group--error" : "govuk-form-group");

        var labelTag = new TagBuilder("label");
        labelTag.AddCssClass("govuk-label");
        labelTag.Attributes["for"] = TagBuilder.CreateSanitizedId(field, "_");
        labelTag.InnerHtml.Append(label);
        output.Content.AppendHtml(labelTag);

        if (!string.IsNullOrEmpty(hint))
        {
            var hintTag = new TagBuilder("div");
            hintTag.AddCssClass("govuk-hint");
            hintTag.InnerHtml.Append(hint);
            output.Content.AppendHtml(hintTag);
        }

        if (!hasError)
        {
            return null;
        }

        var errorId = $"{field}-error";
        var errorTag = new TagBuilder("p");
        errorTag.AddCssClass("govuk-error-message");
        errorTag.Attributes["id"] = errorId;
        var visuallyHidden = new TagBuilder("span");
        visuallyHidden.AddCssClass("govuk-visually-hidden");
        visuallyHidden.InnerHtml.Append("Error:");
        errorTag.InnerHtml.AppendHtml(visuallyHidden);
        errorTag.InnerHtml.Append(" " + entry!.Errors[0].ErrorMessage);
        output.Content.AppendHtml(errorTag);

        return errorId;
    }
}
