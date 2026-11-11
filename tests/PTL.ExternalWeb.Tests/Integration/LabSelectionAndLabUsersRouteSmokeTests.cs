using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using PTL.Contracts.ExternalUser;
using PTL.Contracts.LabUsers;
using PTL.ExternalWeb.Tests.TestSupport;

namespace PTL.ExternalWeb.Tests.Integration;

// The lab selection, Switch lab and Lab users pages through the full MVC pipeline: who sees what, who is turned away, and
// that a user with several labs is held at the lab selection until they choose one.
public class LabSelectionAndLabUsersRouteSmokeTests : IClassFixture<PtlExternalWebTestFactory>
{
    private static readonly Guid ContactId = Guid.Parse("99999999-9999-9999-9999-999999999999");
    private static readonly Guid LabA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid LabB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private readonly PtlExternalWebTestFactory _factory;

    public LabSelectionAndLabUsersRouteSmokeTests(PtlExternalWebTestFactory factory)
    {
        _factory = factory;
        _factory.ExternalUserApi.Labs.Clear();
        _factory.ExternalUserApi.LabContexts.Clear();
        _factory.LabUserApi.Users.Clear();
        _factory.LabUserApi.Found = true;
    }

    private async Task<HttpResponseMessage> GetAsync(string path, string signInQuery, bool followRedirects = false)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = followRedirects });
        var cookies = new Dictionary<string, string>();
        var signIn = await client.GetAsync($"{PtlExternalWebTestFactory.TestSignInPath}?{signInQuery}");
        CookieForwardingHttpClient.CaptureCookies(signIn, cookies);
        return await CookieForwardingHttpClient.SendWithCookiesAsync(client, HttpMethod.Get, path, cookies);
    }

    private static string Participant(int labCount, Guid? lab, string userType = "Participant", string roles = "Participant,Viewer") =>
        $"roles={Uri.EscapeDataString(roles)}&userType={userType}&labCount={labCount}&contactId={ContactId}" +
        (lab is { } id ? $"&participantId={id}&labCode=0142" : string.Empty);

    private const string LabUserRoles = "Lab User,Participant,Viewer";

    private void TwoLabs()
    {
        _factory.ExternalUserApi.Labs.Add(new ExternalLab(LabA, "0142", "Bristol Veterinary Diagnostics"));
        _factory.ExternalUserApi.Labs.Add(new ExternalLab(LabB, "0187", "Northern Laboratory"));
    }

    // ---- lab selection ----

    [Fact]
    public async Task ManyLabsNoneChosen_AnyPageGoesToTheLabSelection()
    {
        var response = await GetAsync("/Home/Index", Participant(2, lab: null));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Lab/Select", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task SelectionPage_ListsTheLabsAsRadiosAndNeedsNoBreadcrumb()
    {
        TwoLabs();

        var response = await GetAsync("/Lab/Select", Participant(2, lab: null));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Select a lab", body);
        Assert.Contains("govuk-radios__input", body);
        Assert.Contains("Lab 0142", body);
        Assert.Contains("Northern Laboratory", body);
        Assert.Contains("Continue", body);
        Assert.DoesNotContain("govuk-error-summary", body);
        Assert.DoesNotContain("govuk-breadcrumbs", body);
    }

    [Fact]
    public async Task SelectionPage_WhenSwitching_PreselectsTheCurrentLab()
    {
        TwoLabs();

        var body = await (await GetAsync("/Lab/Select", Participant(2, LabB))).Content.ReadAsStringAsync();

        Assert.Matches($"value=\"{LabB}\"[^>]*checked", body);
        Assert.DoesNotMatch($"value=\"{LabA}\"[^>]*checked", body);
    }

    [Fact]
    public async Task SelectionPage_WithNoActiveLabs_GoesToTheNoActiveLabPage()
    {
        var response = await GetAsync("/Lab/Select", Participant(2, lab: null));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/NotPermitted?reason=no-active-lab", response.Headers.Location!.OriginalString);
    }

    // ---- Switch lab ----

    [Fact]
    public async Task Header_ShowsSwitchLab_OnlyWhenTheUserHasMoreThanOneLab()
    {
        var many = await (await GetAsync("/Home/Index", Participant(2, LabA))).Content.ReadAsStringAsync();
        var one = await (await GetAsync("/Home/Index", Participant(1, LabA))).Content.ReadAsStringAsync();

        Assert.Contains("Switch lab", many);
        Assert.DoesNotContain("Switch lab", one);
    }

    [Fact]
    public async Task Header_ShowsSwitchLabToALabUserWithSeveralLabsToo()
    {
        var body = await (await GetAsync("/Home/Index", Participant(3, LabA, "LabUser", LabUserRoles))).Content.ReadAsStringAsync();

        Assert.Contains("Switch lab", body);
    }

    // ---- who sees the Lab users section ----

    [Fact]
    public async Task PrimaryContact_SeesTheLabUsersSectionAndMyAccount()
    {
        var body = await (await GetAsync("/Home/Index", Participant(1, LabA))).Content.ReadAsStringAsync();

        Assert.Contains("Lab users", body);
        Assert.Contains("View lab users", body);
        Assert.Contains("Add lab user", body);
        Assert.Contains("My account", body);
    }

    [Fact]
    public async Task LabUser_SeesNeitherMyAccountNorLabUsers_ButEverythingElseAParticipantSees()
    {
        var response = await GetAsync("/Home/Index", Participant(1, LabA, "LabUser", LabUserRoles));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("My account", body);
        Assert.DoesNotContain("Lab users", body);
        Assert.DoesNotContain("Add lab user", body);
        Assert.Contains("Enter results", body);
        Assert.Contains("View reports", body);
        Assert.Contains("Schemes", body);
    }

    [Theory]
    [InlineData("/LabUser/Index")]
    [InlineData("/LabUser/Add")]
    [InlineData("/Customer/Index")]
    [InlineData("/Participant/Index")]
    public async Task LabUser_IsForbiddenFromWhatOnlyThePrimaryContactMayDo(string path)
    {
        var response = await GetAsync(path, Participant(1, LabA, "LabUser", LabUserRoles), followRedirects: true);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Viewer_IsForbiddenFromTheLabUsersPages()
    {
        var response = await GetAsync("/LabUser/Index", "roles=Viewer&userType=Viewer", followRedirects: true);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---- the Lab users pages ----

    [Fact]
    public async Task Index_ShowsEachPersonWithTheirStatusAndTheActionsAvailable()
    {
        _factory.LabUserApi.Users.Add(new LabUserResponse(Guid.NewGuid(), "Active Alex", "alex@lab.example", true, true, true, true));
        _factory.LabUserApi.Users.Add(new LabUserResponse(Guid.NewGuid(), "Inactive Indy", "indy@lab.example", false, false, true, false));

        var response = await GetAsync("/LabUser/Index", Participant(1, LabA));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Active Alex", body);
        Assert.Contains("alex@lab.example", body);
        Assert.Contains("govuk-tag--green", body);
        Assert.Contains("Inactive Indy", body);
        Assert.Contains("govuk-tag--grey", body);
        Assert.Contains("Not sent", body);
        Assert.Equal(LabA, Assert.Single(_factory.LabUserApi.ListCalls.TakeLast(1)));
    }

    [Fact]
    public async Task Index_WithNoPeople_SaysSo()
    {
        var body = await (await GetAsync("/LabUser/Index", Participant(1, LabA))).Content.ReadAsStringAsync();

        Assert.Contains("No lab users have been added to this lab yet.", body);
    }

    [Fact]
    public async Task Add_ShowsTheGdsFormWithTheLab()
    {
        var response = await GetAsync("/LabUser/Add", Participant(1, LabA));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Add lab user", body);
        Assert.Contains("govuk-input", body);
        Assert.Contains("0142", body);
    }

    [Fact]
    public async Task Edit_ShowsThePersonsDetails()
    {
        var id = Guid.NewGuid();
        _factory.LabUserApi.Users.Add(new LabUserResponse(id, "Sam Smith", "sam@lab.example", true, true, true, true));

        var response = await GetAsync($"/LabUser/Edit?labUserId={id}", Participant(1, LabA));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Sam Smith", body);
        Assert.Contains("sam@lab.example", body);
    }

    [Fact]
    public async Task Edit_SharedPerson_ShowsDetailsAsTextWithAnExplanation()
    {
        var id = Guid.NewGuid();
        _factory.LabUserApi.Users.Add(new LabUserResponse(id, "Shared Sam", "shared@lab.example", true, true, false, true));

        var body = await (await GetAsync($"/LabUser/Edit?labUserId={id}", Participant(1, LabA))).Content.ReadAsStringAsync();

        Assert.Contains("Shared Sam", body);
        Assert.Contains("govuk-inset-text", body);
        Assert.DoesNotContain("name=\"Email\"", body);
    }

    [Fact]
    public async Task Edit_UnknownPerson_IsNotFound()
    {
        var response = await GetAsync($"/LabUser/Edit?labUserId={Guid.NewGuid()}", Participant(1, LabA));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task LabUserPages_AreNotFoundWhenThereIsNoLabOnTheSession()
    {
        var response = await GetAsync("/LabUser/Index", Participant(1, lab: null));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
