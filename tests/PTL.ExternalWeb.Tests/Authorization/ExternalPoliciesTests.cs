using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using PTL.Contracts.ExternalUser;
using PTL.ExternalWeb.Authorization;
using PTL.ExternalWeb.Features.Account;

namespace PTL.ExternalWeb.Tests.Authorization;

public class ExternalPoliciesTests
{
    private static ClaimsPrincipal PrincipalWithRoles(params string[] roles) =>
        new(new ClaimsIdentity(roles.Select(role => new Claim(ClaimTypes.Role, role)), "test"));

    private static async Task<bool> IsAuthorizedAsync(string policy, params string[] roles)
    {
        using var provider = new ServiceCollection().AddLogging().AddExternalAuthorization().BuildServiceProvider();
        var authorization = provider.GetRequiredService<IAuthorizationService>();

        var result = await authorization.AuthorizeAsync(PrincipalWithRoles(roles), policy);

        return result.Succeeded;
    }

    [Theory]
    [InlineData(ExternalPolicies.Participant, ExternalRoleNames.Participant)]
    [InlineData(ExternalPolicies.Viewer, ExternalRoleNames.Viewer)]
    [InlineData(ExternalPolicies.TestConsultant, ExternalRoleNames.TestConsultant)]
    [InlineData(ExternalPolicies.LabUser, ExternalRoleNames.LabUser)]
    public async Task Policy_IsSatisfiedByItsOwnRoleOnly(string policy, string role)
    {
        Assert.True(await IsAuthorizedAsync(policy, role));
        Assert.False(await IsAuthorizedAsync(policy, "Some Other Role"));
        Assert.False(await IsAuthorizedAsync(policy));
    }

    [Fact]
    public async Task LabUserPolicy_IsNotSatisfiedByAParticipant()
    {
        Assert.False(await IsAuthorizedAsync(ExternalPolicies.LabUser, ExternalRoleNames.Participant, ExternalRoleNames.Viewer));
    }

    [Fact]
    public async Task PrimaryContactPolicy_IsSatisfiedByAParticipantButNotByALabUser()
    {
        using var provider = new ServiceCollection().AddLogging().AddExternalAuthorization().BuildServiceProvider();
        var authorization = provider.GetRequiredService<IAuthorizationService>();

        ClaimsPrincipal Principal(string? userType, params string[] roles)
        {
            var claims = roles.Select(role => new Claim(ClaimTypes.Role, role)).ToList();
            if (userType is not null)
            {
                claims.Add(new Claim(ExternalUserClaimTypes.UserType, userType));
            }

            return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        }

        // A Lab User holds the Participant role too, which is exactly why this policy exists.
        var participant = await authorization.AuthorizeAsync(Principal("Participant", ExternalRoleNames.Participant), ExternalPolicies.PrimaryContact);
        var oldSession = await authorization.AuthorizeAsync(Principal(null, ExternalRoleNames.Participant), ExternalPolicies.PrimaryContact);
        var labUser = await authorization.AuthorizeAsync(Principal("LabUser", ExternalRoleNames.LabUser, ExternalRoleNames.Participant, ExternalRoleNames.Viewer), ExternalPolicies.PrimaryContact);
        var viewer = await authorization.AuthorizeAsync(Principal("Viewer", ExternalRoleNames.Viewer), ExternalPolicies.PrimaryContact);

        Assert.True(participant.Succeeded);
        Assert.True(oldSession.Succeeded);
        Assert.False(labUser.Succeeded);
        Assert.False(viewer.Succeeded);
    }

    [Fact]
    public void Policies_AreNamedAfterTheSharedRoleNames()
    {
        Assert.Equal(ExternalRoleNames.Participant, ExternalPolicies.Participant);
        Assert.Equal(ExternalRoleNames.Viewer, ExternalPolicies.Viewer);
        Assert.Equal("Test Consultant", ExternalPolicies.TestConsultant);
        Assert.Equal("Lab User", ExternalPolicies.LabUser);
    }
}
