using PTL.Auth.Cidm.Events;

namespace PTL.Auth.Cidm.Tests.Events;

public class CidmSignOutFormWriterTests
{
    [Fact]
    public void WriteForm_IncludesEndSessionEndpointAsFormAction()
    {
        var html = CidmSignOutFormWriter.WriteForm("https://cidm.test/signout", "id-token", "https://app.test/signout-oidc", "state-value");

        Assert.Contains("action=\"https://cidm.test/signout\"", html);
        Assert.Contains("method=\"POST\"", html);
    }

    [Fact]
    public void WriteForm_IncludesHiddenFieldsForHint_RedirectUri_AndState()
    {
        var html = CidmSignOutFormWriter.WriteForm("https://cidm.test/signout", "id-token-value", "https://app.test/signout-oidc", "state-value");

        Assert.Contains("name=\"id_token_hint\" value=\"id-token-value\"", html);
        Assert.Contains("name=\"post_logout_redirect_uri\" value=\"https://app.test/signout-oidc\"", html);
        Assert.Contains("name=\"state\" value=\"state-value\"", html);
    }

    [Fact]
    public void WriteForm_NullOptionalValues_RendersEmptyAttributesWithoutThrowing()
    {
        var html = CidmSignOutFormWriter.WriteForm("https://cidm.test/signout", null, null, null);

        Assert.Contains("name=\"id_token_hint\" value=\"\"", html);
    }

    [Fact]
    public void WriteForm_HtmlEncodesValues_ToPreventInjection()
    {
        var html = CidmSignOutFormWriter.WriteForm("https://cidm.test/signout", "<script>alert(1)</script>", null, null);

        Assert.DoesNotContain("value=\"<script>", html);
        Assert.Contains("&lt;script&gt;", html);
    }

    [Fact]
    public void WriteForm_IncludesAutoSubmitScript()
    {
        var html = CidmSignOutFormWriter.WriteForm("https://cidm.test/signout", null, null, null);

        Assert.Contains("cidmSignOutForm", html);
        Assert.Contains(".submit()", html);
    }
}
