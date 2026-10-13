using System.Net;
using PTL.ApiClient;
using PTL.ApiClient.Tests;
using PTL.Contracts.Country;

namespace PTL.ApiClient.Tests.Country;

public class CountryApiClientTests
{
    private static CountryApiClient CreateClient(HttpStatusCode statusCode, string? jsonContent) =>
        new(new HttpClient(new StubHttpMessageHandler(statusCode, jsonContent)) { BaseAddress = new Uri("http://localhost") });

    [Fact]
    public async Task GetCountriesAsync_Success_ReturnsDeserializedCountries()
    {
        const string json = """[{"countryId":"11111111-1111-1111-1111-111111111111","country":"France","countryTypeId":"22222222-2222-2222-2222-222222222222","countryType":"EU","allocationCount":2}]""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.GetCountriesAsync();

        Assert.Single(result);
        Assert.Equal("France", result[0].Country);
        Assert.Equal(2, result[0].AllocationCount);
    }

    [Fact]
    public async Task GetCountriesAsync_NullResponse_ReturnsEmpty()
    {
        var client = CreateClient(HttpStatusCode.OK, "null");

        var result = await client.GetCountriesAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetCountryTypesAsync_NullResponse_ReturnsEmpty()
    {
        var client = CreateClient(HttpStatusCode.OK, "null");

        var result = await client.GetCountryTypesAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task CreateCountryAsync_Success_ReturnsSuccessfulResult()
    {
        const string json = """{"countryId":"11111111-1111-1111-1111-111111111111","country":"France","countryTypeId":"22222222-2222-2222-2222-222222222222","countryType":"","allocationCount":0}""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.CreateCountryAsync(new CountrySaveRequest("France", Guid.NewGuid()));

        Assert.True(result.Success);
        Assert.Equal("France", result.Country?.Country);
    }

    [Fact]
    public async Task CreateCountryAsync_BadRequest_ReturnsFieldErrors()
    {
        const string json = """{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.1","title":"One or more validation errors occurred.","status":400,"errors":{"Country":["Enter a country name"]}}""";
        var client = CreateClient(HttpStatusCode.BadRequest, json);

        var result = await client.CreateCountryAsync(new CountrySaveRequest(string.Empty, Guid.NewGuid()));

        Assert.False(result.Success);
        Assert.Contains("Country", result.FieldErrors.Keys);
    }

    [Fact]
    public async Task UpdateCountryAsync_NotFound_ReturnsFailure()
    {
        var client = CreateClient(HttpStatusCode.NotFound, null);

        var result = await client.UpdateCountryAsync(Guid.NewGuid(), new CountrySaveRequest("France", Guid.NewGuid()));

        Assert.False(result.Success);
    }

    [Fact]
    public async Task DeleteCountryAsync_Blocked_ReturnsLegacyMessage()
    {
        const string json = """{"success":false,"message":"This country is being used by 8 customer(s)/participant(s)/Group Addresses."}""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.DeleteCountryAsync(Guid.NewGuid());

        Assert.False(result.Success);
        Assert.Equal("This country is being used by 8 customer(s)/participant(s)/Group Addresses.", result.Message);
    }
}
