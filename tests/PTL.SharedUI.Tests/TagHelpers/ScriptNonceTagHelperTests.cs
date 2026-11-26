using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Razor.TagHelpers;
using PTL.SharedUI.TagHelpers;

namespace PTL.SharedUI.Tests.TagHelpers;

public class ScriptNonceTagHelperTests
{
    private const string Policy = "default-src 'self'; script-src 'self' 'nonce-0123abcd'; object-src 'none'";

    [Theory]
    [InlineData(null)]
    [InlineData("module")]
    [InlineData("text/javascript")]
    [InlineData("")]
    public void Process_InlineScript_GetsTheNonceThePolicyAllows(string? type)
    {
        var output = Process(Policy, type is null ? [] : [new TagHelperAttribute("type", type)]);

        Assert.Equal("0123abcd", output.Attributes["nonce"].Value);
    }

    // Allowed by 'self' already, and the markup other tests and caches rely on stays as it was.
    [Fact]
    public void Process_ScriptLoadedBySrc_IsLeftAlone()
    {
        var output = Process(Policy, [new TagHelperAttribute("src", "/js/app.js")]);

        Assert.False(output.Attributes.ContainsName("nonce"));
    }

    // A data block never runs, so it needs no nonce.
    [Fact]
    public void Process_JsonDataBlock_IsLeftAlone()
    {
        var output = Process(Policy, [new TagHelperAttribute("type", "application/json")]);

        Assert.False(output.Attributes.ContainsName("nonce"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("default-src 'self'")]
    [InlineData("script-src 'nonce-")]
    [InlineData("script-src 'nonce-'")]
    public void Process_WithoutAUsableNonce_LeavesTheScriptAlone(string policy)
    {
        var output = Process(policy, []);

        Assert.False(output.Attributes.ContainsName("nonce"));
    }

    private static TagHelperOutput Process(string policy, TagHelperAttributeList attributes)
    {
        var httpContext = new DefaultHttpContext();
        if (policy.Length > 0)
        {
            httpContext.Response.Headers.ContentSecurityPolicy = policy;
        }

        var helper = new ScriptNonceTagHelper { ViewContext = new ViewContext { HttpContext = httpContext } };
        var context = new TagHelperContext([], new Dictionary<object, object>(), "id");
        var output = new TagHelperOutput("script", attributes, (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));

        helper.Process(context, output);
        return output;
    }
}
