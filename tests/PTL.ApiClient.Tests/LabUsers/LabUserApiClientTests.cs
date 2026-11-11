using System.Net;
using PTL.Contracts.LabUsers;

namespace PTL.ApiClient.Tests.LabUsers;

public class LabUserApiClientTests
{
    private static readonly Guid LabId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static (LabUserApiClient Client, List<(HttpMethod Method, string Path, string? Body)> Requests) CreateClient(HttpStatusCode statusCode, string? json)
    {
        var requests = new List<(HttpMethod, string, string?)>();
        var handler = new StubHttpMessageHandler(statusCode, json)
        {
            OnRequest = r => requests.Add((r.Method, r.RequestUri!.AbsolutePath, r.Content?.ReadAsStringAsync().GetAwaiter().GetResult()))
        };
        return (new LabUserApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") }), requests);
    }

    private const string UserJson = """{"labUserId":"22222222-2222-2222-2222-222222222222","name":"Emma Wright","email":"emma@example.org","isActive":true,"loginInstructionsSent":false,"canChangeDetails":true,"hasSignedIn":false}""";

    [Fact]
    public async Task GetListAsync_GetsTheLabsUsers()
    {
        var (client, requests) = CreateClient(HttpStatusCode.OK, $$"""{"labCode":"0142","labName":"Bristol","users":[{{UserJson}}]}""");

        var list = await client.GetListAsync(LabId);

        Assert.Equal((HttpMethod.Get, $"/api/participants/{LabId}/lab-users"), (requests.Single().Method, requests.Single().Path));
        Assert.Equal("0142", list!.LabCode);
        Assert.Equal("Emma Wright", Assert.Single(list.Users).Name);
    }

    [Fact]
    public async Task GetAsync_GetsOnePerson()
    {
        var (client, requests) = CreateClient(HttpStatusCode.OK, $$"""{"labCode":"0142","labName":"Bristol","user":{{UserJson}}}""");

        var details = await client.GetAsync(LabId, UserId);

        Assert.Equal($"/api/participants/{LabId}/lab-users/{UserId}", requests.Single().Path);
        Assert.Equal(UserId, details!.User.LabUserId);
    }

    [Fact]
    public async Task AddAsync_PostsTheFormAndReturnsTheResult()
    {
        var (client, requests) = CreateClient(HttpStatusCode.OK, """{"outcome":"AddedExistingAccount","fieldErrors":{}}""");

        var result = await client.AddAsync(LabId, new LabUserSaveRequest("Ramesh", "ramesh@lab.com", true));

        var request = requests.Single();
        Assert.Equal((HttpMethod.Post, $"/api/participants/{LabId}/lab-users"), (request.Method, request.Path));
        Assert.Contains("\"email\":\"ramesh@lab.com\"", request.Body);
        Assert.Equal(LabUserSaveOutcome.AddedExistingAccount, result!.Outcome);
        Assert.True(result.Success);
    }

    [Fact]
    public async Task AddAsync_FieldErrors_AreReturnedNotThrown()
    {
        var (client, _) = CreateClient(HttpStatusCode.OK, """{"outcome":"Failed","fieldErrors":{"Email":["Enter an email address"]}}""");

        var result = await client.AddAsync(LabId, new LabUserSaveRequest("Ramesh", "", true));

        Assert.False(result!.Success);
        Assert.Equal(["Enter an email address"], result.FieldErrors["Email"]);
    }

    [Fact]
    public async Task UpdateAsync_PutsTheFormForThePerson()
    {
        var (client, requests) = CreateClient(HttpStatusCode.OK, """{"outcome":"UpdatedEmailChanged","fieldErrors":{}}""");

        var result = await client.UpdateAsync(LabId, UserId, new LabUserSaveRequest("Emma", "new@example.org", false));

        Assert.Equal((HttpMethod.Put, $"/api/participants/{LabId}/lab-users/{UserId}"), (requests.Single().Method, requests.Single().Path));
        Assert.Equal(LabUserSaveOutcome.UpdatedEmailChanged, result!.Outcome);
    }

    [Fact]
    public async Task SendLoginInstructionsAsync_PostsAndReturnsTheResponse()
    {
        var (client, requests) = CreateClient(HttpStatusCode.OK, """{"success":false,"message":"The login instructions email could not be sent. Please try again."}""");

        var response = await client.SendLoginInstructionsAsync(LabId, UserId);

        Assert.Equal((HttpMethod.Post, $"/api/participants/{LabId}/lab-users/{UserId}/login-instructions"), (requests.Single().Method, requests.Single().Path));
        Assert.False(response!.Success);
        Assert.Contains("could not be sent", response.Message);
    }

    [Fact]
    public async Task EveryCall_NotFound_ReturnsNull()
    {
        var (client, _) = CreateClient(HttpStatusCode.NotFound, null);
        var request = new LabUserSaveRequest("a", "a@b.com", true);

        Assert.Null(await client.GetListAsync(LabId));
        Assert.Null(await client.GetAsync(LabId, UserId));
        Assert.Null(await client.AddAsync(LabId, request));
        Assert.Null(await client.UpdateAsync(LabId, UserId, request));
        Assert.Null(await client.SendLoginInstructionsAsync(LabId, UserId));
    }

    [Fact]
    public async Task AnyOtherFailure_Throws()
    {
        var (client, _) = CreateClient(HttpStatusCode.InternalServerError, null);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetListAsync(LabId));
    }
}
