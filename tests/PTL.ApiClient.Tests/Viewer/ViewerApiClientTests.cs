using System.Net;
using PTL.ApiClient;
using PTL.ApiClient.Tests;
using PTL.Contracts.Viewer;

namespace PTL.ApiClient.Tests.Viewer;

public class ViewerApiClientTests
{
    private static ViewerApiClient CreateClient(HttpStatusCode statusCode, string? jsonContent) =>
        new(new HttpClient(new StubHttpMessageHandler(statusCode, jsonContent)) { BaseAddress = new Uri("http://localhost") });

    [Fact]
    public async Task GetAllAsync_Success_ReturnsDeserializedViewers()
    {
        const string json = """[{"viewerId":"11111111-1111-1111-1111-111111111111","name":"Jane Smith","email":"jane@example.com","hasLogin":true,"schemes":[],"participants":[]}]""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.GetAllAsync();

        var viewer = Assert.Single(result);
        Assert.Equal("Jane Smith", viewer.Name);
        Assert.True(viewer.HasLogin);
    }

    [Fact]
    public async Task GetAllAsync_NullResponse_ReturnsEmpty()
    {
        var client = CreateClient(HttpStatusCode.OK, "null");

        var result = await client.GetAllAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task CreateAsync_Success_ReturnsSuccessfulResult()
    {
        const string json = """{"viewerId":"11111111-1111-1111-1111-111111111111","name":"Jane Smith","email":"jane@example.com","hasLogin":false,"schemes":[],"participants":[]}""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.CreateAsync(new ViewerSaveRequest("Jane Smith", "jane@example.com"));

        Assert.True(result.Success);
        Assert.Equal("Jane Smith", result.Viewer?.Name);
    }

    [Fact]
    public async Task CreateAsync_BadRequest_ReturnsFieldErrors()
    {
        const string json = """{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.1","title":"One or more validation errors occurred.","status":400,"errors":{"Email":["Enter an email address"]}}""";
        var client = CreateClient(HttpStatusCode.BadRequest, json);

        var result = await client.CreateAsync(new ViewerSaveRequest("Jane Smith", string.Empty));

        Assert.False(result.Success);
        Assert.Contains("Email", result.FieldErrors.Keys);
    }

    [Fact]
    public async Task UpdateAsync_NotFound_ReturnsFailure()
    {
        var client = CreateClient(HttpStatusCode.NotFound, null);

        var result = await client.UpdateAsync(Guid.NewGuid(), new ViewerSaveRequest("Jane Smith", "jane@example.com"));

        Assert.False(result.Success);
    }

    [Fact]
    public async Task DeleteAsync_Success_ReturnsSuccess()
    {
        const string json = """{"success":true,"message":null}""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.DeleteAsync(Guid.NewGuid());

        Assert.True(result.Success);
    }

    [Fact]
    public async Task GenerateLoginAsync_Success_ReturnsSuccess()
    {
        const string json = """{"success":true,"message":null}""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.GenerateLoginAsync(Guid.NewGuid());

        Assert.True(result.Success);
    }

    [Fact]
    public async Task GenerateLoginAsync_NullResponse_ReturnsFailure()
    {
        var client = CreateClient(HttpStatusCode.OK, "null");

        var result = await client.GenerateLoginAsync(Guid.NewGuid());

        Assert.False(result.Success);
    }
}
