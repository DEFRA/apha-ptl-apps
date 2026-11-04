using System.Net;
using PTL.ApiClient.Distribution;
using PTL.ApiClient.Tests;

namespace PTL.ApiClient.Tests.Distribution;

public class DistributionApiClientParticipantHistoryTests
{
    private static DistributionApiClient CreateClient(HttpStatusCode statusCode, string? jsonContent) =>
        new(new HttpClient(new StubHttpMessageHandler(statusCode, jsonContent)) { BaseAddress = new Uri("http://localhost") });

    [Fact]
    public async Task GetParticipantDistributionHistoryAsync_ReturnsDeserializedList()
    {
        const string json = """
            {"distributions":[{"monthlyDistributionSchemeId":"11111111-1111-1111-1111-111111111111","distributionReference":"D1a/00001","schemeCode":"PT1234","schemeName":"Test Scheme","distributionDate":"2026-01-15T00:00:00","yearId":2025,"monthId":1}]}
            """;
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.GetParticipantDistributionHistoryAsync(Guid.NewGuid());

        var distribution = Assert.Single(result.Distributions);
        Assert.Equal("D1a/00001", distribution.DistributionReference);
        Assert.Equal("PT1234", distribution.SchemeCode);
    }

    [Fact]
    public async Task GetParticipantDistributionHistoryAsync_NullResponse_ReturnsEmptyList()
    {
        var client = CreateClient(HttpStatusCode.OK, "null");

        var result = await client.GetParticipantDistributionHistoryAsync(Guid.NewGuid());

        Assert.Empty(result.Distributions);
    }

    [Fact]
    public async Task GetActualResultsForParticipantAsync_NotFound_ReturnsNull()
    {
        var client = CreateClient(HttpStatusCode.NotFound, null);

        var result = await client.GetActualResultsForParticipantAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Null(result);
    }
}
