using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PTL.InternalWeb.Authentication;

namespace PTL.InternalWeb.Tests.Authentication;

public class EntraIdAuthenticationExtensionsTests
{
    [Fact]
    public void AddEntraIdAuthentication_WithValidConfiguration_RegistersCookieAndOpenIdConnectSchemes()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(tenantId: "tenant-123", clientId: "client-123", clientSecret: "secret-123");

        services.AddEntraIdAuthentication(configuration);
        using var provider = services.BuildServiceProvider();
        var schemeProvider = provider.GetRequiredService<IAuthenticationSchemeProvider>();
        var schemes = schemeProvider.GetAllSchemesAsync().GetAwaiter().GetResult();

        Assert.Contains(schemes, s => s.Name == CookieAuthenticationDefaults.AuthenticationScheme);
        Assert.Contains(schemes, s => s.Name == OpenIdConnectDefaults.AuthenticationScheme);
    }

    [Fact]
    public void AddEntraIdAuthentication_ConfiguresOpenIdConnectOptions_ForHybridFlowAndFixedCallbackPaths()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(tenantId: "tenant-123", clientId: "client-123", clientSecret: "secret-123");

        services.AddEntraIdAuthentication(configuration);
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<OpenIdConnectOptions>>()
            .Get(OpenIdConnectDefaults.AuthenticationScheme);

        Assert.Equal("https://login.microsoftonline.com/tenant-123/v2.0", options.Authority);
        Assert.Equal("client-123", options.ClientId);
        Assert.Equal("/signin-oidc", options.CallbackPath);
        Assert.Equal("/signout-oidc", options.SignedOutCallbackPath);
        Assert.True(options.UsePkce);
        Assert.True(options.SaveTokens);
        Assert.Contains("openid", options.Scope);
        Assert.Contains("profile", options.Scope);
        Assert.Contains("email", options.Scope);
    }

    [Theory]
    [InlineData("", "client-123", "secret-123")]
    [InlineData("tenant-123", "", "secret-123")]
    [InlineData("tenant-123", "client-123", "")]
    public void AddEntraIdAuthentication_WithMissingRequiredValue_ThrowsInvalidOperationException(
        string tenantId, string clientId, string clientSecret)
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(tenantId, clientId, clientSecret);

        var exception = Assert.Throws<InvalidOperationException>(() => services.AddEntraIdAuthentication(configuration));
        Assert.Contains("EntraId:", exception.Message);
    }

    [Fact]
    public void HandleRemoteFailure_RedirectsToAccessDeniedAndHandlesResponse()
    {
        var httpContext = new DefaultHttpContext();
        var scheme = new AuthenticationScheme(OpenIdConnectDefaults.AuthenticationScheme, null, typeof(OpenIdConnectHandler));
        var context = new RemoteFailureContext(httpContext, scheme, new OpenIdConnectOptions(), new Exception("sign-in failed"));

        EntraIdAuthenticationExtensions.HandleRemoteFailure(context).GetAwaiter().GetResult();

        Assert.True(context.Result!.Handled);
        Assert.Equal(StatusCodes.Status302Found, httpContext.Response.StatusCode);
        Assert.Equal("/Account/AccessDenied", httpContext.Response.Headers.Location.ToString());
    }

    private static IConfiguration BuildConfiguration(string tenantId, string clientId, string clientSecret) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EntraId:Instance"] = "https://login.microsoftonline.com/",
                ["EntraId:TenantId"] = tenantId,
                ["EntraId:ClientId"] = clientId,
                ["EntraId:ClientSecret"] = clientSecret
            })
            .Build();
}
