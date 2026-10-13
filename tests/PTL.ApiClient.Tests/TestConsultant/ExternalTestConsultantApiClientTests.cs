using System.Net;
using PTL.ApiClient;
using PTL.ApiClient.Tests;
using PTL.Contracts.TestConsultant;

namespace PTL.ApiClient.Tests.TestConsultant;

public class ExternalTestConsultantApiClientTests
{
    private static ExternalTestConsultantApiClient CreateClient(HttpStatusCode statusCode, string? jsonContent) =>
        new(new HttpClient(new StubHttpMessageHandler(statusCode, jsonContent)) { BaseAddress = new Uri("http://localhost") });

    [Fact]
    public async Task GetAllAsync_Success_ReturnsDeserializedConsultants()
    {
        const string json = """[{"externalTestConsultantId":"11111111-1111-1111-1111-111111111111","name":"Jane Smith","department":"Science","email":"jane@example.com","isInactive":false,"inactiveDate":null,"hasLogin":true}]""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.GetAllAsync();

        var consultant = Assert.Single(result);
        Assert.Equal("Jane Smith", consultant.Name);
        Assert.True(consultant.HasLogin);
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
        const string json = """{"externalTestConsultantId":"11111111-1111-1111-1111-111111111111","name":"Jane Smith","department":"Science","email":"jane@example.com","isInactive":false,"inactiveDate":null,"hasLogin":false}""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.CreateAsync(new ExternalTestConsultantSaveRequest("Jane Smith", "Science", "jane@example.com"));

        Assert.True(result.Success);
        Assert.Equal("Jane Smith", result.TestConsultant?.Name);
    }

    [Fact]
    public async Task CreateAsync_BadRequest_ReturnsFieldErrors()
    {
        const string json = """{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.1","title":"One or more validation errors occurred.","status":400,"errors":{"Email":["Enter an email address"]}}""";
        var client = CreateClient(HttpStatusCode.BadRequest, json);

        var result = await client.CreateAsync(new ExternalTestConsultantSaveRequest("Jane Smith", "Science", string.Empty));

        Assert.False(result.Success);
        Assert.Contains("Email", result.FieldErrors.Keys);
    }

    [Fact]
    public async Task UpdateAsync_NotFound_ReturnsFailure()
    {
        var client = CreateClient(HttpStatusCode.NotFound, null);

        var result = await client.UpdateAsync(Guid.NewGuid(), new ExternalTestConsultantSaveRequest("Jane Smith", "Science", "jane@example.com"));

        Assert.False(result.Success);
    }

    [Fact]
    public async Task SetStatusAsync_NotFound_ReturnsNull()
    {
        var client = CreateClient(HttpStatusCode.NotFound, null);

        var result = await client.SetStatusAsync(Guid.NewGuid(), true);

        Assert.Null(result);
    }

    [Fact]
    public async Task SetStatusAsync_Success_ReturnsConsultant()
    {
        const string json = """{"externalTestConsultantId":"11111111-1111-1111-1111-111111111111","name":"Jane Smith","department":"Science","email":"jane@example.com","isInactive":true,"inactiveDate":"2026-10-08T00:00:00","hasLogin":false}""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.SetStatusAsync(Guid.NewGuid(), true);

        Assert.NotNull(result);
        Assert.True(result.IsInactive);
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
