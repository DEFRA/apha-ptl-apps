using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace PTL.InternalWeb.TagHelpers;

/// <summary>
/// Renders a GOV.UK form group whose control is a TinyMCE rich text editor, for any model property
/// holding HTML. The &lt;textarea&gt; is the real form field and posts on its own, so the page still
/// works with scripting unavailable; <c>ptl-rich-text-editor.js</c> enhances it in place.
/// Reusable as-is for Tabulations, Comments and any other rich text field - only the toolbar and
/// height differ per screen.
/// </summary>
[HtmlTargetElement("govuk-rich-text", Attributes = ForAttributeName)]
public class GovUkRichTextTagHelper(IHtmlGenerator generator) : TagHelper
{
    private const string ForAttributeName = "asp-for";

    /// <summary>Legacy Scheme.aspx tinyMCE.init toolbar, used when a caller does not override it.</summary>
    public const string DefaultToolbar = "undo redo | bold italic underline | superscript subscript | charmap";

    [HtmlAttributeName(ForAttributeName)]
    public ModelExpression For { get; set; } = default!;

    public string Label { get; set; } = string.Empty;

    public string? Hint { get; set; }

    public string Toolbar { get; set; } = DefaultToolbar;

    /// <summary>Editor height in pixels; legacy Scheme.aspx used 400.</summary>
    public int Height { get; set; } = 400;

    [HtmlAttributeName("readonly")]
    public bool IsReadOnly { get; set; }

    /// <summary>When set, renders the field's value as plain rendered HTML (the stored markup is
    /// already sanitised server-side) with no textarea and no TinyMCE toolbar at all - used by the
    /// Details ("view mode") screen, as distinct from <see cref="IsReadOnly"/> which still renders
    /// an editor, just a disabled one.</summary>
    [HtmlAttributeName("view-mode")]
    public bool ViewMode { get; set; }

    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = default!;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (ViewMode)
        {
            RenderViewMode(output);
            return;
        }

        var field = For.Name;
        var hasError = ViewContext.ViewData.ModelState.TryGetValue(field, out var entry) && entry.Errors.Count > 0;
        var describedBy = GovUkFormGroupMarkup.Render(output, ViewContext, field, Label, Hint, hasError, entry);

        var htmlAttributes = new Dictionary<string, object>
        {
            ["class"] = hasError ? "govuk-textarea govuk-textarea--error ptl-rich-text" : "govuk-textarea ptl-rich-text",
            ["data-module"] = "ptl-rich-text",
            ["data-toolbar"] = Toolbar,
            ["data-height"] = Height
        };

        if (describedBy is not null)
        {
            htmlAttributes["aria-describedby"] = describedBy;
        }

        if (IsReadOnly)
        {
            htmlAttributes["data-readonly"] = "true";
            htmlAttributes["readonly"] = "readonly";
        }

        output.Content.AppendHtml(generator.GenerateTextArea(ViewContext, For.ModelExplorer, field, rows: 12, columns: 0, htmlAttributes));
    }

    private void RenderViewMode(TagHelperOutput output)
    {
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("class", "govuk-form-group");

        var labelTag = new TagBuilder("p");
        labelTag.AddCssClass("govuk-label govuk-label--s");
        labelTag.InnerHtml.Append(Label);
        output.Content.AppendHtml(labelTag);

        var contentTag = new TagBuilder("div");
        contentTag.AddCssClass("govuk-body");
        contentTag.InnerHtml.AppendHtml((string?)For.Model ?? string.Empty);
        output.Content.AppendHtml(contentTag);
    }
}

