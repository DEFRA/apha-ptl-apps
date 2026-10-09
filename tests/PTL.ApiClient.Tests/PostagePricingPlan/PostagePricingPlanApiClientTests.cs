using System.Net;
using PTL.ApiClient;
using PTL.ApiClient.Tests;
using PTL.Contracts.PostagePricingPlan;

namespace PTL.ApiClient.Tests.PostagePricingPlan;

public class PostagePricingPlanApiClientTests
{
    private static PostagePricingPlanApiClient CreateClient(HttpStatusCode statusCode, string? jsonContent) =>
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
    public async Task GetYearsAsync_NullResponse_ReturnsEmptyYears()
    {
        var client = CreateClient(HttpStatusCode.OK, "null");

        var result = await client.GetYearsAsync();

        Assert.Empty(result.AvailableYears);
        Assert.False(result.CanRenew);
    }

    [Fact]
    public async Task SetPriceAsync_Success_ReturnsSuccessfulResult()
    {
        const string json = """{"success":true,"fieldErrors":{}}""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.SetPriceAsync(new UpdatePostagePricingPlanPriceRequest(Guid.NewGuid(), 5m, 10m, 15m));

        Assert.True(result.Success);
    }

    [Fact]
    public async Task SetPriceAsync_BadRequest_ReturnsFieldErrors()
    {
        const string json = """{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.1","title":"One or more validation errors occurred.","status":400,"errors":{"UKPrice":["A UK Price must not be negative"]}}""";
        var client = CreateClient(HttpStatusCode.BadRequest, json);

        var result = await client.SetPriceAsync(new UpdatePostagePricingPlanPriceRequest(Guid.NewGuid(), -1m, 10m, 15m));

        Assert.False(result.Success);
        Assert.Contains("UKPrice", result.FieldErrors.Keys);
    }

    [Fact]
    public async Task RenewAsync_Success_ReturnsDeserializedResult()
    {
        const string json = """{"success":true,"message":"The postage pricing plan has successfully been renewed for the financial year 2027/28."}""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.RenewAsync();

        Assert.True(result.Success);
        Assert.Contains("2027/28", result.Message);
    }

    [Fact]
    public async Task RenewAsync_Blocked_ReturnsUnsuccessfulResult()
    {
        const string json = """{"success":false,"message":"No postage pricing plan has been entered for the current financial year, so the plan cannot be renewed."}""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.RenewAsync();

        Assert.False(result.Success);
        Assert.NotEmpty(result.Message);
    }

    [Fact]
    public async Task RenewAsync_NullResponse_ReturnsUnsuccessfulResult()
    {
        var client = CreateClient(HttpStatusCode.OK, "null");

        var result = await client.RenewAsync();

        Assert.False(result.Success);
        Assert.NotEmpty(result.Message);
    }
}
