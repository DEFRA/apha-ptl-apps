using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using PTL.InternalWeb.Authorization;
using PTL.InternalWeb.Features.Account;
using PTL.InternalWeb.Navigation;

namespace PTL.InternalWeb.Tests.Navigation;

// Menu Authorization Matrix: users only see the menu entries their PTL roles open, using the same policies that
// guard the pages (legacy MasterContent skipped children whose folder role the user lacked).
public class SideNavigationAuthorizationTests
{
    private static readonly ServiceProvider Provider = new ServiceCollection()
        .AddLogging()
        .AddPtlAuthorization()
        .BuildServiceProvider();

    private static ClaimsPrincipal User(string? ptlRoles)
    {
        var claims = ptlRoles is null ? [] : new[] { new Claim(InternalUserClaimTypes.ResolvedRoles, ptlRoles) };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, ptlRoles is null ? null : "test"));
        return new PtlRoleClaimsTransformation().TransformAsync(principal).GetAwaiter().GetResult();
    }

    private static async Task<string[]> VisibleTopLevelAsync(string? ptlRoles)
    {
        var visible = await SideNavigationAuthorization.FilterAsync(
            SideNavigationProvider.Build(), User(ptlRoles), Provider.GetRequiredService<IAuthorizationService>());
        return [.. visible.Select(item => item.Text)];
    }

    [Fact]
    public async Task Admin_SeesSystemAdministrationOnly_BesidesHome() =>
        Assert.Equal(["Home", "System Administration"], await VisibleTopLevelAsync("Admin"));

    [Fact]
    public async Task ContractsAdmin_SeesManageContractsOnly_BesidesHome() =>
        Assert.Equal(["Home", "Manage Contracts"], await VisibleTopLevelAsync("Contracts Admin"));

    [Fact]
    public async Task InternalUser_SeesDistributions_ThroughTheSyntheticRole() =>
        Assert.Equal(["Home", "Distributions"], await VisibleTopLevelAsync("Internal User,Distributions"));

    [Fact]
    public async Task SchemeAdmin_SeesManageSchemes() =>
        Assert.Equal(["Home", "Manage Schemes"], await VisibleTopLevelAsync("Scheme Admin"));

    [Fact]
    public async Task TestConsultantAssessorAndSignOffRoles_SeeTheirOwnEntries() =>
        Assert.Equal(["Home", "Test Consultant", "Assessor", "Results Sign-off"], await VisibleTopLevelAsync("Test Consultant,Assessor,Results Sign-off"));

    [Fact]
    public async Task UserWithNoPtlRoles_SeesOnlyHome() =>
        Assert.Equal(["Home"], await VisibleTopLevelAsync(string.Empty));

    [Fact]
    public async Task AllRoles_SeeEveryTopLevelEntry() =>
        Assert.Equal(
            ["Home", "System Administration", "Manage Contracts", "Manage Schemes", "Distributions", "Test Consultant", "Assessor", "Results Sign-off"],
            await VisibleTopLevelAsync("Admin,Contracts Admin,Scheme Admin,Internal User,Scheduling,Distributions,Test Consultant,Assessor,Results Sign-off"));

    [Theory]
    [InlineData("Contracts Admin", false)]
    [InlineData("Contracts Admin,Generate Invoices", true)]
    public async Task InvoiceGeneration_AlsoNeedsTheGenerateInvoicesRole(string ptlRoles, bool expectedVisible)
    {
        var manageContracts = SideNavigationProvider.Build().Single(item => item.Text == "Manage Contracts");

        var visible = await SideNavigationAuthorization.FilterAsync(
            manageContracts.Children, User(ptlRoles), Provider.GetRequiredService<IAuthorizationService>());

        Assert.Equal(expectedVisible, visible.Any(item => item.Text == "Invoice Generation"));
        Assert.Contains(visible, item => item.Text == "Customers");
    }

    [Fact]
    public void Children_InheritTheirParentsPolicy()
    {
        var tree = SideNavigationProvider.Build();

        var systemAdministration = tree.Single(item => item.Text == "System Administration");
        var manageContracts = tree.Single(item => item.Text == "Manage Contracts");

        Assert.All(systemAdministration.Children, child => Assert.Equal(PtlPolicies.Admin, child.RequiredPolicy));
        Assert.Equal(PtlPolicies.ContractsAdmin, manageContracts.Children.Single(item => item.Text == "Customers").RequiredPolicy);
        Assert.Equal(PtlPolicies.InvoiceGeneration, manageContracts.Children.Single(item => item.Text == "Invoice Generation").RequiredPolicy);
        Assert.Null(tree.Single(item => item.Text == "Home").RequiredPolicy);
    }
}
