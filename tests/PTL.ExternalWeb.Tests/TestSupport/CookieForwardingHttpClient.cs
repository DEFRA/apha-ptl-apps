namespace PTL.ExternalWeb.Tests.TestSupport;

// WebApplicationFactory's CookieContainer-backed client isn't reliably forwarding cookies across
// requests in this test host, so cookies are collected and forwarded manually instead - shared
// here since more than one test class needs it (see AccountRouteSmokeTests, HomeNavigationSmokeTests).
internal static class CookieForwardingHttpClient
{
    public static void CaptureCookies(HttpResponseMessage response, Dictionary<string, string> cookies)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var values))
        {
            return;
        }

        foreach (var value in values)
        {
            var nameValue = value.Split(';')[0];
            var separatorIndex = nameValue.IndexOf('=');
            if (separatorIndex > 0)
            {
                cookies[nameValue[..separatorIndex]] = nameValue;
            }
        }
    }

    public static Task<HttpResponseMessage> SendWithCookiesAsync(
        HttpClient client, HttpMethod method, string url, Dictionary<string, string> cookies, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, url) { Content = content };
        request.Headers.Add("Cookie", string.Join("; ", cookies.Values));
        return client.SendAsync(request);
    }
}
