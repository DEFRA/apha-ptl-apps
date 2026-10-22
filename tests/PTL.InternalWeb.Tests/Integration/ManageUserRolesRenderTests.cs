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
