using System.Net;
using PTL.ApiClient;
using PTL.ApiClient.Tests;
using PTL.Contracts.ExternalSiteMessage;

namespace PTL.ApiClient.Tests.ExternalSiteMessage;

public class ExternalSiteMessageApiClientTests
{
    private static ExternalSiteMessageApiClient CreateClient(HttpStatusCode statusCode, string? jsonContent) =>
        new(new HttpClient(new StubHttpMessageHandler(statusCode, jsonContent)) { BaseAddress = new Uri("http://localhost") });

    [Fact]
    public async Task GetExternalSiteMessageAsync_Success_ReturnsDeserializedMessage()
    {
        const string json = """{"message":"<p>Body</p>","importantMessage":"<p>Notice</p>","supportEmailAddress":"vetqas@apha.gov.uk"}""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.GetExternalSiteMessageAsync();

        Assert.Equal("<p>Body</p>", result.Message);
        Assert.Equal("vetqas@apha.gov.uk", result.SupportEmailAddress);
    }

    [Fact]
    public async Task GetExternalSiteMessageAsync_NullResponse_ReturnsEmptyMessage()
    {
        var client = CreateClient(HttpStatusCode.OK, "null");

        var result = await client.GetExternalSiteMessageAsync();

        Assert.Equal(string.Empty, result.Message);
    }

    [Fact]
    public async Task UpdateExternalSiteMessageAsync_Success_ReturnsSuccessfulResult()
    {
        const string json = """{"message":"Body","importantMessage":"Notice","supportEmailAddress":"vetqas@apha.gov.uk"}""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.UpdateExternalSiteMessageAsync(new ExternalSiteMessageSaveRequest("Body", "Notice", "vetqas@apha.gov.uk"));

        Assert.True(result.Success);
        Assert.Equal("Body", result.ExternalSiteMessage?.Message);
    }

    [Fact]
    public async Task UpdateExternalSiteMessageAsync_BadRequest_ReturnsFieldErrors()
    {
        const string json = """{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.1","title":"One or more validation errors occurred.","status":400,"errors":{"ImportantMessage":["The Important Message cannot exceed 500 characters."]}}""";
        var client = CreateClient(HttpStatusCode.BadRequest, json);

        var result = await client.UpdateExternalSiteMessageAsync(new ExternalSiteMessageSaveRequest("Body", new string('a', 501), "vetqas@apha.gov.uk"));

        Assert.False(result.Success);
        Assert.Contains("ImportantMessage", result.FieldErrors.Keys);
    }
}
