using System.Net;
using PTL.ApiClient.Tests;
using PTL.Contracts.ExternalUser;

namespace PTL.ApiClient.Tests.ExternalUser;

public class ExternalUserApiClientTests
{
    private static ExternalUserApiClient CreateClient(HttpStatusCode statusCode, string? jsonContent) =>
        new(new HttpClient(new StubHttpMessageHandler(statusCode, jsonContent)) { BaseAddress = new Uri("http://localhost") });

    private static ResolveExternalUserRequest CreateRequest() =>
        new(Guid.NewGuid(), "jane@example.com", "Jane Doe");

    [Fact]
    public async Task ResolveAsync_ReturnsDeserializedResponse()
    {
        const string json = """
            {
              "status": "Resolved",
              "displayName": "Jane Doe",
              "roles": ["Lab User", "Participant", "Viewer"],
              "labs": [{ "participantId": "11111111-1111-1111-1111-111111111111", "labCode": "LAB001", "labName": "North Lab" }],
              "userType": "LabUser",
              "participantId": "11111111-1111-1111-1111-111111111111",
              "labCode": "LAB001",
              "viewerId": null,
              "testConsultantId": null,
              "labUserId": "22222222-2222-2222-2222-222222222222",
              "canOrderOnline": true
            }
            """;
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.ResolveAsync(CreateRequest());

        Assert.Equal(ExternalUserResolutionStatus.Resolved, result.Status);
        Assert.Equal("Jane Doe", result.DisplayName);
        Assert.Equal(["Lab User", "Participant", "Viewer"], result.Roles);
        Assert.Equal(ExternalUserType.LabUser, result.UserType);
        Assert.Equal(new ExternalLab(Guid.Parse("11111111-1111-1111-1111-111111111111"), "LAB001", "North Lab"), Assert.Single(result.Labs));
        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), result.ParticipantId);
        Assert.Equal(Guid.Parse("22222222-2222-2222-2222-222222222222"), result.LabUserId);
        Assert.Null(result.ViewerId);
        Assert.True(result.CanOrderOnline);
    }

    [Fact]
    public async Task ResolveAsync_DeniedStatusFromTheApi_IsReturnedAsTheStatus()
    {
        const string json = """{"status":"NoActiveLab","displayName":"Jane Doe","roles":[],"labs":[]}""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.ResolveAsync(CreateRequest());

        Assert.Equal(ExternalUserResolutionStatus.NoActiveLab, result.Status);
        Assert.Empty(result.Roles);
    }

    [Fact]
    public async Task ResolveAsync_NullResponse_IsDeniedAsNotFoundWithTheRequestDisplayName()
    {
        var client = CreateClient(HttpStatusCode.OK, "null");

        var result = await client.ResolveAsync(CreateRequest());

        Assert.Equal(ExternalUserResolutionStatus.NotFound, result.Status);
        Assert.Equal("Jane Doe", result.DisplayName);
        Assert.Empty(result.Roles);
        Assert.Empty(result.Labs);
        Assert.Null(result.ParticipantId);
    }

    [Fact]
    public async Task ResolveAsync_ErrorStatusCode_Throws()
    {
        var client = CreateClient(HttpStatusCode.InternalServerError, null);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.ResolveAsync(CreateRequest()));
    }

    [Fact]
    public async Task GetLabsAsync_ReturnsTheLabsForTheContact()
    {
        var ssoIdExt = Guid.NewGuid();
        var requests = new List<string>();
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, """[{"participantId":"11111111-1111-1111-1111-111111111111","labCode":"0142","labName":"Bristol"}]""")
        {
            OnRequest = r => requests.Add(r.RequestUri!.AbsolutePath)
        };
        var client = new ExternalUserApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });

        var labs = await client.GetLabsAsync(ssoIdExt);

        Assert.Equal($"/api/external-users/{ssoIdExt}/labs", Assert.Single(requests));
        Assert.Equal(new ExternalLab(Guid.Parse("11111111-1111-1111-1111-111111111111"), "0142", "Bristol"), Assert.Single(labs));
    }

    [Fact]
    public async Task GetLabsAsync_NullBody_IsEmpty_AndAnErrorThrows()
    {
        Assert.Empty(await CreateClient(HttpStatusCode.OK, "null").GetLabsAsync(Guid.NewGuid()));
        await Assert.ThrowsAsync<HttpRequestException>(() => CreateClient(HttpStatusCode.InternalServerError, null).GetLabsAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetLabAsync_ReturnsTheChosenLab_OrNullWhenNotTheUsersOwn_OrThrowsOnAFault()
    {
        var ssoIdExt = Guid.NewGuid();
        var labId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var requests = new List<string>();
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, """{"participantId":"11111111-1111-1111-1111-111111111111","labCode":"0142","labName":"Bristol","canOrderOnline":true}""")
        {
            OnRequest = r => requests.Add(r.RequestUri!.AbsolutePath)
        };
        var client = new ExternalUserApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });

        var lab = await client.GetLabAsync(ssoIdExt, labId);

        Assert.Equal($"/api/external-users/{ssoIdExt}/labs/{labId}", Assert.Single(requests));
        Assert.Equal(new ExternalLabContext(labId, "0142", "Bristol", true), lab);
        Assert.Null(await CreateClient(HttpStatusCode.NotFound, null).GetLabAsync(ssoIdExt, labId));
        await Assert.ThrowsAsync<HttpRequestException>(() => CreateClient(HttpStatusCode.InternalServerError, null).GetLabAsync(ssoIdExt, labId));
    }
}
