using System.Net;
using PTL.ApiClient.Tests;

namespace PTL.ApiClient.Tests.Lookup;

public class LookupApiClientTests
{
    private static LookupApiClient CreateClient(HttpStatusCode statusCode, string? jsonContent) =>
        new(new HttpClient(new StubHttpMessageHandler(statusCode, jsonContent)) { BaseAddress = new Uri("http://localhost") });

    [Fact]
    public async Task GetCountriesAsync_ReturnsDeserializedList()
    {
        const string json = """[{"countryId":"11111111-1111-1111-1111-111111111111","country":"United Kingdom"}]""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.GetCountriesAsync();

        Assert.Single(result);
        Assert.Equal("United Kingdom", result[0].Country);
    }

    [Fact]
    public async Task GetCurrenciesAsync_ReturnsDeserializedList()
    {
        const string json = """[{"currencyId":"22222222-2222-2222-2222-222222222222","name":"British Pound","symbol":"£","longName":"£ - British Pound"}]""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.GetCurrenciesAsync();

        Assert.Single(result);
        Assert.Equal("£ - British Pound", result[0].LongName);
    }

    [Fact]
    public async Task GetCustomerTypesAsync_ReturnsDeserializedList()
    {
        const string json = """[{"customerTypeId":"33333333-3333-3333-3333-333333333333","customerType":"Commercial"}]""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.GetCustomerTypesAsync();

        Assert.Single(result);
        Assert.Equal("Commercial", result[0].CustomerType);
    }

    [Fact]
    public async Task GetVatRatingsAsync_ReturnsDeserializedList()
    {
        const string json = """[{"vatRatingId":"44444444-4444-4444-4444-444444444444","vatRating":"Standard"}]""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.GetVatRatingsAsync();

        Assert.Single(result);
        Assert.Equal("Standard", result[0].VatRating);
    }

    [Fact]
    public async Task GetLabTypesAsync_ReturnsDeserializedList()
    {
        const string json = """[{"labTypeId":"55555555-5555-5555-5555-555555555555","name":"Reference laboratory"}]""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.GetLabTypesAsync();

        Assert.Single(result);
        Assert.Equal("Reference laboratory", result[0].Name);
    }

    [Fact]
    public async Task GetCurrentYearsAsync_ReturnsDeserializedList()
    {
        const string json = """[{"yearId":2026,"year":"2026/27"}]""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.GetCurrentYearsAsync();

        Assert.Single(result);
        Assert.Equal("2026/27", result[0].Year);
    }

    [Fact]
    public async Task GetSchemeCurrenciesAsync_ReturnsDeserializedList()
    {
        const string json = """[{"schemeCurrencyId":"66666666-6666-6666-6666-666666666666","schemeId":"77777777-7777-7777-7777-777777777777","currencyId":"22222222-2222-2222-2222-222222222222","price":12.5,"currencyName":"British Pound","currencySymbol":"£"}]""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.GetSchemeCurrenciesAsync(Guid.Parse("77777777-7777-7777-7777-777777777777"));

        Assert.Single(result);
        Assert.Equal(12.5m, result[0].Price);
        Assert.Equal("£", result[0].CurrencySymbol);
    }

    [Fact]
    public async Task GetPostagePricingPlansForYearAsync_ReturnsDeserializedList()
    {
        const string json = """[{"postageId":"88888888-8888-8888-8888-888888888888","name":"Standard","ukPrice":5.5,"euPrice":8.0,"nonEuPrice":12.0,"yearId":2026}]""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.GetPostagePricingPlansForYearAsync(2026);

        Assert.Single(result);
        Assert.Equal("Standard", result[0].Name);
        Assert.Equal(2026, result[0].YearId);
    }
}
