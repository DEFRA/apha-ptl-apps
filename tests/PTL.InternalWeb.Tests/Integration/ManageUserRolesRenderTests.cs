using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PTL.ApiClient;
using PTL.Contracts.User;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Integration;

// Renders Features/SystemAdministration/Views/ManageUserRoles.cshtml (legacy Admin/UserRoles.aspx) through the real MVC pipeline.
public class ManageUserRolesRenderTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly RoleResponse Admin = new(Guid.NewGuid(), "Admin");
    private static readonly RoleResponse Support = new(Guid.NewGuid(), "Support");

    private async Task<string> GetAsync()
    {
        var rows = new[] { new UserRoleRowResponse(Guid.NewGuid(), "dt2\\jsmith", "Jane Smith", [Support.RoleId]) };
        var client = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IUserApiClient>();
            services.AddSingleton<IUserApiClient>(new FakeUserApiClient { UserRoleGrid = rows });
            services.RemoveAll<IRoleApiClient>();
            services.AddSingleton<IRoleApiClient>(new FakeRoleApiClient { Roles = [Support, Admin] });
        })).CreateClient();

        return await client.GetStringAsync("/SystemAdministration/ManageUserRoles");
    }

    // One column per role makes the grid wider than the content column; it has to scroll inside the page
    // (like Preparation and Packaging) instead of widening the whole page.
    [Fact]
    public async Task Grid_ScrollsInsideALabelledRegionWithTheUserColumnPinned()
    {
        var html = await GetAsync();

        Assert.Matches("<section class=\"ptl-table-scroll ptl-table-scroll--sticky-first ptl-table-scroll--sticky-second\" aria-label=\"User roles\">\\s*<table", html);
        Assert.Contains("</table>\n    </section>", html.Replace("\r\n", "\n"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Grid_PostsEachUsersLoadedRolesAsOneHiddenField()
    {
        var html = await GetAsync();

        Assert.Matches($"name=\"Rows\\[0\\]\\.OriginalRoleIds\" value=\"{Support.RoleId}\"", html);
    }

    private async Task<string> GetSignedInAsAsync(Guid signedInUserId)
    {
        var rows = new[]
        {
            new UserRoleRowResponse(signedInUserId, "dt2\\me", "Me Myself", [Admin.RoleId, Support.RoleId]),
            new UserRoleRowResponse(Guid.NewGuid(), "dt2\\other", "Other Person", [Admin.RoleId, Support.RoleId]),
        };
        var client = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.Configure<TestAuthHandlerOptions>(TestAuthHandler.SchemeName, options => options.UserId = signedInUserId);
            services.RemoveAll<IUserApiClient>();
            services.AddSingleton<IUserApiClient>(new FakeUserApiClient { UserRoleGrid = rows });
            services.RemoveAll<IRoleApiClient>();
            services.AddSingleton<IRoleApiClient>(new FakeRoleApiClient { Roles = [Support, Admin] });
        })).CreateClient();

        return await client.GetStringAsync("/SystemAdministration/ManageUserRoles");
    }

    private static string Box(string html, int row, RoleResponse role) =>
        Regex.Match(html, $"<input[^>]*id=\"Rows_{row}__{role.RoleId}\"[^>]*>").Value;

    // Legacy PreventAdminUncheck greyed out the Admin box on the signed-in user's own row only.
    [Fact]
    public async Task OwnAdminBox_IsDisabledExplainedAndStillPosted()
    {
        var html = await GetSignedInAsAsync(Guid.NewGuid());

        var own = Box(html, 0, Admin);
        Assert.Contains("disabled=\"disabled\"", own, StringComparison.Ordinal);
        Assert.Contains("checked=\"checked\"", own, StringComparison.Ordinal);
        Assert.Contains("aria-describedby=\"own-admin-hint\"", own, StringComparison.Ordinal);
        Assert.Contains("You cannot change your own Admin access", html, StringComparison.Ordinal);
        Assert.Single(Regex.Matches(html, $"<input type=\"hidden\" name=\"Rows\\[0\\]\\.SelectedRoleIds\" value=\"{Admin.RoleId}\" />"));
    }

    [Fact]
    public async Task EverythingElse_StaysEditable()
    {
        var html = await GetSignedInAsAsync(Guid.NewGuid());

        Assert.DoesNotContain("disabled", Box(html, 0, Support), StringComparison.Ordinal);
        Assert.DoesNotContain("disabled", Box(html, 1, Admin), StringComparison.Ordinal);
        Assert.DoesNotContain("disabled", Box(html, 1, Support), StringComparison.Ordinal);
        Assert.Single(Regex.Matches(html, "<input type=\"hidden\" name=\"Rows\\[\\d\\]\\.SelectedRoleIds\""));
    }

    [Fact]
    public async Task WhenTheSignedInUserIsNotInTheGrid_NoBoxIsDisabledAndNoHintShows()
    {
        var html = await GetAsync();

        Assert.DoesNotContain("own-admin-hint", html, StringComparison.Ordinal);
        Assert.DoesNotContain("disabled=\"disabled\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Grid_ShowsUsernameThenNameThenOneColumnPerRole()
    {
        var html = await GetAsync();

        var headers = Regex.Matches(html, "<th scope=\"col\" class=\"govuk-table__header\">([^<]*)</th>").Select(m => m.Groups[1].Value);
        Assert.Equal(["Username", "Name", "Admin", "Support"], headers);
        Assert.Matches("<th scope=\"row\" class=\"govuk-table__header\">\\s*dt2\\\\jsmith", html);
        Assert.Contains("<td class=\"govuk-table__cell\">Jane Smith</td>", html, StringComparison.Ordinal);
        Assert.Contains("Support for Jane Smith", html, StringComparison.Ordinal);
    }
}
