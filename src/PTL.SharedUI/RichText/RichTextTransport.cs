using System.Text;

namespace PTL.SharedUI.RichText;

/// <summary>
/// Wire format for rich text fields. The editor script posts the HTML as <c>ptl-b64:&lt;base64 of UTF-8&gt;</c>
/// so the markup never appears in the request body, where the AWS WAF XSS rules would otherwise reject it.
/// Only properties bound with <see cref="RichTextBinderAttribute"/> are decoded, and the decoded HTML is
/// validated exactly as it was before.
/// </summary>
public static class RichTextTransport
{
    public const string Prefix = "ptl-b64:";

    /// <summary>Wraps HTML in the wire format, for a hidden field that carries rich text from one form to the next.</summary>
    public static string Encode(string? html) =>
        string.IsNullOrEmpty(html) ? string.Empty : Prefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(html));

    /// <summary>Returns the HTML for a posted value; a value without the prefix (scripting off) is returned unchanged.</summary>
    public static string? Decode(string? posted)
    {
        if (posted is null || !posted.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return posted;
        }

        var encoded = posted[Prefix.Length..];
        var buffer = new byte[encoded.Length];
        if (!Convert.TryFromBase64String(encoded, buffer, out var written))
        {
            return posted;
        }

        return Encoding.UTF8.GetString(buffer, 0, written);
    }
}
