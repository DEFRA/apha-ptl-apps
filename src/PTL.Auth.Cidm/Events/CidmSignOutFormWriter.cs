using System.Text.Encodings.Web;

namespace PTL.Auth.Cidm.Events;

/// <summary>
/// Builds the DEFRA CIDM-recommended auto-submitting POST sign-out form. A plain GET redirect risks
/// exceeding browser URL length limits once id_token_hint is included (it grows with the relationships/
/// roles claims), and a fetch/XHR POST is blocked by CORS - so an auto-submitting HTML form is what the
/// onboarding guide itself recommends and demonstrates.
/// </summary>
internal static class CidmSignOutFormWriter
{
    public static string WriteForm(string endSessionEndpoint, string? idTokenHint, string? postLogoutRedirectUri, string? state)
    {
        var encoder = HtmlEncoder.Default;

        return $"""
            <!DOCTYPE html>
            <html lang="en">
            <head><meta charset="utf-8" /><title>Signing out</title></head>
            <body>
              <form id="cidmSignOutForm" action="{encoder.Encode(endSessionEndpoint)}" method="POST">
                <input type="hidden" name="id_token_hint" value="{encoder.Encode(idTokenHint ?? string.Empty)}" />
                <input type="hidden" name="post_logout_redirect_uri" value="{encoder.Encode(postLogoutRedirectUri ?? string.Empty)}" />
                <input type="hidden" name="state" value="{encoder.Encode(state ?? string.Empty)}" />
                <noscript>
                  <p>Select Continue to finish signing out.</p>
                  <input type="submit" value="Continue" />
                </noscript>
              </form>
              <script>document.getElementById('cidmSignOutForm').submit();</script>
            </body>
            </html>
            """;
    }
}
