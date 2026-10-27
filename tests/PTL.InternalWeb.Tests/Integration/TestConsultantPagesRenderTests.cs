using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PTL.ApiClient;
using PTL.Contracts.TestConsultant;
using PTL.Contracts.User;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Integration;

// Internal / External Test Consultant pages through the real MVC pipeline: one POST form whose buttons post the whole
// pending list with ?command=..&rowIndex=.., so Edit/Remove/Status/Save/Cancel work without script or browser dialogs.
public partial class TestConsultantPagesRenderTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly Guid IdAlan = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid IdZoe = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

    private const string External = "ExternalTestConsultantManagement";
    private const string Internal = "InternalTestConsultantDepartment";

    [GeneratedRegex("__RequestVerificationToken[^>]*value=\"([^\"]+)\"", RegexOptions.None)]
    private static partial Regex AntiforgeryTokenPattern();

    [GeneratedRegex("<input type=\"hidden\" name=\"([^\"]*)\" value=\"([^\"]*)\" />")]
    private static partial Regex HiddenInputPattern();

    private readonly WebApplicationFactory<Program> _factory;
    private readonly FakeExternalTestConsultantApiClient _external;
    private readonly FakeUserApiClient _internal;

    public TestConsultantPagesRenderTests(WebApplicationFactory<Program> factory)
    {
        _external = new FakeExternalTestConsultantApiClient
        {
            TestConsultants =
            [
                new ExternalTestConsultantResponse(IdAlan, "Alan", "Science", "alan@example.com", false, null),
                new ExternalTestConsultantResponse(IdZoe, "Zoe", "Virology", "zoe@example.com", true, new DateTime(2026, 10, 8), 2, "Scheme A, Scheme B")
            ]
        };
        _internal = new FakeUserApiClient
        {
            TestConsultants =
            [
                new UserResponse(IdAlan, "m100001", "Alan Smith", "Alan", "Smith", "alan@apha.gov.uk", "Science", false, null),
                new UserResponse(IdZoe, "m100002", "Zoe Jones", "Zoe", "Jones", "zoe@apha.gov.uk", "Virology", true, new DateTime(2026, 10, 8), 1, "Scheme A")
            ]
        };

        _factory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IExternalTestConsultantApiClient>();
                services.AddSingleton<IExternalTestConsultantApiClient>(_external);
                services.RemoveAll<IUserApiClient>();
                services.AddSingleton<IUserApiClient>(_internal);
            }));
    }

    private static string MainContent(string html) =>
        Regex.Match(html, "<main[\\s\\S]*?</main>").Value is { Length: > 0 } main ? main : html;

    // Matches a button of the given label whose formaction is ?command=<command>[&rowIndex=<row>].
    private static string Button(string page, string label, string command, int? row = null, string cssClass = "govuk-button--secondary") =>
        $"<button[^>]*{cssClass}[^>]*type=\"submit\"[^>]*formaction=\"/SystemAdministration/{page}\\?command={command}"
        + (row is null ? string.Empty : $"(?:&amp;|&)rowIndex={row}")
        + $"\"[^>]*>{label}";

    private static string Hidden(string name, string value) => $"<input type=\"hidden\" name=\"{Regex.Escape(name)}\" value=\"{Regex.Escape(value)}\" />";

    private static KeyValuePair<string, string> Field(string name, string value) => new(name, value);

    private static List<KeyValuePair<string, string>> ExternalRows(params (Guid? Id, string Name, string Department, string Email, bool IsInactive)[] rows) =>
        [.. rows.SelectMany((r, i) => new[]
        {
            Field($"Rows[{i}].ExternalTestConsultantId", r.Id?.ToString() ?? string.Empty),
            Field($"Rows[{i}].Name", r.Name),
            Field($"Rows[{i}].Department", r.Department),
            Field($"Rows[{i}].Email", r.Email),
            Field($"Rows[{i}].IsInactive", r.IsInactive ? "true" : "false"),
            Field($"Rows[{i}].InactiveDate", string.Empty),
            Field($"Rows[{i}].IsRemoved", "false")
        })];

    private static List<KeyValuePair<string, string>> SavedExternalRows() =>
        ExternalRows((IdAlan, "Alan", "Science", "alan@example.com", false), (IdZoe, "Zoe", "Virology", "zoe@example.com", true));

    private static List<KeyValuePair<string, string>> SavedInternalRows() =>
    [
        Field("Rows[0].UserId", IdAlan.ToString()), Field("Rows[0].Username", "m100001"), Field("Rows[0].FriendlyName", "Alan Smith"),
        Field("Rows[0].Department", "Science"), Field("Rows[0].IsInactive", "false"), Field("Rows[0].InactiveDate", string.Empty),
        Field("Rows[1].UserId", IdZoe.ToString()), Field("Rows[1].Username", "m100002"), Field("Rows[1].FriendlyName", "Zoe Jones"),
        Field("Rows[1].Department", "Virology"), Field("Rows[1].IsInactive", "true"), Field("Rows[1].InactiveDate", string.Empty)
    ];

    private static List<KeyValuePair<string, string>> HiddenFields(string html) =>
        [.. HiddenInputPattern().Matches(html).Select(m => Field(WebUtility.HtmlDecode(m.Groups[1].Value), WebUtility.HtmlDecode(m.Groups[2].Value)))];

    private async Task<(HttpResponseMessage Response, string Cookie)> PostAsync(string page, string command, int? rowIndex, IEnumerable<KeyValuePair<string, string>> fields)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var form = await client.GetAsync($"/SystemAdministration/{page}");
        var token = AntiforgeryTokenPattern().Match(await form.Content.ReadAsStringAsync()).Groups[1].Value;
        var cookie = string.Join("; ", form.Headers.TryGetValues("Set-Cookie", out var cookies) ? cookies.Select(c => c.Split(';')[0]) : []);

        var body = new List<KeyValuePair<string, string>> { Field("__RequestVerificationToken", token) };
        body.AddRange(fields);

        var url = $"/SystemAdministration/{page}?command={command}" + (rowIndex is null ? string.Empty : $"&rowIndex={rowIndex}");
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = new FormUrlEncodedContent(body) };
        request.Headers.Add("Cookie", cookie);
        return (await client.SendAsync(request), cookie);
    }

    private async Task<string> FollowRedirectAsync(HttpResponseMessage response, string cookie)
    {
        var next = new HttpRequestMessage(HttpMethod.Get, response.Headers.Location);
        var tempDataCookies = response.Headers.TryGetValues("Set-Cookie", out var set) ? set.Select(c => c.Split(';')[0]) : [];
        next.Headers.Add("Cookie", string.Join("; ", tempDataCookies.Prepend(cookie)));
        return await (await _factory.CreateClient().SendAsync(next)).Content.ReadAsStringAsync();
    }

    [Theory]
    [InlineData(External)]
    [InlineData(Internal)]
    public async Task Get_RendersActionsAsTheLastColumnWithSecondarySubmitButtonsAndNoLinksScriptOrBrowserDialogs(string page)
    {
        var html = await _factory.CreateClient().GetStringAsync($"/SystemAdministration/{page}");
        var main = MainContent(html);

        var headers = Regex.Matches(main, "<th scope=\"col\" class=\"govuk-table__header\">([^<]*)</th>").Select(m => m.Groups[1].Value).ToList();
        Assert.Equal("Actions", headers[^1]);
        Assert.Equal(1, headers.Count(h => h == "Actions"));
        Assert.Matches(Button(page, "Edit", "edit", 0), main);
        Assert.Matches(Button(page, "Edit", "edit", 1), main);
        Assert.Matches(Button(page, "Active", "toggle", 0), main);
        Assert.Matches(Button(page, "Inactive", "toggle", 1), main);
        Assert.Matches(Button(page, "Save", "save", cssClass: "govuk-button\""), main);
        Assert.Matches(Button(page, "Cancel", "cancel"), main);
        Assert.Matches("formnovalidate[^>]*formaction=\"/SystemAdministration/" + page + "\\?command=cancel\"", main);
        Assert.DoesNotMatch("<a[^>]*govuk-button", main);
        Assert.DoesNotContain(">Edit</a>", main, StringComparison.Ordinal);
        Assert.DoesNotContain("onclick", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("confirm(", html, StringComparison.Ordinal);
        Assert.DoesNotContain("alert(", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Are you sure", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExternalGet_HasRemoveAndAddButtonsNoApplyAndCarriesEachPendingRowInHiddenInputs()
    {
        var main = MainContent(await _factory.CreateClient().GetStringAsync($"/SystemAdministration/{External}"));

        Assert.Matches(Button(External, "Remove", "remove", 0), main);
        Assert.Matches(Button(External, "Remove", "remove", 1), main);
        Assert.DoesNotContain("command=apply", main, StringComparison.Ordinal);
        Assert.Matches(Button(External, "Add", "add"), main);
        Assert.Matches("<table id=\"Rows\"", main);
        Assert.Matches(Hidden("Rows[0].ExternalTestConsultantId", IdAlan.ToString()), main);
        Assert.Matches(Hidden("Rows[0].Name", "Alan"), main);
        Assert.Matches(Hidden("Rows[1].Email", "zoe@example.com"), main);
        Assert.Matches(Hidden("Rows[1].IsInactive", "true"), main);
        Assert.Matches(Hidden("Rows[0].IsRemoved", "false"), main);
        Assert.Matches("<form method=\"post\"[^>]*>\\s*<button type=\"submit\" class=\"govuk-visually-hidden\"[^>]*formaction=\"/SystemAdministration/" + External + "\\?command=submit\"", main);
        Assert.Contains("8 Oct 2026", main, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InternalGet_HasNoRemoveApplyOrAddAndCarriesEachPendingRowInHiddenInputs()
    {
        var main = MainContent(await _factory.CreateClient().GetStringAsync($"/SystemAdministration/{Internal}"));

        Assert.DoesNotContain("command=remove", main, StringComparison.Ordinal);
        Assert.DoesNotContain("command=apply", main, StringComparison.Ordinal);
        Assert.DoesNotContain("command=add", main, StringComparison.Ordinal);
        Assert.Matches("<table id=\"Rows\"", main);
        Assert.Matches(Hidden("Rows[0].UserId", IdAlan.ToString()), main);
        Assert.Matches(Hidden("Rows[0].Department", "Science"), main);
        Assert.Matches(Hidden("Rows[1].IsInactive", "true"), main);
        Assert.Contains("8 Oct 2026", main, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExternalEdit_ShowsTheEditingRowAsVisibleInputsWithRowSaveAndCancelAndWritesNothing()
    {
        var (response, _) = await PostAsync(External, "edit", 1, SavedExternalRows());
        var main = MainContent(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Matches("<input id=\"Edit_Name\" name=\"Edit.Name\" value=\"Zoe\"", main);
        Assert.Matches("<input id=\"Edit_Department\" name=\"Edit.Department\" value=\"Virology\"", main);
        Assert.Matches("<input id=\"Edit_Email\" name=\"Edit.Email\" value=\"zoe@example.com\"", main);
        Assert.Matches("<input type=\"hidden\" name=\"EditIndex\" value=\"1\" />", main);
        Assert.Matches(Button(External, "Save", "update", 1), main);
        Assert.Matches(Button(External, "Cancel", "cancelEdit", 1), main);
        Assert.Matches(Button(External, "Edit", "edit", 0), main);
        Assert.DoesNotMatch(Button(External, "Edit", "edit", 1), main);
        Assert.DoesNotMatch(Button(External, "Remove", "remove", 1), main);
        Assert.Empty(_external.UpdateCalls);
    }

    [Fact]
    public async Task InternalEdit_ShowsTheDepartmentInputWithRowSaveAndCancelAndWritesNothing()
    {
        var (response, _) = await PostAsync(Internal, "edit", 0, SavedInternalRows());
        var main = MainContent(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Matches("<input id=\"Edit_Department\" name=\"Edit.Department\" value=\"Science\"", main);
        Assert.Matches(Button(Internal, "Save", "update", 0), main);
        Assert.Matches(Button(Internal, "Cancel", "cancelEdit", 0), main);
        Assert.Empty(_internal.UpdateTestConsultantCalls);
    }

    [Fact]
    public async Task ExternalUpdate_WithInvalidValues_ShowsTheErrorSummaryAndKeepsTheRowInEditMode()
    {
        var fields = SavedExternalRows();
        fields.Add(Field("EditIndex", "0"));
        fields.Add(Field("Edit.Name", string.Empty));
        fields.Add(Field("Edit.Email", "not-an-email"));

        var (response, _) = await PostAsync(External, "update", 0, fields);
        var main = MainContent(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("govuk-error-summary", main, StringComparison.Ordinal);
        Assert.Contains("Enter a name", main, StringComparison.Ordinal);
        Assert.Contains("Enter an email address in the correct format", main, StringComparison.Ordinal);
        Assert.Matches("<input id=\"Edit_Name\"", main);
        Assert.Empty(_external.UpdateCalls);
    }

    [Fact]
    public async Task ExternalAdd_AppendsAPendingRowWithoutCallingTheApi()
    {
        var fields = SavedExternalRows();
        fields.Add(Field("Add.Name", "Grace"));
        fields.Add(Field("Add.Department", "Chemistry"));
        fields.Add(Field("Add.Email", "grace@example.com"));

        var (response, _) = await PostAsync(External, "add", null, fields);
        var main = MainContent(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Matches(Hidden("Rows[2].ExternalTestConsultantId", string.Empty), main);
        Assert.Matches(Hidden("Rows[2].Name", "Grace"), main);
        Assert.Null(_external.LastCreateRequest);
    }

    [Fact]
    public async Task ExternalRemove_OfAnUnallocatedConsultant_RedisplaysThePageWithoutTheRowAndWithNoConfirmationPageOrDelete()
    {
        var (response, _) = await PostAsync(External, "remove", 0, SavedExternalRows());
        var html = await response.Content.ReadAsStringAsync();
        var main = MainContent(html);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Manage External Test Consultants", main, StringComparison.Ordinal);
        Assert.Matches(Hidden("Rows[0].IsRemoved", "true"), main);
        Assert.DoesNotContain(">Alan</td>", main, StringComparison.Ordinal);
        Assert.DoesNotMatch(Button(External, "Remove", "remove", 0), main);
        Assert.DoesNotContain("Are you sure", html, StringComparison.Ordinal);
        Assert.Empty(_external.DeleteCalls);
    }

    [Fact]
    public async Task ExternalRemove_OfAnAllocatedConsultant_ShowsTheDependencyMessageInTheErrorBannerAndKeepsTheRow()
    {
        var (response, _) = await PostAsync(External, "remove", 1, SavedExternalRows());
        var main = MainContent(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("govuk-notification-banner", main, StringComparison.Ordinal);
        Assert.Contains("This external test consultant cannot be removed because it is being used on 2 scheme(s): Scheme A, Scheme B.", main, StringComparison.Ordinal);
        Assert.Matches(Hidden("Rows[1].IsRemoved", "false"), main);
        Assert.Matches(Button(External, "Remove", "remove", 1), main);
        Assert.Empty(_external.DeleteCalls);
    }

    [Fact]
    public async Task ExternalToggle_OfAnAllocatedConsultant_ShowsTheDependencyMessage()
    {
        var (response, _) = await PostAsync(External, "toggle", 1, SavedExternalRows());
        var main = MainContent(await response.Content.ReadAsStringAsync());

        Assert.Contains("cannot have its Active/Inactive status changed because it is being used on 2 scheme(s)", main, StringComparison.Ordinal);
        Assert.Matches(Hidden("Rows[1].IsInactive", "true"), main);
        Assert.Empty(_external.SetStatusCalls);
    }

    [Fact]
    public async Task InternalToggle_OfAnAllocatedConsultant_ShowsTheDependencyMessage()
    {
        var (response, _) = await PostAsync(Internal, "toggle", 1, SavedInternalRows());
        var main = MainContent(await response.Content.ReadAsStringAsync());

        Assert.Contains("This internal test consultant cannot have its Active/Inactive status changed because it is being used on 1 scheme(s): Scheme A.", main, StringComparison.Ordinal);
        Assert.Matches(Hidden("Rows[1].IsInactive", "true"), main);
        Assert.Empty(_internal.UpdateTestConsultantCalls);
    }

    [Fact]
    public async Task ExternalFlow_RemoveThenSave_DeletesOnSaveRedirectsToIndexAndShowsTheSuccessBanner()
    {
        var (removed, _) = await PostAsync(External, "remove", 0, SavedExternalRows());
        var redisplayed = MainContent(await removed.Content.ReadAsStringAsync());
        Assert.Empty(_external.DeleteCalls);

        var (saved, cookie) = await PostAsync(External, "save", null, HiddenFields(redisplayed));

        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        Assert.Equal("/SystemAdministration/Index", saved.Headers.Location!.OriginalString);
        Assert.Equal([IdAlan], _external.DeleteCalls);
        var index = await FollowRedirectAsync(saved, cookie);
        Assert.Contains("govuk-notification-banner--success", index, StringComparison.Ordinal);
        Assert.Contains("External test consultants saved successfully.", index, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExternalSave_WhenTheApiRefusesTheDelete_StaysOnThePageWithTheError()
    {
        _external.DeleteResult = new ExternalTestConsultantDeleteResponse(false, "Alan is still assigned.");
        var fields = SavedExternalRows();
        fields[fields.FindIndex(f => f.Key == "Rows[0].IsRemoved")] = Field("Rows[0].IsRemoved", "true");

        var (response, _) = await PostAsync(External, "save", null, fields);
        var main = MainContent(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("govuk-error-summary", main, StringComparison.Ordinal);
        Assert.Contains("Alan: Alan is still assigned.", main, StringComparison.Ordinal);
        Assert.Matches(Hidden("Rows[0].IsRemoved", "true"), main);
    }

    [Fact]
    public async Task ExternalApply_WritesAndRedisplaysThePage()
    {
        var fields = SavedExternalRows();
        fields[fields.FindIndex(f => f.Key == "Rows[0].IsInactive")] = Field("Rows[0].IsInactive", "true");

        var (response, _) = await PostAsync(External, "apply", null, fields);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal([(IdAlan, true)], _external.SetStatusCalls);
    }

    [Fact]
    public async Task ExternalFlow_RemoveThenCancel_GoesToIndexAndDeletesNothing()
    {
        var fields = SavedExternalRows();
        fields[fields.FindIndex(f => f.Key == "Rows[0].IsRemoved")] = Field("Rows[0].IsRemoved", "true");

        var (cancel, _) = await PostAsync(External, "cancel", null, fields);

        Assert.Equal(HttpStatusCode.Redirect, cancel.StatusCode);
        Assert.Equal("/SystemAdministration/Index", cancel.Headers.Location!.OriginalString);
        Assert.Empty(_external.DeleteCalls);
        Assert.Empty(_external.SetStatusCalls);
    }

    [Fact]
    public async Task ExternalGet_WhenEveryRowIsRemoved_ShowsTheEmptyMessage()
    {
        var fields = SavedExternalRows();
        fields[fields.FindIndex(f => f.Key == "Rows[0].IsRemoved")] = Field("Rows[0].IsRemoved", "true");
        fields[fields.FindIndex(f => f.Key == "Rows[1].IsRemoved")] = Field("Rows[1].IsRemoved", "true");

        var (response, _) = await PostAsync(External, "submit", null, fields);
        var main = MainContent(await response.Content.ReadAsStringAsync());

        Assert.Contains("There are no External Test Consultants to display.", main, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InternalFlow_ToggleEditThenSave_WritesOnSaveRedirectsToIndexAndShowsTheSuccessBanner()
    {
        var fields = SavedInternalRows();
        fields[fields.FindIndex(f => f.Key == "Rows[0].Department")] = Field("Rows[0].Department", "Chemistry");

        var (saved, cookie) = await PostAsync(Internal, "save", null, fields);

        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        Assert.Equal("/SystemAdministration/Index", saved.Headers.Location!.OriginalString);
        var call = Assert.Single(_internal.UpdateTestConsultantCalls);
        Assert.Equal(IdAlan, call.UserId);
        Assert.Equal("Chemistry", call.Request.Department);
        var index = await FollowRedirectAsync(saved, cookie);
        Assert.Contains("govuk-notification-banner--success", index, StringComparison.Ordinal);
        Assert.Contains("Internal test consultant departments saved successfully.", index, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InternalFlow_ChangeThenCancel_GoesToIndexAndWritesNothing()
    {
        var fields = SavedInternalRows();
        fields[fields.FindIndex(f => f.Key == "Rows[0].Department")] = Field("Rows[0].Department", "Chemistry");

        var (cancel, _) = await PostAsync(Internal, "cancel", null, fields);

        Assert.Equal(HttpStatusCode.Redirect, cancel.StatusCode);
        Assert.Equal("/SystemAdministration/Index", cancel.Headers.Location!.OriginalString);
        Assert.Empty(_internal.UpdateTestConsultantCalls);
    }
}
