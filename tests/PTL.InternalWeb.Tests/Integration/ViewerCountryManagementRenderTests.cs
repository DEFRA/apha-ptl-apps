using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PTL.ApiClient;
using PTL.Contracts.Country;
using PTL.Contracts.Viewer;
using PTL.InternalWeb.Authorization;
using PTL.InternalWeb.Features.SystemAdministration;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Integration;

// Manage Viewers / Manage Countries through the real MVC pipeline: one POST form whose buttons post the whole pending
// list with ?command=..&rowIndex=.., so Edit/Remove/Save/Cancel work without script or browser dialogs.
public partial class ViewerCountryManagementRenderTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly Guid IdAlan = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid IdZoe = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
    private static readonly Guid IdFrance = Guid.Parse("cccccccc-0000-0000-0000-000000000003");
    private static readonly Guid IdGermany = Guid.Parse("dddddddd-0000-0000-0000-000000000004");
    private static readonly Guid TypeEu = Guid.Parse("11111111-0000-0000-0000-000000000001");
    private static readonly Guid TypeNonEu = Guid.Parse("22222222-0000-0000-0000-000000000002");

    [GeneratedRegex("__RequestVerificationToken[^>]*value=\"([^\"]+)\"", RegexOptions.None)]
    private static partial Regex AntiforgeryTokenPattern();

    private readonly WebApplicationFactory<Program> _factory;
    private readonly FakeViewerApiClient _viewers;
    private readonly FakeCountryApiClient _countries;

    public ViewerCountryManagementRenderTests(WebApplicationFactory<Program> factory)
    {
        _viewers = new FakeViewerApiClient
        {
            Viewers =
            [
                new ViewerResponse(IdAlan, "Alan", "alan@example.com", [], []),
                new ViewerResponse(IdZoe, "Zoe", "zoe@example.com", [], [])
            ]
        };
        _countries = new FakeCountryApiClient
        {
            Countries =
            [
                new CountryResponse(IdFrance, "France", TypeEu, "EU", 0),
                new CountryResponse(IdGermany, "Germany", TypeNonEu, "Non-EU", 0)
            ],
            CountryTypes = [new CountryTypeResponse(TypeEu, "EU"), new CountryTypeResponse(TypeNonEu, "Non-EU")]
        };

        _factory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IViewerApiClient>();
                services.AddSingleton<IViewerApiClient>(_viewers);
                services.RemoveAll<ICountryApiClient>();
                services.AddSingleton<ICountryApiClient>(_countries);
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

    private static List<KeyValuePair<string, string>> ViewerRows(params (Guid? Id, string Name, string Email)[] rows) =>
        [.. rows.SelectMany((r, i) => new[]
        {
            Field($"Rows[{i}].ViewerId", r.Id?.ToString() ?? string.Empty),
            Field($"Rows[{i}].Name", r.Name),
            Field($"Rows[{i}].Email", r.Email),
            Field($"Rows[{i}].IsRemoved", "false")
        })];

    private static List<KeyValuePair<string, string>> CountryRows(params (Guid? Id, string Name, Guid TypeId)[] rows) =>
        [.. rows.SelectMany((r, i) => new[]
        {
            Field($"Rows[{i}].CountryId", r.Id?.ToString() ?? string.Empty),
            Field($"Rows[{i}].Country", r.Name),
            Field($"Rows[{i}].CountryTypeId", r.TypeId.ToString()),
            Field($"Rows[{i}].IsRemoved", "false")
        })];

    // Flags row <index> as removed, as the page's hidden Rows[i].IsRemoved input does after Remove.
    private static List<KeyValuePair<string, string>> WithRemoved(List<KeyValuePair<string, string>> fields, params int[] indices)
    {
        foreach (var i in indices)
        {
            fields[fields.FindIndex(f => f.Key == $"Rows[{i}].IsRemoved")] = Field($"Rows[{i}].IsRemoved", "true");
        }

        return fields;
    }

    [GeneratedRegex("<input type=\"hidden\" name=\"([^\"]*)\" value=\"([^\"]*)\" />")]
    private static partial Regex HiddenInputPattern();

    // The hidden inputs a browser would post back from the given HTML fragment.
    private static List<KeyValuePair<string, string>> HiddenFields(string html) =>
        [.. HiddenInputPattern().Matches(html).Select(m => Field(WebUtility.HtmlDecode(m.Groups[1].Value), WebUtility.HtmlDecode(m.Groups[2].Value)))];

    private static bool IsRemovedFlag(string html, int row, string expected) =>
        Regex.IsMatch(html, $"<input type=\"hidden\" name=\"Rows\\[{row}\\]\\.IsRemoved\" value=\"{expected}\" />");

    private static List<KeyValuePair<string, string>> SavedViewerRows() => ViewerRows((IdAlan, "Alan", "alan@example.com"), (IdZoe, "Zoe", "zoe@example.com"));

    private static List<KeyValuePair<string, string>> SavedCountryRows() => CountryRows((IdFrance, "France", TypeEu), (IdGermany, "Germany", TypeNonEu));

    private async Task<(HttpResponseMessage Response, string Cookie)> PostAsync(
        string page,
        string command,
        int? rowIndex,
        IEnumerable<KeyValuePair<string, string>> fields)
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
    [InlineData("ViewerManagement")]
    [InlineData("CountryManagement")]
    public async Task Get_RendersEditAndRemoveAsSecondarySubmitButtonsWithNoLinksScriptOrBrowserDialogs(string page)
    {
        var html = await _factory.CreateClient().GetStringAsync($"/SystemAdministration/{page}");
        var main = MainContent(html);

        Assert.Matches(Button(page, "Edit", "edit", 0), main);
        Assert.Matches(Button(page, "Remove", "remove", 0), main);
        Assert.Matches(Button(page, "Edit", "edit", 1), main);
        Assert.Matches(Button(page, "Remove", "remove", 1), main);
        Assert.DoesNotMatch("<a[^>]*govuk-button", main);
        Assert.DoesNotContain(">Edit</a>", main, StringComparison.Ordinal);
        Assert.DoesNotContain(">Remove</a>", main, StringComparison.Ordinal);
        Assert.DoesNotContain("onclick", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("confirm(", html, StringComparison.Ordinal);
        Assert.DoesNotContain("alert(", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ViewerGet_CarriesEachPendingRowInHiddenInputsAndPutsTheEnterKeyButtonFirst()
    {
        var main = MainContent(await _factory.CreateClient().GetStringAsync("/SystemAdministration/ViewerManagement"));

        Assert.Matches("<table id=\"Rows\"", main);
        Assert.Matches(Hidden("Rows[0].ViewerId", IdAlan.ToString()), main);
        Assert.Matches(Hidden("Rows[0].Name", "Alan"), main);
        Assert.Matches(Hidden("Rows[0].Email", "alan@example.com"), main);
        Assert.Matches(Hidden("Rows[1].ViewerId", IdZoe.ToString()), main);
        Assert.Matches(Hidden("Rows[0].IsRemoved", "false"), main);
        Assert.Matches("<form method=\"post\"[^>]*>\\s*(?:<input[^>]*/>\\s*)*<button type=\"submit\" class=\"govuk-visually-hidden\"[^>]*formaction=\"/SystemAdministration/ViewerManagement\\?command=submit\"", main);
    }

    [Fact]
    public async Task CountryGet_CarriesEachPendingRowInHiddenInputsAndShowsTheTypeName()
    {
        var main = MainContent(await _factory.CreateClient().GetStringAsync("/SystemAdministration/CountryManagement"));

        Assert.Matches("<table id=\"Rows\"", main);
        Assert.Matches(Hidden("Rows[0].CountryId", IdFrance.ToString()), main);
        Assert.Matches(Hidden("Rows[0].Country", "France"), main);
        Assert.Matches(Hidden("Rows[0].CountryTypeId", TypeEu.ToString()), main);
        Assert.Matches(Hidden("Rows[0].IsRemoved", "false"), main);
        Assert.Matches("<td class=\"govuk-table__cell\">EU</td>", main);
        Assert.Matches("<form method=\"post\"[^>]*>\\s*(?:<input[^>]*/>\\s*)*<button type=\"submit\" class=\"govuk-visually-hidden\"[^>]*formaction=\"/SystemAdministration/CountryManagement\\?command=submit\"", main);
    }

    [Fact]
    public async Task ViewerGet_HasPageLevelSaveAndCancelButtonsAnAddButtonAndNoApply()
    {
        var main = MainContent(await _factory.CreateClient().GetStringAsync("/SystemAdministration/ViewerManagement"));

        Assert.Matches(Button("ViewerManagement", "Save", "save", cssClass: "govuk-button\""), main);
        Assert.DoesNotContain("command=apply", main, StringComparison.Ordinal);
        Assert.DoesNotContain(">Apply<", main, StringComparison.Ordinal);
        Assert.Matches(Button("ViewerManagement", "Cancel", "cancel"), main);
        Assert.Matches(Button("ViewerManagement", "Add", "add"), main);
        Assert.Matches("formnovalidate[^>]*formaction=\"/SystemAdministration/ViewerManagement\\?command=cancel\"", main);
    }

    [Fact]
    public async Task CountryGet_HasPageLevelSaveAndCancelButtonsAnAddButtonAndNoApply()
    {
        var main = MainContent(await _factory.CreateClient().GetStringAsync("/SystemAdministration/CountryManagement"));

        Assert.Matches(Button("CountryManagement", "Save", "save", cssClass: "govuk-button\""), main);
        Assert.Matches(Button("CountryManagement", "Cancel", "cancel"), main);
        Assert.Matches(Button("CountryManagement", "Add", "add"), main);
        Assert.DoesNotContain("command=apply", main, StringComparison.Ordinal);
        Assert.DoesNotContain(">Apply<", main, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ViewerEdit_ShowsTheEditingRowAsVisibleEditInputsWithRowSaveAndCancelAndNoEditOrRemoveForIt()
    {
        var (response, _) = await PostAsync("ViewerManagement", "edit", 1, SavedViewerRows());
        var main = MainContent(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Matches("<input id=\"Edit_Name\" name=\"Edit.Name\" value=\"Zoe\"", main);
        Assert.Matches("<input id=\"Edit_Email\" name=\"Edit.Email\" value=\"zoe@example.com\"", main);
        Assert.Matches("<input type=\"hidden\" name=\"EditIndex\" value=\"1\" />", main);
        Assert.Matches(Hidden("Rows[1].Name", "Zoe"), main);
        Assert.Matches(Button("ViewerManagement", "Save", "update", 1), main);
        Assert.Matches(Button("ViewerManagement", "Cancel", "cancelEdit", 1), main);
        Assert.Matches(Button("ViewerManagement", "Edit", "edit", 0), main);
        Assert.DoesNotMatch(Button("ViewerManagement", "Edit", "edit", 1), main);
        Assert.DoesNotMatch(Button("ViewerManagement", "Remove", "remove", 1), main);
        Assert.Empty(_viewers.UpdateCalls);
    }

    [Fact]
    public async Task CountryEdit_ShowsTheEditingRowAsVisibleEditInputsWithTheTypeSelectedAndRowSaveAndCancel()
    {
        var (response, _) = await PostAsync("CountryManagement", "edit", 1, SavedCountryRows());
        var main = MainContent(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Matches("<input id=\"Edit_Country\" name=\"Edit.Country\" value=\"Germany\"", main);
        Assert.Matches("<select id=\"Edit_CountryTypeId\" name=\"Edit.CountryTypeId\"", main);
        Assert.Matches($"<option value=\"{TypeNonEu}\"[^>]*selected", main);
        Assert.DoesNotMatch($"<option value=\"{TypeEu}\"[^>]*selected", main);
        Assert.Matches("<input type=\"hidden\" name=\"EditIndex\" value=\"1\" />", main);
        Assert.Matches(Button("CountryManagement", "Save", "update", 1), main);
        Assert.Matches(Button("CountryManagement", "Cancel", "cancelEdit", 1), main);
        Assert.DoesNotMatch(Button("CountryManagement", "Remove", "remove", 1), main);
        Assert.Empty(_countries.UpdateRequests);
    }

    [Fact]
    public async Task ViewerUpdate_WithInvalidValues_ShowsTheErrorSummaryAndKeepsTheRowInEditMode()
    {
        var fields = SavedViewerRows();
        fields.Add(Field("EditIndex", "0"));
        fields.Add(Field("Edit.Name", string.Empty));
        fields.Add(Field("Edit.Email", "not-an-email"));

        var (response, _) = await PostAsync("ViewerManagement", "update", 0, fields);
        var main = MainContent(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("govuk-error-summary", main, StringComparison.Ordinal);
        Assert.Contains("Enter a name", main, StringComparison.Ordinal);
        Assert.Contains("Enter an email address in the correct format", main, StringComparison.Ordinal);
        Assert.Matches("<input id=\"Edit_Name\"", main);
        Assert.Empty(_viewers.UpdateCalls);
    }

    [Fact]
    public async Task ViewerAdd_AppendsAPendingRowWithoutCallingTheApi()
    {
        var fields = SavedViewerRows();
        fields.Add(Field("Add.Name", "Grace"));
        fields.Add(Field("Add.Email", "grace@example.com"));

        var (response, _) = await PostAsync("ViewerManagement", "add", null, fields);
        var main = MainContent(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Matches(Hidden("Rows[2].ViewerId", string.Empty), main);
        Assert.Matches(Hidden("Rows[2].Name", "Grace"), main);
        Assert.Empty(_viewers.CreateRequests);
    }

    [Fact]
    public async Task CountryAdd_OfAnExistingNameIgnoringCase_ShowsTheLegacyDuplicateMessageInTheErrorSummary()
    {
        var fields = SavedCountryRows();
        fields.Add(Field("Add.Country", "france"));
        fields.Add(Field("Add.CountryTypeId", TypeEu.ToString()));

        var (response, _) = await PostAsync("CountryManagement", "add", null, fields);
        var main = MainContent(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("govuk-error-summary", main, StringComparison.Ordinal);
        Assert.Contains("This country already exists. Please try with different name.", main, StringComparison.Ordinal);
        Assert.DoesNotMatch(Hidden("Rows[2].Country", "france"), main);
        Assert.Empty(_countries.CreateRequests);
    }

    [Fact]
    public async Task ViewerRemove_RedisplaysThePageWithTheRowFlaggedRemovedAndNotRenderedWithoutDeletingOrRedirecting()
    {
        var (response, _) = await PostAsync("ViewerManagement", "remove", 1, SavedViewerRows());
        var main = MainContent(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.Contains("<h1 class=\"govuk-heading-l\">Manage Viewers</h1>", main, StringComparison.Ordinal);
        Assert.True(IsRemovedFlag(main, 0, "false"));
        Assert.True(IsRemovedFlag(main, 1, "true"));
        Assert.Matches(Hidden("Rows[1].ViewerId", IdZoe.ToString()), main);
        Assert.Matches(Hidden("Rows[1].Name", "Zoe"), main);
        Assert.Contains(">Alan</td>", main, StringComparison.Ordinal);
        Assert.DoesNotContain(">Zoe</td>", main, StringComparison.Ordinal);
        Assert.DoesNotMatch(Button("ViewerManagement", "Edit", "edit", 1), main);
        Assert.DoesNotMatch(Button("ViewerManagement", "Remove", "remove", 1), main);
        Assert.DoesNotContain("There are no Viewers to display.", main, StringComparison.Ordinal);
        Assert.DoesNotContain("saved successfully", main, StringComparison.Ordinal);
        Assert.Empty(_viewers.DeleteCalls);
    }

    [Fact]
    public async Task CountryRemove_RedisplaysThePageWithTheRowFlaggedRemovedAndNotRenderedWithoutDeletingOrRedirecting()
    {
        var (response, _) = await PostAsync("CountryManagement", "remove", 0, SavedCountryRows());
        var main = MainContent(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.Contains("<h1 class=\"govuk-heading-l\">Manage Countries</h1>", main, StringComparison.Ordinal);
        Assert.True(IsRemovedFlag(main, 0, "true"));
        Assert.True(IsRemovedFlag(main, 1, "false"));
        Assert.Matches(Hidden("Rows[0].CountryId", IdFrance.ToString()), main);
        Assert.Matches(Hidden("Rows[0].Country", "France"), main);
        Assert.Matches(Hidden("Rows[0].CountryTypeId", TypeEu.ToString()), main);
        Assert.DoesNotContain(">France</td>", main, StringComparison.Ordinal);
        Assert.Contains(">Germany</td>", main, StringComparison.Ordinal);
        Assert.DoesNotMatch(Button("CountryManagement", "Remove", "remove", 0), main);
        Assert.DoesNotContain("There are no Countries to display.", main, StringComparison.Ordinal);
        Assert.DoesNotContain("saved successfully", main, StringComparison.Ordinal);
        Assert.Empty(_countries.DeleteCalls);
    }

    [Theory]
    [InlineData("ViewerManagement", "There are no Viewers to display.")]
    [InlineData("CountryManagement", "There are no Countries to display.")]
    public async Task WhenEveryRowIsRemoved_TheEmptyStateShowsNoRowIsRenderedAndTheHiddenInputsAreStillPosted(string page, string emptyText)
    {
        var fields = page == "ViewerManagement" ? WithRemoved(SavedViewerRows(), 0, 1) : WithRemoved(SavedCountryRows(), 0, 1);

        var (response, _) = await PostAsync(page, "cancelEdit", null, fields);
        var main = MainContent(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(emptyText, main, StringComparison.Ordinal);
        Assert.True(IsRemovedFlag(main, 0, "true"));
        Assert.True(IsRemovedFlag(main, 1, "true"));
        Assert.DoesNotMatch("formaction=\"[^\"]*command=edit", main);
        Assert.DoesNotMatch("formaction=\"[^\"]*command=remove", main);
        Assert.Equal(2, Regex.Count(main, "<tr class=\"govuk-table__row\">"));
    }

    [Theory]
    [InlineData("ViewerManagement")]
    [InlineData("CountryManagement")]
    public async Task RemovedRows_SurviveAnotherCommandRoundTripWithoutBeingRendered(string page)
    {
        var fields = page == "ViewerManagement" ? WithRemoved(SavedViewerRows(), 0) : WithRemoved(SavedCountryRows(), 0);

        var (response, _) = await PostAsync(page, "edit", 1, fields);
        var main = MainContent(await response.Content.ReadAsStringAsync());

        Assert.True(IsRemovedFlag(main, 0, "true"));
        Assert.True(IsRemovedFlag(main, 1, "false"));
        Assert.Matches("<input type=\"hidden\" name=\"EditIndex\" value=\"1\" />", main);
        Assert.DoesNotContain(page == "ViewerManagement" ? ">Alan</td>" : ">France</td>", main, StringComparison.Ordinal);
        Assert.DoesNotMatch(Button(page, "Edit", "edit", 0), main);
    }

    [Fact]
    public async Task ViewerFlow_RemoveThenSave_DeletesOnSaveRedirectsToIndexAndShowsTheSuccessBanner()
    {
        var (removed, _) = await PostAsync("ViewerManagement", "remove", 1, SavedViewerRows());
        var redisplayed = MainContent(await removed.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
        Assert.True(IsRemovedFlag(redisplayed, 1, "true"));
        Assert.Empty(_viewers.DeleteCalls);

        var posted = HiddenFields(redisplayed);
        var (saved, cookie) = await PostAsync("ViewerManagement", "save", null, posted);

        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        Assert.Equal("/SystemAdministration/Index", saved.Headers.Location!.OriginalString);
        Assert.Equal([IdZoe], _viewers.DeleteCalls);
        Assert.Empty(_viewers.CreateRequests);
        Assert.Empty(_viewers.UpdateCalls);
        var index = await FollowRedirectAsync(saved, cookie);
        Assert.Contains("govuk-notification-banner--success", index, StringComparison.Ordinal);
        Assert.Contains("Viewers saved successfully.", index, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CountryFlow_RemoveThenSave_DeletesOnSaveRedirectsToIndexAndShowsTheSuccessBanner()
    {
        var (removed, _) = await PostAsync("CountryManagement", "remove", 0, SavedCountryRows());
        var redisplayed = MainContent(await removed.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
        Assert.True(IsRemovedFlag(redisplayed, 0, "true"));
        Assert.Empty(_countries.DeleteCalls);

        var (saved, cookie) = await PostAsync("CountryManagement", "save", null, HiddenFields(redisplayed));

        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        Assert.Equal("/SystemAdministration/Index", saved.Headers.Location!.OriginalString);
        Assert.Equal([IdFrance], _countries.DeleteCalls);
        Assert.Empty(_countries.CreateRequests);
        Assert.Empty(_countries.UpdateRequests);
        var index = await FollowRedirectAsync(saved, cookie);
        Assert.Contains("govuk-notification-banner--success", index, StringComparison.Ordinal);
        Assert.Contains("Countries saved successfully.", index, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ViewerFlow_RemoveThenApply_DeletesAndRedisplaysThePageWithoutTheRemovedRow()
    {
        var (apply, _) = await PostAsync("ViewerManagement", "apply", null, WithRemoved(SavedViewerRows(), 0));
        var main = MainContent(await apply.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, apply.StatusCode);
        Assert.Equal([IdAlan], _viewers.DeleteCalls);
        Assert.Matches(Hidden("Rows[0].ViewerId", IdZoe.ToString()), main);
        Assert.DoesNotMatch(Hidden("Rows[1].ViewerId", IdZoe.ToString()), main);
        Assert.DoesNotMatch(Hidden("Rows[0].ViewerId", IdAlan.ToString()), main);
    }

    [Fact]
    public async Task ViewerFlow_RemoveThenCancel_GoesToIndexAndDeletesNothing()
    {
        var (cancel, _) = await PostAsync("ViewerManagement", "cancel", null, WithRemoved(SavedViewerRows(), 0));

        Assert.Equal(HttpStatusCode.Redirect, cancel.StatusCode);
        Assert.Equal("/SystemAdministration/Index", cancel.Headers.Location!.OriginalString);
        Assert.Empty(_viewers.DeleteCalls);
    }

    [Fact]
    public async Task CountryFlow_RemoveThenCancel_GoesToIndexAndDeletesNothing()
    {
        var (cancel, _) = await PostAsync("CountryManagement", "cancel", null, WithRemoved(SavedCountryRows(), 0));

        Assert.Equal(HttpStatusCode.Redirect, cancel.StatusCode);
        Assert.Equal("/SystemAdministration/Index", cancel.Headers.Location!.OriginalString);
        Assert.Empty(_countries.DeleteCalls);
    }

    [Fact]
    public async Task ViewerSave_WhenTheRemovedRowCannotBeDeleted_StaysOnThePageWithTheErrorAndTheRowStillFlaggedRemoved()
    {
        _viewers.DeleteResult = new ViewerDeleteResponse(false, "Alan is still assigned.");

        var (saved, _) = await PostAsync("ViewerManagement", "save", null, WithRemoved(SavedViewerRows(), 0));
        var main = MainContent(await saved.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Contains("govuk-error-summary", main, StringComparison.Ordinal);
        Assert.Contains("Alan: Alan is still assigned.", main, StringComparison.Ordinal);
        Assert.True(IsRemovedFlag(main, 0, "true"));
        Assert.DoesNotContain(">Alan</td>", main, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CountrySave_WhenTheRemovedRowCannotBeDeleted_StaysOnThePageWithTheErrorAndTheRowStillFlaggedRemoved()
    {
        _countries.DeleteResponse = new CountryDeleteResponse(false, "France is in use.");

        var (saved, _) = await PostAsync("CountryManagement", "save", null, WithRemoved(SavedCountryRows(), 0));
        var main = MainContent(await saved.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Contains("govuk-error-summary", main, StringComparison.Ordinal);
        Assert.Contains("France: France is in use.", main, StringComparison.Ordinal);
        Assert.True(IsRemovedFlag(main, 0, "true"));
        Assert.DoesNotContain(">France</td>", main, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ViewerRemove_WhenAssignedToSchemesOrParticipants_ShowsTheBlockedMessageWithDetails()
    {
        _viewers.Viewers =
        [
            new ViewerResponse(IdAlan, "Alan", "alan@example.com", [new ViewerSchemeResponse("SFW1234", "Heavy Metals")], [new ViewerParticipantResponse("LAB001", "Example Lab")]),
            new ViewerResponse(IdZoe, "Zoe", "zoe@example.com", [], [])
        ];

        var (response, _) = await PostAsync("ViewerManagement", "remove", 0, SavedViewerRows());
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("This viewer cannot be removed because it is assigned to one or more schemes or participants.", html, StringComparison.Ordinal);
        Assert.Contains("Scheme SFW1234: Heavy Metals", html, StringComparison.Ordinal);
        Assert.Contains("Participant LAB001: Example Lab", html, StringComparison.Ordinal);
        Assert.Contains("<h1 class=\"govuk-heading-l\">Manage Viewers</h1>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("govuk-button--warning", html, StringComparison.Ordinal);
        Assert.Empty(_viewers.DeleteCalls);
    }

    [Fact]
    public async Task CountryRemove_WhenInUse_ShowsTheBlockedMessage()
    {
        _countries.Countries = [new CountryResponse(IdFrance, "France", TypeEu, "EU", 8), new CountryResponse(IdGermany, "Germany", TypeNonEu, "Non-EU", 0)];

        var (response, _) = await PostAsync("CountryManagement", "remove", 0, SavedCountryRows());
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("This country cannot be removed because it is being used by 8 customer(s)/participant(s)/Group Addresses.", html, StringComparison.Ordinal);
        Assert.Contains("<h1 class=\"govuk-heading-l\">Manage Countries</h1>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("govuk-button--warning", html, StringComparison.Ordinal);
        Assert.Empty(_countries.DeleteCalls);
    }

    [Fact]
    public async Task ViewerSave_WritesOnlyNewAndChangedRowsThenGoesToSystemAdministrationWithTheSuccessBanner()
    {
        var fields = ViewerRows((IdAlan, "Alan", "alan@example.com"), (IdZoe, "Zoe Q", "zoe@example.com"), (null, "Grace", "grace@example.com"));

        var (response, cookie) = await PostAsync("ViewerManagement", "save", null, fields);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/SystemAdministration/Index", response.Headers.Location!.OriginalString);
        Assert.Equal(IdZoe, Assert.Single(_viewers.UpdateCalls).ViewerId);
        Assert.Equal("Grace", Assert.Single(_viewers.CreateRequests).Name);
        Assert.Contains("Viewers saved successfully.", await FollowRedirectAsync(response, cookie), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ViewerApply_WritesAndStaysOnThePage()
    {
        var fields = ViewerRows((IdAlan, "Alan", "alan@example.com"), (null, "Grace", "grace@example.com"));

        var (response, _) = await PostAsync("ViewerManagement", "apply", null, fields);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<h1 class=\"govuk-heading-l\">Manage Viewers</h1>", html, StringComparison.Ordinal);
        Assert.Equal("Grace", Assert.Single(_viewers.CreateRequests).Name);
    }

    [Fact]
    public async Task CountrySave_WritesOnlyNewAndChangedRowsThenGoesToSystemAdministrationWithTheSuccessBanner()
    {
        var fields = CountryRows((IdFrance, "France", TypeEu), (IdGermany, "Germany", TypeEu), (null, "Spain", TypeEu));

        var (response, cookie) = await PostAsync("CountryManagement", "save", null, fields);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/SystemAdministration/Index", response.Headers.Location!.OriginalString);
        Assert.Equal(IdGermany, Assert.Single(_countries.UpdateRequests).CountryId);
        Assert.Equal("Spain", Assert.Single(_countries.CreateRequests).Country);
        Assert.Contains("Countries saved successfully.", await FollowRedirectAsync(response, cookie), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CountrySave_WhenTheApiRejectsARow_StaysOnThePageWithTheRowNamedInTheErrorSummary()
    {
        _countries.SaveResult = new CountrySaveResult(false, null, new Dictionary<string, string[]> { ["Country"] = ["Country already exists"] });

        var (response, _) = await PostAsync("CountryManagement", "save", null, CountryRows((IdFrance, "France", TypeEu), (null, "Spain", TypeEu)));
        var main = MainContent(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("govuk-error-summary", main, StringComparison.Ordinal);
        Assert.Contains("Spain: Country already exists", main, StringComparison.Ordinal);
        Assert.Matches(Hidden("Rows[1].Country", "Spain"), main);
    }

    [Theory]
    [InlineData("ViewerManagement")]
    [InlineData("CountryManagement")]
    public async Task Cancel_GoesToSystemAdministrationWithoutWritingAnything(string page)
    {
        var fields = page == "ViewerManagement" ? ViewerRows((null, "Grace", "grace@example.com")) : CountryRows((null, "Spain", TypeEu));

        var (response, _) = await PostAsync(page, "cancel", null, fields);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/SystemAdministration/Index", response.Headers.Location!.OriginalString);
        Assert.Empty(_viewers.CreateRequests);
        Assert.Empty(_countries.CreateRequests);
    }
}

// The pages sit behind the Admin policy: a signed-in user without the Admin role (or no one) is refused before any POST action
// (and so any API call) runs.
public class ViewerCountryManagementAuthorizationTests(PtlInternalWebTestFactory factory) : IClassFixture<PtlInternalWebTestFactory>
{
    private async Task<HttpClient> SignedInClientAsync(string? roles)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var url = roles is null ? PtlInternalWebTestFactory.TestSignInPath : $"{PtlInternalWebTestFactory.TestSignInPath}?roles={Uri.EscapeDataString(roles)}";
        Assert.Equal(HttpStatusCode.NoContent, (await client.GetAsync(url)).StatusCode);
        return client;
    }

    private static string RedirectPath(HttpResponseMessage response)
    {
        var location = response.Headers.Location!;
        return location.IsAbsoluteUri ? location.AbsolutePath : location.OriginalString;
    }

    public static TheoryData<string, string> PostCases => new()
    {
        { "ViewerManagement", "remove" }, { "ViewerManagement", "save" }, { "ViewerManagement", "apply" }, { "ViewerManagement", "cancel" },
        { "CountryManagement", "remove" }, { "CountryManagement", "save" }, { "CountryManagement", "cancel" }
    };

    [Theory]
    [MemberData(nameof(PostCases))]
    public async Task Post_ByANonAdminRole_IsRefusedWithAccessDenied(string page, string command)
    {
        var client = await SignedInClientAsync(roles: "Contracts Admin");

        var response = await client.PostAsync($"/SystemAdministration/{page}?command={command}&rowIndex=0", new FormUrlEncodedContent([]));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/AccessDenied", RedirectPath(response));
    }

    [Theory]
    [InlineData("ViewerManagement")]
    [InlineData("CountryManagement")]
    public async Task Get_ByANonAdminRole_IsRefusedWithAccessDenied(string page)
    {
        var client = await SignedInClientAsync(roles: "Contracts Admin");
        var response = await client.GetAsync($"/SystemAdministration/{page}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/AccessDenied", RedirectPath(response));
    }

    [Theory]
    [InlineData("ViewerManagement")]
    [InlineData("CountryManagement")]
    public async Task Post_ByAnAnonymousUser_IsSentToSignIn(string page)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.PostAsync($"/SystemAdministration/{page}?command=save", new FormUrlEncodedContent([]));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location!.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ThePostActions_AreInheritedFromTheAdminOnlyController_AndNoneOptsOutOfAuthorization()
    {
        var controller = typeof(SystemAdministrationController);
        Assert.Equal(PtlPolicies.Admin, Assert.Single(controller.GetCustomAttributes<AuthorizeAttribute>()).Policy);

        var actions = controller.GetMethods().Where(m => m.Name is nameof(SystemAdministrationController.ViewerManagement) or nameof(SystemAdministrationController.CountryManagement)).ToList();

        Assert.Equal(4, actions.Count);
        Assert.Equal(2, actions.Count(m => m.GetCustomAttribute<HttpPostAttribute>() is not null));
        Assert.All(actions, m => Assert.Empty(m.GetCustomAttributes<AllowAnonymousAttribute>()));
    }
}
