using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using PTL.Auth.Entra.Claims;
using PTL.Common.Auth;
using PTL.InternalWeb.Authorization;
using PTL.InternalWeb.Features.Account;

namespace PTL.InternalWeb.Tests.Authorization;

// Policy outcomes must match the legacy Web.config <location> folders and IsInRole checks. Legacy Admin is not
// a superuser: every policy needs its own role.
public class PtlPoliciesTests
{
    private static readonly ServiceProvider Provider = new ServiceCollection()
        .AddLogging()
        .AddPtlAuthorization()
        .BuildServiceProvider();

    private static async Task<bool> AllowedAsync(string? ptlRoles, string policy)
    {
        var claims = new List<Claim>();
        if (ptlRoles is not null)
        {
            claims.Add(new Claim(InternalUserClaimTypes.ResolvedRoles, ptlRoles));
        }

        var principal = await new PtlRoleClaimsTransformation().TransformAsync(
            new ClaimsPrincipal(new ClaimsIdentity(claims, ptlRoles is null ? null : "test")));

        var result = await Provider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(principal, policy);
        return result.Succeeded;
    }

    [Theory]
    [InlineData("Admin", PtlPolicies.Admin, true)]
    [InlineData("Admin", PtlPolicies.ContractsAdmin, false)]
    [InlineData("Admin", PtlPolicies.SchemeAdmin, false)]
    [InlineData("Admin", PtlPolicies.Distributions, false)]
    [InlineData("Contracts Admin", PtlPolicies.ContractsAdmin, true)]
    [InlineData("Contracts Admin", PtlPolicies.Admin, false)]
    [InlineData("Scheme Admin", PtlPolicies.SchemeAdmin, true)]
    [InlineData("Scheme Admin", PtlPolicies.ContractsAdmin, false)]
    [InlineData("Internal User", PtlPolicies.InternalUser, true)]
    [InlineData("Internal User", PtlPolicies.Scheduling, false)]
    [InlineData("Scheduling", PtlPolicies.Scheduling, true)]
    [InlineData("Scheduling", PtlPolicies.InternalUser, false)]
    [InlineData("Distributions", PtlPolicies.Distributions, true)]
    [InlineData("Distributions", PtlPolicies.Scheduling, false)]
    [InlineData("Test Consultant", PtlPolicies.TestConsultant, true)]
    [InlineData("Assessor", PtlPolicies.Assessor, true)]
    [InlineData("Results Sign-off", PtlPolicies.ResultsSignOff, true)]
    [InlineData("Assessor", PtlPolicies.ResultsSignOff, false)]
    [InlineData("Override Schemes", PtlPolicies.OverrideSchemes, true)]
    [InlineData("Override Schemes", PtlPolicies.ContractsAdmin, false)]
    [InlineData("Support", PtlPolicies.Support, true)]
    [InlineData("Support", PtlPolicies.Admin, false)]
    public async Task EachPolicy_NeedsItsOwnPtlRole(string ptlRoles, string policy, bool expected) =>
        Assert.Equal(expected, await AllowedAsync(ptlRoles, policy));

    [Theory]
    [InlineData("Contracts Admin,Generate Invoices", true)]
    [InlineData("Contracts Admin", false)]
    [InlineData("Generate Invoices", false)]
    [InlineData("Admin,Scheme Admin", false)]
    public async Task InvoiceGeneration_NeedsContractsAdminAndGenerateInvoices(string ptlRoles, bool expected) =>
        Assert.Equal(expected, await AllowedAsync(ptlRoles, PtlPolicies.InvoiceGeneration));

    [Theory]
    [InlineData(PtlPolicies.Admin)]
    [InlineData(PtlPolicies.ContractsAdmin)]
    [InlineData(PtlPolicies.SchemeAdmin)]
    [InlineData(PtlPolicies.InternalUser)]
    [InlineData(PtlPolicies.Scheduling)]
    [InlineData(PtlPolicies.Distributions)]
    [InlineData(PtlPolicies.TestConsultant)]
    [InlineData(PtlPolicies.Assessor)]
    [InlineData(PtlPolicies.ResultsSignOff)]
    [InlineData(PtlPolicies.InvoiceGeneration)]
    [InlineData(PtlPolicies.OverrideSchemes)]
    [InlineData(PtlPolicies.Support)]
    public async Task EveryPolicy_RefusesASignedInUserWithNoPtlRoles(string policy) =>
        Assert.False(await AllowedAsync(string.Empty, policy));

    [Fact]
    public async Task EveryPolicy_RefusesAnAnonymousUser()
    {
        foreach (var policy in new[] { PtlPolicies.Admin, PtlPolicies.ContractsAdmin, PtlPolicies.InvoiceGeneration, PtlPolicies.Scheduling })
        {
            Assert.False(await AllowedAsync(null, policy));
        }
    }

    [Fact]
    public async Task FallbackPolicy_RequiresASignedInUser()
    {
        var provider = Provider.GetRequiredService<IAuthorizationPolicyProvider>();

        var fallback = await provider.GetFallbackPolicyAsync();

        Assert.NotNull(fallback);
        Assert.Contains(fallback.Requirements, requirement => requirement is Microsoft.AspNetCore.Authorization.Infrastructure.DenyAnonymousAuthorizationRequirement);
    }

    // Authorization is driven by PTL roles only: the Entra role and group claim types are never kept on the cookie.
    [Theory]
    [InlineData(ClaimTypes.Role)]
    [InlineData("roles")]
    [InlineData("groups")]
    [InlineData("wids")]
    public void EntraRoleAndGroupClaims_AreNotRetainedOnTheSessionCookie(string claimType)
    {
        var retained = OpenIdConnectEventHelpers.RetainedClaims(EntraClaimTypes.ObjectId, EntraClaimTypes.ObjectIdLongClaimUri);

        Assert.DoesNotContain(claimType, retained);
    }
}
