using System.Security.Claims;
using Microsoft.Extensions.Logging.Abstractions;
using PTL.Auth.Cidm.Claims;
using PTL.Auth.Cidm.Events;
using PTL.Contracts.ExternalUser;
using PTL.ExternalWeb.Features.Account;
using PTL.ExternalWeb.Tests.TestSupport;

namespace PTL.ExternalWeb.Tests.Features.Account;

public class ExternalUserResolverTests
{
    private static readonly Guid LabId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static ClaimsPrincipal CreatePrincipal(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "Test"));

    private static ClaimsPrincipal PrincipalWithContact(params Claim[] extraClaims) =>
        CreatePrincipal([new Claim(CidmClaimTypes.ContactId, Guid.NewGuid().ToString()), .. extraClaims]);

    private static ResolveExternalUserResponse Resolved(
        ExternalUserType userType,
        string[] roles,
        ExternalLab[]? labs = null,
        Guid? participantId = null,
        string? labCode = null,
        Guid? viewerId = null,
        Guid? testConsultantId = null,
        Guid? labUserId = null,
        bool canOrderOnline = false) =>
        new(ExternalUserResolutionStatus.Resolved, "Jane Doe", roles, labs ?? [], userType, participantId, labCode, viewerId, testConsultantId, labUserId, canOrderOnline);

    private static readonly ResolveExternalUserResponse ViewerResponse = Resolved(ExternalUserType.Viewer, [ExternalRoleNames.Viewer], viewerId: UserId);

    private static Task<CidmExternalUserResolution> ResolveAsync(ClaimsPrincipal principal, FakeExternalUserApiClient apiClient) =>
        new ExternalUserResolver(apiClient, NullLogger<ExternalUserResolver>.Instance).ResolveAsync(principal, CancellationToken.None);

    // ---- the CIDM token ----

    [Fact]
    public async Task ResolveAsync_NullPrincipal_Throws()
    {
        var resolver = new ExternalUserResolver(new FakeExternalUserApiClient(ViewerResponse), NullLogger<ExternalUserResolver>.Instance);

        await Assert.ThrowsAsync<ArgumentNullException>(() => resolver.ResolveAsync(null!, CancellationToken.None));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task ResolveAsync_MissingOrInvalidContactId_DeniesWithGenericNotPermittedAndNeverCallsTheApi(string? contactId)
    {
        var claims = contactId is null ? [new Claim(ClaimTypes.Email, "user@example.com")] : new[] { new Claim(CidmClaimTypes.ContactId, contactId) };
        var apiClient = new FakeExternalUserApiClient(ViewerResponse);

        var result = await ResolveAsync(CreatePrincipal(claims), apiClient);

        Assert.False(result.IsAllowed);
        Assert.Equal("/Account/NotPermitted", result.DenialRedirectPath);
        Assert.Null(apiClient.ReceivedRequest);
    }

    [Fact]
    public async Task ResolveAsync_SendsOnlyTheContactIdEmailAndName_NeverTheTokensRoles()
    {
        var ssoIdExt = Guid.NewGuid();
        var principal = CreatePrincipal(
            new Claim(CidmClaimTypes.ContactId, ssoIdExt.ToString()),
            new Claim(ClaimTypes.Email, "user@example.com"),
            new Claim(ClaimTypes.Role, "Participant"),
            new Claim("roles", "rel:Participant:3"));
        var apiClient = new FakeExternalUserApiClient(ViewerResponse);

        await ResolveAsync(principal, apiClient);

        Assert.Equal(new ResolveExternalUserRequest(ssoIdExt, "user@example.com", string.Empty), apiClient.ReceivedRequest);
    }

    [Fact]
    public async Task ResolveAsync_NoEmailClaim_SendsEmptyEmail()
    {
        var apiClient = new FakeExternalUserApiClient(ViewerResponse);

        await ResolveAsync(PrincipalWithContact(), apiClient);

        Assert.Equal(string.Empty, apiClient.ReceivedRequest!.Email);
    }

    [Fact]
    public async Task ResolveAsync_LowercaseEmailClaimOnly_UsesIt()
    {
        var apiClient = new FakeExternalUserApiClient(ViewerResponse);

        await ResolveAsync(PrincipalWithContact(new Claim("email", "lowercase@example.com")), apiClient);

        Assert.Equal("lowercase@example.com", apiClient.ReceivedRequest!.Email);
    }

    [Fact]
    public async Task ResolveAsync_BothEmailClaims_PrefersTheMappedOne()
    {
        var apiClient = new FakeExternalUserApiClient(ViewerResponse);

        await ResolveAsync(PrincipalWithContact(new Claim(ClaimTypes.Email, "mapped@example.com"), new Claim("email", "raw@example.com")), apiClient);

        Assert.Equal("mapped@example.com", apiClient.ReceivedRequest!.Email);
    }

    [Theory]
    [InlineData("Jane", "Doe", null, null, "Jane Doe")]
    [InlineData("Jane", null, null, null, "Jane")]
    [InlineData(null, "Doe", null, null, "Doe")]
    [InlineData(null, null, "Jane Fallback", null, "Jane Fallback")]
    [InlineData(null, null, null, "Jane Identity", "Jane Identity")]
    [InlineData(null, null, null, null, "")]
    [InlineData("  ", " ", "Jane Fallback", null, "Jane Fallback")]
    public async Task ResolveAsync_DisplayName_UsesFirstAndLastNameThenNameClaims(string? first, string? last, string? name, string? identityName, string expected)
    {
        var claims = new List<Claim>();
        AddIfPresent(claims, "firstName", first);
        AddIfPresent(claims, "lastName", last);
        AddIfPresent(claims, "name", name);
        AddIfPresent(claims, ClaimTypes.Name, identityName);
        var apiClient = new FakeExternalUserApiClient(ViewerResponse);

        await ResolveAsync(PrincipalWithContact([.. claims]), apiClient);

        Assert.Equal(expected, apiClient.ReceivedRequest!.DisplayName);
    }

    [Fact]
    public async Task ResolveAsync_DisplayName_FallsBackToGivenNameAndSurnameClaims()
    {
        var apiClient = new FakeExternalUserApiClient(ViewerResponse);

        await ResolveAsync(PrincipalWithContact(new Claim(ClaimTypes.GivenName, "Jane"), new Claim(ClaimTypes.Surname, "Doe")), apiClient);

        Assert.Equal("Jane Doe", apiClient.ReceivedRequest!.DisplayName);
    }

    [Fact]
    public async Task ResolveAsync_DisplayName_PrefersFirstNameLastNameOverFallbacks()
    {
        var apiClient = new FakeExternalUserApiClient(ViewerResponse);

        await ResolveAsync(
            PrincipalWithContact(new Claim("firstName", "Jane"), new Claim("lastName", "Doe"), new Claim(ClaimTypes.GivenName, "Other"), new Claim(ClaimTypes.Name, "fallback-name")),
            apiClient);

        Assert.Equal("Jane Doe", apiClient.ReceivedRequest!.DisplayName);
    }

    [Fact]
    public async Task ResolveAsync_DisplayNameLongerThanTheApiAccepts_IsTruncated()
    {
        var apiClient = new FakeExternalUserApiClient(ViewerResponse);

        await ResolveAsync(PrincipalWithContact(new Claim("name", new string('x', ResolveExternalUserRequest.MaxDisplayNameLength + 50))), apiClient);

        Assert.Equal(ResolveExternalUserRequest.MaxDisplayNameLength, apiClient.ReceivedRequest!.DisplayName.Length);
    }

    // ---- denials ----

    [Theory]
    [InlineData(ExternalUserResolutionStatus.NotFound, "/Account/NotPermitted")]
    [InlineData(ExternalUserResolutionStatus.Ambiguous, "/Account/NotPermitted")]
    [InlineData(ExternalUserResolutionStatus.LinkedToAnotherIdentity, "/Account/NotPermitted")]
    [InlineData(ExternalUserResolutionStatus.Inactive, "/Account/NotPermitted?reason=inactive")]
    [InlineData(ExternalUserResolutionStatus.NoActiveLab, "/Account/NotPermitted?reason=no-active-lab")]
    public async Task ResolveAsync_DeniedStatus_RedirectsToTheMatchingNotPermittedPage(ExternalUserResolutionStatus status, string expectedPath)
    {
        var apiClient = new FakeExternalUserApiClient(ResolveExternalUserResponse.Denied(status, "Jane Doe"));

        var result = await ResolveAsync(PrincipalWithContact(), apiClient);

        Assert.False(result.IsAllowed);
        Assert.Equal(expectedPath, result.DenialRedirectPath);
        Assert.Null(result.Claims);
    }

    [Theory]
    [InlineData(typeof(HttpRequestException))]
    [InlineData(typeof(TaskCanceledException))]
    [InlineData(typeof(NotSupportedException))]
    [InlineData(typeof(Polly.Timeout.TimeoutRejectedException))]
    public async Task ResolveAsync_ApiCallFailsInAnExpectedWay_DeniesWithErrorRedirect(Type exceptionType)
    {
        var apiClient = new FakeExternalUserApiClient(ViewerResponse, (Exception)Activator.CreateInstance(exceptionType)!);

        var result = await ResolveAsync(PrincipalWithContact(), apiClient);

        Assert.False(result.IsAllowed);
        Assert.Equal("/Home/Error", result.DenialRedirectPath);
    }

    [Fact]
    public async Task ResolveAsync_ApiCallFailsUnexpectedly_DoesNotHideTheBug()
    {
        var apiClient = new FakeExternalUserApiClient(ViewerResponse, new InvalidOperationException("bug"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => ResolveAsync(PrincipalWithContact(), apiClient));
    }

    // ---- the claims added for each user type ----

    [Fact]
    public async Task ResolveAsync_Viewer_AddsViewerClaims()
    {
        var apiClient = new FakeExternalUserApiClient(ViewerResponse);

        var result = await ResolveAsync(PrincipalWithContact(), apiClient);

        Assert.True(result.IsAllowed);
        var claims = result.Claims!;
        Assert.Equal("Jane Doe", claims[ExternalUserClaimTypes.DisplayName]);
        Assert.Equal("Viewer", claims[ExternalUserClaimTypes.ResolvedRoles]);
        Assert.Equal("Viewer", claims[ExternalUserClaimTypes.UserType]);
        Assert.Equal("0", claims[ExternalUserClaimTypes.LabCount]);
        Assert.Equal(UserId.ToString(), claims[ExternalUserClaimTypes.ViewerId]);
        Assert.Equal(5, claims.Count);
        Assert.DoesNotContain(ExternalUserClaimTypes.ParticipantId, claims.Keys);
        Assert.DoesNotContain(ExternalUserClaimTypes.LabCode, claims.Keys);
        Assert.DoesNotContain(ExternalUserClaimTypes.CanOrderOnline, claims.Keys);
    }

    [Fact]
    public async Task ResolveAsync_ParticipantWithOneLab_AddsTheLabClaims()
    {
        var response = Resolved(
            ExternalUserType.Participant,
            [ExternalRoleNames.Participant, ExternalRoleNames.Viewer],
            labs: [new ExternalLab(LabId, "LAB001", "North Lab")],
            participantId: LabId,
            labCode: "LAB001",
            canOrderOnline: true);

        var result = await ResolveAsync(PrincipalWithContact(), new FakeExternalUserApiClient(response));

        var claims = result.Claims!;
        Assert.Equal("Participant,Viewer", claims[ExternalUserClaimTypes.ResolvedRoles]);
        Assert.Equal("Participant", claims[ExternalUserClaimTypes.UserType]);
        Assert.Equal("1", claims[ExternalUserClaimTypes.LabCount]);
        Assert.Equal(LabId.ToString(), claims[ExternalUserClaimTypes.ParticipantId]);
        Assert.Equal("LAB001", claims[ExternalUserClaimTypes.LabCode]);
        Assert.Equal("true", claims[ExternalUserClaimTypes.CanOrderOnline]);
        Assert.DoesNotContain(ExternalUserClaimTypes.ViewerId, claims.Keys);
    }

    [Fact]
    public async Task ResolveAsync_ParticipantWithSeveralLabs_AddsTheCountButNoLab()
    {
        var response = Resolved(
            ExternalUserType.Participant,
            [ExternalRoleNames.Participant, ExternalRoleNames.Viewer],
            labs: [new ExternalLab(LabId, "LAB001", "A"), new ExternalLab(Guid.NewGuid(), "LAB002", "B")]);

        var result = await ResolveAsync(PrincipalWithContact(), new FakeExternalUserApiClient(response));

        var claims = result.Claims!;
        Assert.Equal("2", claims[ExternalUserClaimTypes.LabCount]);
        Assert.DoesNotContain(ExternalUserClaimTypes.ParticipantId, claims.Keys);
        Assert.DoesNotContain(ExternalUserClaimTypes.LabCode, claims.Keys);
    }

    [Fact]
    public async Task ResolveAsync_LabUser_IsIdentifiedAsLabUserWithItsOwnId()
    {
        var response = Resolved(
            ExternalUserType.LabUser,
            [ExternalRoleNames.LabUser, ExternalRoleNames.Participant, ExternalRoleNames.Viewer],
            labs: [new ExternalLab(LabId, "LAB001", "North Lab")],
            participantId: LabId,
            labCode: "LAB001",
            labUserId: UserId);

        var result = await ResolveAsync(PrincipalWithContact(), new FakeExternalUserApiClient(response));

        var claims = result.Claims!;
        Assert.Equal("LabUser", claims[ExternalUserClaimTypes.UserType]);
        Assert.Equal("Lab User,Participant,Viewer", claims[ExternalUserClaimTypes.ResolvedRoles]);
        Assert.Equal(UserId.ToString(), claims[ExternalUserClaimTypes.LabUserId]);
        Assert.Equal(LabId.ToString(), claims[ExternalUserClaimTypes.ParticipantId]);
    }

    [Fact]
    public async Task ResolveAsync_TestConsultant_AddsItsOwnId()
    {
        var response = Resolved(ExternalUserType.TestConsultant, [ExternalRoleNames.TestConsultant, ExternalRoleNames.Viewer], testConsultantId: UserId);

        var result = await ResolveAsync(PrincipalWithContact(), new FakeExternalUserApiClient(response));

        var claims = result.Claims!;
        Assert.Equal("TestConsultant", claims[ExternalUserClaimTypes.UserType]);
        Assert.Equal(UserId.ToString(), claims[ExternalUserClaimTypes.TestConsultantId]);
        Assert.DoesNotContain(ExternalUserClaimTypes.ViewerId, claims.Keys);
    }

    [Fact]
    public async Task ResolveAsync_BlankLabCodeAndNoOnlineOrdering_AddsNeitherClaim()
    {
        var response = Resolved(ExternalUserType.Participant, [ExternalRoleNames.Participant], participantId: LabId, labCode: "  ", canOrderOnline: false);

        var result = await ResolveAsync(PrincipalWithContact(), new FakeExternalUserApiClient(response));

        Assert.DoesNotContain(ExternalUserClaimTypes.LabCode, result.Claims!.Keys);
        Assert.DoesNotContain(ExternalUserClaimTypes.CanOrderOnline, result.Claims.Keys);
    }

    private static void AddIfPresent(List<Claim> claims, string type, string? value)
    {
        if (value is not null)
        {
            claims.Add(new Claim(type, value));
        }
    }
}
