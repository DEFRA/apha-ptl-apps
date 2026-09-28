using System.Net;
using PTL.ApiClient;
using PTL.ApiClient.Tests;
using PTL.Contracts.WeightedPricingPlan;

namespace PTL.ApiClient.Tests.WeightedPricingPlan;

public class WeightedPricingPlanApiClientTests
{
    private static WeightedPricingPlanApiClient CreateClient(HttpStatusCode statusCode, string? jsonContent) =>
        new(new HttpClient(new StubHttpMessageHandler(statusCode, jsonContent)) { BaseAddress = new Uri("http://localhost") });

    [Fact]
    public async Task GetYearsAsync_Success_ReturnsDeserializedYears()
    {
        const string json = """{"availableYears":[{"yearId":2026,"year":"2026/27"}],"canRenew":true,"nextYearId":2027,"nextYearLabel":"2027/28"}""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.GetYearsAsync();

        Assert.Single(result.AvailableYears);
        Assert.True(result.CanRenew);
        Assert.Equal(2027, result.NextYearId);
    }

    [Fact]
    public async Task GetPercentagesForYearAsync_Success_ReturnsDeserializedPercentages()
    {
        const string json = """[{"numberOfDistributionsOnScheme":12,"numberOfDistributionsChosen":6,"weight":50}]""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.GetPercentagesForYearAsync(2026);

        Assert.Single(result);
        Assert.Equal(50, result[0].Weight);
    }

    [Fact]
    public async Task RenewAsync_Success_ReturnsDeserializedResult()
    {
        const string json = """{"success":true,"message":"The weighted pricing plan has successfully been renewed for the financial year 2027/28."}""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.RenewAsync();

        Assert.True(result.Success);
        Assert.Contains("2027/28", result.Message);
    }

    [Fact]
    public async Task RenewAsync_Blocked_ReturnsUnsuccessfulResult()
    {
        const string json = """{"success":false,"message":"No weighted pricing percentages have been entered for the current financial year, so the plan cannot be renewed."}""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.RenewAsync();

        Assert.False(result.Success);
        Assert.NotEmpty(result.Message);
    }
}
