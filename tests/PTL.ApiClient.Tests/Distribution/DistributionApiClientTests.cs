using System.Net;
using PTL.ApiClient.Distribution;
using PTL.ApiClient.Tests;

namespace PTL.ApiClient.Tests.Distribution;

public class DistributionApiClientTests
{
    private static DistributionApiClient CreateClient(HttpStatusCode statusCode, string? jsonContent) =>
        new(new HttpClient(new StubHttpMessageHandler(statusCode, jsonContent)) { BaseAddress = new Uri("http://localhost") });

    [Fact]
    public async Task GetDashboardAsync_ReturnsDeserializedList()
    {
        const string json = """
            [{"yearId":2026,"monthId":4,"monthDescription":"Apr","monthlyDistributionId":"11111111-1111-1111-1111-111111111111","isInitialisable":false,"schedulingCompletePercentage":50,"preparationCompletePercentage":0,"packagingCompletePercentage":0,"resultsCompletePercentage":0,"tabulationsCompletePercentage":0}]
            """;
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.GetDashboardAsync(2026);

        Assert.Single(result);
        Assert.Equal("Apr", result[0].MonthDescription);
        Assert.Equal(50m, result[0].SchedulingCompletePercentage);
    }

    [Fact]
    public async Task GetDashboardAsync_NullResponse_ReturnsEmptyList()
    {
        var client = CreateClient(HttpStatusCode.OK, "null");

        var result = await client.GetDashboardAsync(2026);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetYearsAsync_ReturnsDeserializedList()
    {
        const string json = """[{"yearId":2026,"label":"2026/27"}]""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.GetYearsAsync();

        Assert.Single(result);
        Assert.Equal("2026/27", result[0].Label);
    }

    [Fact]
    public async Task GetYearsAsync_NullResponse_ReturnsEmptyList()
    {
        var client = CreateClient(HttpStatusCode.OK, "null");

        var result = await client.GetYearsAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetScheduleAsync_ReturnsDeserializedResponse()
    {
        const string json = """
            {"monthlyDistributionId":"11111111-1111-1111-1111-111111111111","yearId":2026,"monthId":4,"schemes":[]}
            """;
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.GetScheduleAsync(2026, 4);

        Assert.NotNull(result);
        Assert.Equal(2026, result!.YearId);
        Assert.Empty(result.Schemes);
    }

    [Fact]
    public async Task SaveScheduleAsync_ReturnsDeserializedResult()
    {
        const string json = """{"success":true,"fieldErrorsBySchemeId":{}}""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.SaveScheduleAsync(2026, 4, []);

        Assert.NotNull(result);
        Assert.True(result!.Success);
    }
}
