using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using PTL.Auth.Cidm.Events;
using PTL.Auth.Cidm.Options;

namespace PTL.Auth.Cidm.Tests.Events;

public class CidmOpenIdConnectEventsTests
{
    private static readonly CidmOptions TestCidmOptions = new()
    {
        Address = "https://cidm.test/idphub/b2c",
        Policy = "b2c_1a_test_signupsignin",
        ClientId = "client-id",
        ClientSecret = "client-secret",
        ServiceId = "service-id-value"
    };

    private static CidmOpenIdConnectEvents CreateEvents() =>
        new(Microsoft.Extensions.Options.Options.Create(TestCidmOptions), NullLogger<CidmOpenIdConnectEvents>.Instance);

    private static AuthenticationScheme CreateScheme() =>
        new("oidc", "oidc", typeof(OpenIdConnectHandler));

    [Fact]
    public async Task RedirectToIdentityProvider_AddsServiceIdParameter()
    {
        var context = new RedirectContext(
            new DefaultHttpContext(), CreateScheme(), new OpenIdConnectOptions(), new AuthenticationProperties())
        {
            ProtocolMessage = new OpenIdConnectMessage()
        };

        await CreateEvents().RedirectToIdentityProvider(context);

        Assert.Equal("service-id-value", context.ProtocolMessage.GetParameter("serviceId"));
    }

    [Fact]
    public async Task RedirectToIdentityProvider_ForceReselectionPropertySet_AddsForceReselectionParameter()
    {
        var properties = new AuthenticationProperties();
        properties.Items[CidmAuthenticationDefaults.ForceReselectionProperty] = "true";
        var context = new RedirectContext(new DefaultHttpContext(), CreateScheme(), new OpenIdConnectOptions(), properties)
        {
            ProtocolMessage = new OpenIdConnectMessage()
        };

        await CreateEvents().RedirectToIdentityProvider(context);

        Assert.Equal("true", context.ProtocolMessage.GetParameter("forceReselection"));
    }

    [Fact]
    public async Task RedirectToIdentityProvider_RelationshipIdPropertySet_AddsRelationshipIdParameter()
    {
        var properties = new AuthenticationProperties();
        properties.Items[CidmAuthenticationDefaults.RelationshipIdProperty] = "rel-123";
        var context = new RedirectContext(new DefaultHttpContext(), CreateScheme(), new OpenIdConnectOptions(), properties)
        {
            ProtocolMessage = new OpenIdConnectMessage()
        };

        await CreateEvents().RedirectToIdentityProvider(context);

        Assert.Equal("rel-123", context.ProtocolMessage.GetParameter("relationshipId"));
    }

    [Fact]
    public async Task RedirectToIdentityProvider_NoOptionalProperties_DoesNotAddOptionalParameters()
    {
        var context = new RedirectContext(new DefaultHttpContext(), CreateScheme(), new OpenIdConnectOptions(), new AuthenticationProperties())
        {
            ProtocolMessage = new OpenIdConnectMessage()
        };

        await CreateEvents().RedirectToIdentityProvider(context);

        Assert.Null(context.ProtocolMessage.GetParameter("forceReselection"));
        Assert.Null(context.ProtocolMessage.GetParameter("relationshipId"));
    }

    [Fact]
    public async Task TokenValidated_ParsesRelationshipsAndRolesIntoClaims()
    {
        var identity = new ClaimsIdentity(
        [
            new Claim("relationships", "23950a2d-c37d-43da-9fcb-0a4ce9aa11ee:bc19305a-f9b6-ea11-a812-000d3ab4653d:ABC Organisation2:0:Employee:0"),
            new Claim("roles", "23950a2d-c37d-43da-9fcb-0a4ce9aa11ee:Certifier:3")
        ]);
        var principal = new ClaimsPrincipal(identity);
        var context = new TokenValidatedContext(new DefaultHttpContext(), CreateScheme(), new OpenIdConnectOptions(), principal, new AuthenticationProperties());

        await CreateEvents().TokenValidated(context);

        Assert.Contains(identity.Claims, c => c.Type == ClaimTypes.Role && c.Value == "Certifier");
        Assert.Contains(identity.Claims, c => c.Type == "cidm:relationship");
        Assert.Contains(identity.Claims, c => c.Type == "cidm:role");
    }

    [Fact]
    public async Task TokenValidated_NoRelationshipsOrRoles_AddsNoExtraClaims()
    {
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "test-user")]);
        var principal = new ClaimsPrincipal(identity);
        var context = new TokenValidatedContext(new DefaultHttpContext(), CreateScheme(), new OpenIdConnectOptions(), principal, new AuthenticationProperties());

        await CreateEvents().TokenValidated(context);

        Assert.DoesNotContain(identity.Claims, c => c.Type == "cidm:relationship" || c.Type == "cidm:role");
    }

    [Fact]
    public async Task TokenValidated_PrincipalWithoutClaimsIdentity_ReturnsWithoutThrowing()
    {
        var context = new TokenValidatedContext(
            new DefaultHttpContext(), CreateScheme(), new OpenIdConnectOptions(), new ClaimsPrincipal(), new AuthenticationProperties());

        await CreateEvents().TokenValidated(context);
    }

    [Fact]
    public async Task RemoteFailure_HandlesResponseAndRedirectsToGenericErrorPage()
    {
        var httpContext = new DefaultHttpContext();
        var context = new RemoteFailureContext(httpContext, CreateScheme(), new OpenIdConnectOptions(), new InvalidOperationException("boom"));

        await CreateEvents().RemoteFailure(context);

        Assert.True(context.Result.Handled);
        Assert.Equal(StatusCodes.Status302Found, httpContext.Response.StatusCode);
        Assert.Equal("/Home/Error", httpContext.Response.Headers.Location.ToString());
    }

    [Fact]
    public async Task RedirectToIdentityProviderForSignOut_RendersHtmlFormWithEndSessionEndpoint()
    {
        var httpContext = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        var context = new RedirectContext(httpContext, CreateScheme(), new OpenIdConnectOptions(), new AuthenticationProperties())
        {
            ProtocolMessage = new OpenIdConnectMessage
            {
                IssuerAddress = "https://cidm.test/signout",
                IdTokenHint = "id-token",
                PostLogoutRedirectUri = "https://app.test/signout-oidc"
            }
        };

        await CreateEvents().RedirectToIdentityProviderForSignOut(context);

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = new StreamReader(httpContext.Response.Body).ReadToEnd();
        Assert.Contains("action=\"https://cidm.test/signout\"", body);
        Assert.Contains("text/html", httpContext.Response.ContentType);
    }

    [Fact]
    public async Task RedirectToIdentityProviderForSignOut_NoIssuerAddress_DoesNotWriteResponse()
    {
        var httpContext = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        var context = new RedirectContext(httpContext, CreateScheme(), new OpenIdConnectOptions(), new AuthenticationProperties())
        {
            ProtocolMessage = new OpenIdConnectMessage()
        };

        await CreateEvents().RedirectToIdentityProviderForSignOut(context);

        Assert.Equal(0, httpContext.Response.Body.Length);
    }
}
