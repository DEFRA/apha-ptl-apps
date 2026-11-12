using System.Net;
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
        Assert.DoesNotContain(" name=", own, StringComparison.Ordinal);
        Assert.Single(Regex.Matches(html, $"<input type=\"hidden\" name=\"Rows\\[0\\]\\.RoleSelections\\[{Admin.RoleId}\\]\" value=\"true\" />"));
    }

    [Fact]
    public async Task EverythingElse_StaysEditable()
    {
        var html = await GetSignedInAsAsync(Guid.NewGuid());

        Assert.DoesNotContain("disabled", Box(html, 0, Support), StringComparison.Ordinal);
        Assert.DoesNotContain("disabled", Box(html, 1, Admin), StringComparison.Ordinal);
        Assert.DoesNotContain("disabled", Box(html, 1, Support), StringComparison.Ordinal);
        Assert.Single(Regex.Matches(html, "<input type=\"hidden\" name=\"Rows\\[\\d\\]\\.RoleSelections"));
    }

    // Boxes that share a name read as an unfenced checkbox group (WAVE: "not enclosed in a fieldset"); each box is its own yes/no.
    [Fact]
    public async Task EveryCheckbox_HasItsOwnName()
    {
        var html = await GetSignedInAsAsync(Guid.NewGuid());

        var names = Regex.Matches(html, "<input[^>]*type=\"checkbox\"[^>]*>").Select(m => Regex.Match(m.Value, " name=\"([^\"]*)\"").Groups[1].Value).Where(n => n.Length > 0).ToList();

        // Four boxes; the signed-in user's own Admin box is disabled and unnamed (its hidden input carries the role).
        Assert.Equal(3, names.Count);
        Assert.Equal(names.Count, names.Distinct().Count());
        Assert.All(names, name => Assert.Matches("^Rows\\[\\d\\]\\.RoleSelections\\[[0-9a-f-]{36}\\]$", name));
        Assert.DoesNotContain("SelectedRoleIds", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WhenTheSignedInUserIsNotInTheGrid_NoBoxIsDisabledAndNoHintShows()
    {
        var html = await GetAsync();

        Assert.DoesNotContain("own-admin-hint", html, StringComparison.Ordinal);
        Assert.DoesNotContain("disabled=\"disabled\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Save_BindsEachUniquelyNamedBoxBackIntoItsUsersRoles()
    {
        var me = Guid.NewGuid();
        var other = Guid.NewGuid();
        var userApiClient = new FakeUserApiClient
        {
            UserRoleGrid =
            [
                new UserRoleRowResponse(me, "dt2\\me", "Me Myself", [Admin.RoleId]),
                new UserRoleRowResponse(other, "dt2\\other", "Other Person", [Support.RoleId]),
            ]
        };
        var client = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.Configure<TestAuthHandlerOptions>(TestAuthHandler.SchemeName, options => options.UserId = me);
            services.RemoveAll<IUserApiClient>();
            services.AddSingleton<IUserApiClient>(userApiClient);
            services.RemoveAll<IRoleApiClient>();
            services.AddSingleton<IRoleApiClient>(new FakeRoleApiClient { Roles = [Support, Admin] });
        })).CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var page = await client.GetAsync("/SystemAdministration/ManageUserRoles");
        var html = await page.Content.ReadAsStringAsync();
        var token = Regex.Match(html, "__RequestVerificationToken[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        var cookie = string.Join("; ", page.Headers.TryGetValues("Set-Cookie", out var cookies) ? cookies.Select(c => c.Split(';')[0]) : []);

        // As a browser posts it: own Admin through its hidden input, the other user's Support ticked off and Admin ticked on.
        var form = new List<KeyValuePair<string, string>>
        {
            new("__RequestVerificationToken", token),
            new("Rows[0].UserId", me.ToString()), new("Rows[0].FriendlyName", "Me Myself"),
            new($"Rows[0].RoleSelections[{Admin.RoleId}]", "true"),
            new("Rows[1].UserId", other.ToString()), new("Rows[1].FriendlyName", "Other Person"),
            new($"Rows[1].RoleSelections[{Admin.RoleId}]", "true"),
        };
        var request = new HttpRequestMessage(HttpMethod.Post, "/SystemAdministration/ManageUserRoles") { Content = new FormUrlEncodedContent(form) };
        request.Headers.Add("Cookie", cookie);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var batch = Assert.Single(userApiClient.SetRolesBatchCalls);
        Assert.Equal([Admin.RoleId], batch.Items.Single(i => i.UserId == me).RoleIds);
        Assert.Equal([Admin.RoleId], batch.Items.Single(i => i.UserId == other).RoleIds);
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
