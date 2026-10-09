using System.Security.Claims;
using Microsoft.Extensions.Logging.Abstractions;
using PTL.Auth.Cidm.Claims;
using PTL.Contracts.ExternalUser;
using PTL.ExternalWeb.Features.Account;
using PTL.ExternalWeb.Tests.TestSupport;

namespace PTL.ExternalWeb.Tests.Features.Account;

public class ExternalUserResolverTests
{
    private static readonly NullLogger<ExternalUserResolver> Logger = NullLogger<ExternalUserResolver>.Instance;

    private static ClaimsPrincipal CreatePrincipal(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "Test"));

    [Fact]
    public async Task ResolveAsync_NullPrincipal_Throws()
    {
        var resolver = new ExternalUserResolver(new FakeExternalUserApiClient(new ResolveExternalUserResponse("", [], null, null, null, null)), Logger);

        await Assert.ThrowsAsync<ArgumentNullException>(() => resolver.ResolveAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task ResolveAsync_MissingContactIdClaim_DeniesWithNotPermittedRedirect()
    {
        var principal = CreatePrincipal(new Claim(ClaimTypes.Email, "user@example.com"));
        var resolver = new ExternalUserResolver(new FakeExternalUserApiClient(new ResolveExternalUserResponse("", [], null, null, null, null)), Logger);

        var result = await resolver.ResolveAsync(principal, CancellationToken.None);

        Assert.False(result.IsAllowed);
        Assert.Equal("/Account/NotPermitted", result.DenialRedirectPath);
    }

    [Fact]
    public async Task ResolveAsync_ZeroRolesResolved_DeniesWithNotPermittedRedirect()
    {
        var principal = CreatePrincipal(
            new Claim(CidmClaimTypes.ContactId, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Email, "user@example.com"));
        var apiClient = new FakeExternalUserApiClient(new ResolveExternalUserResponse("Jane Doe", [], null, null, null, null));
        var resolver = new ExternalUserResolver(apiClient, Logger);

        var result = await resolver.ResolveAsync(principal, CancellationToken.None);

        Assert.False(result.IsAllowed);
        Assert.Equal("/Account/NotPermitted", result.DenialRedirectPath);
    }

    [Fact]
    public async Task ResolveAsync_RolesResolved_AllowsAndAddsDisplayNameAndRolesClaims()
    {
        var ssoIdExt = Guid.NewGuid();
        var principal = CreatePrincipal(
            new Claim(CidmClaimTypes.ContactId, ssoIdExt.ToString()),
            new Claim(ClaimTypes.Email, "user@example.com"),
            new Claim(ClaimTypes.Role, "Viewer"),
            new Claim(ClaimTypes.Role, "Participant"));
        var apiClient = new FakeExternalUserApiClient(new ResolveExternalUserResponse("Jane Doe", ["Viewer", "Participant"], Guid.NewGuid(), "LAB001", Guid.NewGuid(), null));
        var resolver = new ExternalUserResolver(apiClient, Logger);

        var result = await resolver.ResolveAsync(principal, CancellationToken.None);

        Assert.True(result.IsAllowed);
        Assert.Equal("Jane Doe", result.Claims![ExternalUserClaimTypes.DisplayName]);
        Assert.Equal("Viewer,Participant", result.Claims![ExternalUserClaimTypes.ResolvedRoles]);
        Assert.Equal("LAB001", result.Claims![ExternalUserClaimTypes.LabCode]);
        Assert.Equal(ssoIdExt, apiClient.ReceivedRequest!.SsoIdExt);
        Assert.Equal(["Viewer", "Participant"], apiClient.ReceivedRequest!.Roles);
    }

    [Fact]
    public async Task ResolveAsync_NoLabCodeInResponse_DoesNotAddLabCodeClaim()
    {
        var principal = CreatePrincipal(
            new Claim(CidmClaimTypes.ContactId, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, "Viewer"));
        var apiClient = new FakeExternalUserApiClient(new ResolveExternalUserResponse("Jane Doe", ["Viewer"], null, null, Guid.NewGuid(), null));
        var resolver = new ExternalUserResolver(apiClient, Logger);

        var result = await resolver.ResolveAsync(principal, CancellationToken.None);

        Assert.True(result.IsAllowed);
        Assert.False(result.Claims!.ContainsKey(ExternalUserClaimTypes.LabCode));
    }

    [Fact]
    public async Task ResolveAsync_DisplayName_PrefersFirstNameLastNameClaims()
    {
        var principal = CreatePrincipal(
            new Claim(CidmClaimTypes.ContactId, Guid.NewGuid().ToString()),
            new Claim("firstName", "Jane"),
            new Claim("lastName", "Doe"),
            new Claim(ClaimTypes.Name, "fallback-name"),
            new Claim(ClaimTypes.Role, "Viewer"));
        var apiClient = new FakeExternalUserApiClient(new ResolveExternalUserResponse("Jane Doe", ["Viewer"], null, null, Guid.NewGuid(), null));
        var resolver = new ExternalUserResolver(apiClient, Logger);

        await resolver.ResolveAsync(principal, CancellationToken.None);

        Assert.Equal("Jane Doe", apiClient.ReceivedRequest!.DisplayName);
    }

    [Fact]
    public async Task ResolveAsync_DisplayName_FallsBackToNameClaimWhenNoFirstLastName()
    {
        var principal = CreatePrincipal(
            new Claim(CidmClaimTypes.ContactId, Guid.NewGuid().ToString()),
            new Claim("name", "Jane Fallback"),
            new Claim(ClaimTypes.Role, "Viewer"));
        var apiClient = new FakeExternalUserApiClient(new ResolveExternalUserResponse("Jane Fallback", ["Viewer"], null, null, Guid.NewGuid(), null));
        var resolver = new ExternalUserResolver(apiClient, Logger);

        await resolver.ResolveAsync(principal, CancellationToken.None);

        Assert.Equal("Jane Fallback", apiClient.ReceivedRequest!.DisplayName);
    }

    [Fact]
    public async Task ResolveAsync_DisplayName_NoNameClaimsAtAll_UsesEmptyString()
    {
        var principal = CreatePrincipal(
            new Claim(CidmClaimTypes.ContactId, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, "Viewer"));
        var apiClient = new FakeExternalUserApiClient(new ResolveExternalUserResponse("", ["Viewer"], null, null, Guid.NewGuid(), null));
        var resolver = new ExternalUserResolver(apiClient, Logger);

        await resolver.ResolveAsync(principal, CancellationToken.None);

        Assert.Equal(string.Empty, apiClient.ReceivedRequest!.DisplayName);
    }

    [Fact]
    public async Task ResolveAsync_DuplicateRoleClaims_SendsDistinctRolesOnly()
    {
        var principal = CreatePrincipal(
            new Claim(CidmClaimTypes.ContactId, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, "Viewer"),
            new Claim(ClaimTypes.Role, "Viewer"));
        var apiClient = new FakeExternalUserApiClient(new ResolveExternalUserResponse("Jane Doe", ["Viewer"], null, null, Guid.NewGuid(), null));
        var resolver = new ExternalUserResolver(apiClient, Logger);

        await resolver.ResolveAsync(principal, CancellationToken.None);

        Assert.Equal(["Viewer"], apiClient.ReceivedRequest!.Roles);
    }

    [Fact]
    public async Task ResolveAsync_NoEmailClaim_SendsEmptyEmail()
    {
        var principal = CreatePrincipal(
            new Claim(CidmClaimTypes.ContactId, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, "Viewer"));
        var apiClient = new FakeExternalUserApiClient(new ResolveExternalUserResponse("Jane Doe", ["Viewer"], null, null, Guid.NewGuid(), null));
        var resolver = new ExternalUserResolver(apiClient, Logger);

        await resolver.ResolveAsync(principal, CancellationToken.None);

        Assert.Equal(string.Empty, apiClient.ReceivedRequest!.Email);
    }

    [Fact]
    public async Task ResolveAsync_LowercaseEmailClaimOnly_UsesLowercaseEmailClaim()
    {
        var principal = CreatePrincipal(
            new Claim(CidmClaimTypes.ContactId, Guid.NewGuid().ToString()),
            new Claim("email", "lowercase@example.com"),
            new Claim(ClaimTypes.Role, "Viewer"));
        var apiClient = new FakeExternalUserApiClient(new ResolveExternalUserResponse("Jane Doe", ["Viewer"], null, null, Guid.NewGuid(), null));
        var resolver = new ExternalUserResolver(apiClient, Logger);

        await resolver.ResolveAsync(principal, CancellationToken.None);

        Assert.Equal("lowercase@example.com", apiClient.ReceivedRequest!.Email);
    }

    [Fact]
    public async Task ResolveAsync_DisplayName_FallsBackToGivenNameSurnameClaims()
    {
        var principal = CreatePrincipal(
            new Claim(CidmClaimTypes.ContactId, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.GivenName, "Jane"),
            new Claim(ClaimTypes.Surname, "Doe"),
            new Claim(ClaimTypes.Role, "Viewer"));
        var apiClient = new FakeExternalUserApiClient(new ResolveExternalUserResponse("Jane Doe", ["Viewer"], null, null, Guid.NewGuid(), null));
        var resolver = new ExternalUserResolver(apiClient, Logger);

        await resolver.ResolveAsync(principal, CancellationToken.None);

        Assert.Equal("Jane Doe", apiClient.ReceivedRequest!.DisplayName);
    }
}
