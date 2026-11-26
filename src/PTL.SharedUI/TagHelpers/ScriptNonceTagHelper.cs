using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace PTL.SharedUI.TagHelpers;

/// <summary>
/// Adds the nonce from this response's Content-Security-Policy to the inline scripts a view renders, so they keep
/// running while the policy refuses any inline script the app did not issue. Scripts loaded by src are already allowed
/// by 'self', and data blocks such as type="application/json" never run, so both are left as they are.
/// </summary>
[HtmlTargetElement("script")]
public class ScriptNonceTagHelper : TagHelper
{
    private const string NoncePrefix = "'nonce-";

    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = default!;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (output.Attributes.ContainsName("src") || !IsExecutable(output))
        {
            return;
        }

        if (NonceOf(ViewContext.HttpContext.Response.Headers.ContentSecurityPolicy.ToString()) is { } nonce)
        {
            output.Attributes.SetAttribute("nonce", nonce);
        }
    }

    private static bool IsExecutable(TagHelperOutput output)
    {
        if (!output.Attributes.TryGetAttribute("type", out var type))
        {
            return true;
        }

        var value = type.Value?.ToString()?.Trim() ?? string.Empty;
        return value.Length == 0
            || value.Equals("module", StringComparison.OrdinalIgnoreCase)
            || value.Equals("text/javascript", StringComparison.OrdinalIgnoreCase);
    }

    private static string? NonceOf(string policy)
    {
        var start = policy.IndexOf(NoncePrefix, StringComparison.Ordinal);
        if (start < 0)
        {
            return null;
        }

        start += NoncePrefix.Length;
        var end = policy.IndexOf('\'', start);
        return end > start ? policy[start..end] : null;
    }
}
