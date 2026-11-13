using System.Security.Claims;
using Microsoft.Extensions.Logging.Abstractions;
using PTL.Auth.Entra.Claims;
using PTL.Contracts.InternalUser;
using PTL.InternalWeb.Features.Account;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Features.Account;

public class InternalUserResolverTests
{
    private static ClaimsPrincipal CreatePrincipal(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "Test"));

    [Fact]
    public async Task ResolveAsync_NullPrincipal_Throws()
    {
        var resolver = new InternalUserResolver(new FakeInternalUserApiClient(new ResolveInternalUserResponse(false, null, "", "", [])), NullLogger<InternalUserResolver>.Instance);

        await Assert.ThrowsAsync<ArgumentNullException>(() => resolver.ResolveAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task ResolveAsync_MissingObjectIdClaim_DeniesWithNotPermittedRedirect()
    {
        var principal = CreatePrincipal(new Claim(EntraClaimTypes.OnPremisesSamAccountName, "auser"));
        var resolver = new InternalUserResolver(new FakeInternalUserApiClient(new ResolveInternalUserResponse(false, null, "", "", [])), NullLogger<InternalUserResolver>.Instance);

        var result = await resolver.ResolveAsync(principal, CancellationToken.None);

        Assert.False(result.IsAllowed);
        Assert.Equal("/Account/NotPermitted", result.DenialRedirectPath);
    }

    [Fact]
    public async Task ResolveAsync_MissingSamAccountNameAndDomain_DeniesWithNotPermittedRedirect()
    {
        var principal = CreatePrincipal(new Claim(EntraClaimTypes.ObjectId, Guid.NewGuid().ToString()));
        var resolver = new InternalUserResolver(new FakeInternalUserApiClient(new ResolveInternalUserResponse(false, null, "", "", [])), NullLogger<InternalUserResolver>.Instance);

        var result = await resolver.ResolveAsync(principal, CancellationToken.None);

        Assert.False(result.IsAllowed);
        Assert.Equal("/Account/NotPermitted", result.DenialRedirectPath);
    }

    [Fact]
    public async Task ResolveAsync_NotPermittedByApi_DeniesWithNotPermittedRedirect()
    {
        var principal = CreatePrincipal(
            new Claim(EntraClaimTypes.ObjectId, Guid.NewGuid().ToString()),
            new Claim(EntraClaimTypes.OnPremisesSamAccountName, "auser"),
            new Claim(EntraClaimTypes.OnPremisesDomainName, "DEFRA"));
        var apiClient = new FakeInternalUserApiClient(new ResolveInternalUserResponse(false, null, "", "", []));
        var resolver = new InternalUserResolver(apiClient, NullLogger<InternalUserResolver>.Instance);

        var result = await resolver.ResolveAsync(principal, CancellationToken.None);

        Assert.False(result.IsAllowed);
        Assert.Equal("/Account/NotPermitted", result.DenialRedirectPath);
    }

    [Fact]
    public async Task ResolveAsync_PermittedByApi_AllowsAndAddsClaims()
    {
        var objectId = Guid.NewGuid();
        var principal = CreatePrincipal(
            new Claim(EntraClaimTypes.ObjectId, objectId.ToString()),
            new Claim(EntraClaimTypes.OnPremisesSamAccountName, "auser"),
            new Claim(EntraClaimTypes.OnPremisesDomainName, "DEFRA.local"));
        var apiClient = new FakeInternalUserApiClient(new ResolveInternalUserResponse(true, Guid.NewGuid(), "Alice User", "QAU", ["Admin", "Distributions"]));
        var resolver = new InternalUserResolver(apiClient, NullLogger<InternalUserResolver>.Instance);

        var result = await resolver.ResolveAsync(principal, CancellationToken.None);

        Assert.True(result.IsAllowed);
        Assert.Equal("Alice User", result.Claims![InternalUserClaimTypes.FullName]);
        Assert.Equal("QAU", result.Claims![InternalUserClaimTypes.Department]);
        Assert.Equal("Admin,Distributions", result.Claims![InternalUserClaimTypes.ResolvedRoles]);
        Assert.Equal(objectId, apiClient.ReceivedRequest!.SsoIdInt);
        // Domain truncated at first '.' (NetBIOS-style), matching legacy fldUsername format.
        Assert.Equal("DEFRA\\auser", apiClient.ReceivedRequest!.Username);
    }

    [Fact]
    public async Task ResolveAsync_ObjectIdLongClaimUriFallback_StillResolves()
    {
        var objectId = Guid.NewGuid();
        var principal = CreatePrincipal(
            new Claim(EntraClaimTypes.ObjectIdLongClaimUri, objectId.ToString()),
            new Claim(EntraClaimTypes.OnPremisesSamAccountName, "auser"),
            new Claim(EntraClaimTypes.OnPremisesDomainName, "DEFRA"));
        var apiClient = new FakeInternalUserApiClient(new ResolveInternalUserResponse(true, Guid.NewGuid(), "Alice User", "QAU", ["Admin"]));
        var resolver = new InternalUserResolver(apiClient, NullLogger<InternalUserResolver>.Instance);

        var result = await resolver.ResolveAsync(principal, CancellationToken.None);

        Assert.True(result.IsAllowed);
        Assert.Equal(objectId, apiClient.ReceivedRequest!.SsoIdInt);
    }

    [Fact]
    public async Task ResolveAsync_UsesTheDirectoryEmployeeId_WhenTheTokenHasNoAccountClaims()
    {
        var objectId = Guid.NewGuid();
        var principal = CreatePrincipal(new Claim(EntraClaimTypes.ObjectId, objectId.ToString()));
        var apiClient = new FakeInternalUserApiClient(new ResolveInternalUserResponse(true, Guid.NewGuid(), "Alice User", "QAU", ["Admin"]));
        var lookup = new FakeEmployeeIdLookup { EmployeeId = "12345" };

        var result = await new InternalUserResolver(apiClient, NullLogger<InternalUserResolver>.Instance, lookup).ResolveAsync(principal, CancellationToken.None);

        Assert.True(result.IsAllowed);
        Assert.Equal(objectId, lookup.RequestedObjectId);
        Assert.Equal("DEMETER\\12345", apiClient.ReceivedRequest!.Username);
    }

    [Fact]
    public async Task ResolveAsync_PrefersTheDirectoryEmployeeId_ToABareAccountName()
    {
        var principal = CreatePrincipal(
            new Claim(EntraClaimTypes.ObjectId, Guid.NewGuid().ToString()),
            new Claim(EntraClaimTypes.OnPremisesSamAccountName, "auser"));
        var apiClient = new FakeInternalUserApiClient(new ResolveInternalUserResponse(false, null, "", "", []));

        await new InternalUserResolver(apiClient, NullLogger<InternalUserResolver>.Instance, new FakeEmployeeIdLookup { EmployeeId = "12345" })
            .ResolveAsync(principal, CancellationToken.None);

        Assert.Equal("DEMETER\\12345", apiClient.ReceivedRequest!.Username);
    }

    [Fact]
    public async Task ResolveAsync_DoesNotQueryTheDirectory_WhenTheTokenGivesADomainQualifiedName()
    {
        var principal = CreatePrincipal(
            new Claim(EntraClaimTypes.ObjectId, Guid.NewGuid().ToString()),
            new Claim(EntraClaimTypes.OnPremisesSamAccountName, "auser"),
            new Claim(EntraClaimTypes.OnPremisesDomainName, "DEFRA"));
        var apiClient = new FakeInternalUserApiClient(new ResolveInternalUserResponse(false, null, "", "", []));
        var lookup = new FakeEmployeeIdLookup { EmployeeId = "12345" };

        await new InternalUserResolver(apiClient, NullLogger<InternalUserResolver>.Instance, lookup).ResolveAsync(principal, CancellationToken.None);

        Assert.Null(lookup.RequestedObjectId);
        Assert.Equal("DEFRA\\auser", apiClient.ReceivedRequest!.Username);
    }

    [Fact]
    public async Task ResolveAsync_KeepsTheBareAccountName_WhenTheDirectoryHasNoEmployeeId()
    {
        var principal = CreatePrincipal(
            new Claim(EntraClaimTypes.ObjectId, Guid.NewGuid().ToString()),
            new Claim(EntraClaimTypes.OnPremisesSamAccountName, "auser"));
        var apiClient = new FakeInternalUserApiClient(new ResolveInternalUserResponse(false, null, "", "", []));

        await new InternalUserResolver(apiClient, NullLogger<InternalUserResolver>.Instance, new FakeEmployeeIdLookup())
            .ResolveAsync(principal, CancellationToken.None);

        Assert.Equal("auser", apiClient.ReceivedRequest!.Username);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    [InlineData("12\\34")]
    public async Task ResolveAsync_DeniesWithoutCallingApi_WhenTheDirectoryEmployeeIdIsMissingOrUnusable(string? employeeId)
    {
        var principal = CreatePrincipal(new Claim(EntraClaimTypes.ObjectId, Guid.NewGuid().ToString()));
        var apiClient = new FakeInternalUserApiClient(new ResolveInternalUserResponse(true, Guid.NewGuid(), "Alice User", "QAU", []));

        var result = await new InternalUserResolver(apiClient, NullLogger<InternalUserResolver>.Instance, new FakeEmployeeIdLookup { EmployeeId = employeeId })
            .ResolveAsync(principal, CancellationToken.None);

        Assert.False(result.IsAllowed);
        Assert.Equal("/Account/NotPermitted", result.DenialRedirectPath);
        Assert.Null(apiClient.ReceivedRequest);
    }

    [Fact]
    public async Task ResolveAsync_DeniesWithoutCallingApi_WhenTheDirectoryLookupFailsAndTheTokenHasNoAccountName()
    {
        var principal = CreatePrincipal(new Claim(EntraClaimTypes.ObjectId, Guid.NewGuid().ToString()));
        var apiClient = new FakeInternalUserApiClient(new ResolveInternalUserResponse(true, Guid.NewGuid(), "Alice User", "QAU", []));

        var result = await new InternalUserResolver(apiClient, NullLogger<InternalUserResolver>.Instance, new FakeEmployeeIdLookup { Throw = true })
            .ResolveAsync(principal, CancellationToken.None);

        Assert.False(result.IsAllowed);
        Assert.Equal("/Account/NotPermitted", result.DenialRedirectPath);
        Assert.Null(apiClient.ReceivedRequest);
    }

    [Fact]
    public async Task ResolveAsync_FallsBackToTheBareAccountName_WhenTheDirectoryLookupFails()
    {
        var principal = CreatePrincipal(
            new Claim(EntraClaimTypes.ObjectId, Guid.NewGuid().ToString()),
            new Claim(EntraClaimTypes.OnPremisesSamAccountName, "auser"));
        var apiClient = new FakeInternalUserApiClient(new ResolveInternalUserResponse(false, null, "", "", []));

        await new InternalUserResolver(apiClient, NullLogger<InternalUserResolver>.Instance, new FakeEmployeeIdLookup { Throw = true })
            .ResolveAsync(principal, CancellationToken.None);

        Assert.Equal("auser", apiClient.ReceivedRequest!.Username);
    }

    [Fact]
    public async Task ResolveAsync_ApiCallThrowsHttpRequestException_DeniesWithErrorRedirect()
    {
        var principal = CreatePrincipal(
            new Claim(EntraClaimTypes.ObjectId, Guid.NewGuid().ToString()),
            new Claim(EntraClaimTypes.OnPremisesSamAccountName, "auser"),
            new Claim(EntraClaimTypes.OnPremisesDomainName, "DEFRA"));
        var apiClient = new FakeInternalUserApiClient(new ResolveInternalUserResponse(false, null, "", "", []), new HttpRequestException("Connection refused"));
        var resolver = new InternalUserResolver(apiClient, NullLogger<InternalUserResolver>.Instance);

        var result = await resolver.ResolveAsync(principal, CancellationToken.None);

        Assert.False(result.IsAllowed);
        Assert.Equal("/Home/Error", result.DenialRedirectPath);
    }
}


