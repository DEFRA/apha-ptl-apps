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
    public async Task GetGroupAddressesAsync_ReturnsDeserializedList()
    {
        const string json = """[{"groupAddressId":"66666666-6666-6666-6666-666666666666","identifier":"PTL-001","address1":"1 Sample Street","countryId":"77777777-7777-7777-7777-777777777777"}]""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.GetGroupAddressesAsync();

        Assert.Single(result);
        Assert.Equal("PTL-001", result[0].Identifier);
    }

    [Fact]
    public async Task GetSchedulesAsync_ReturnsDeserializedList()
    {
        const string json = """[{"scheduleId":"11111111-1111-1111-1111-111111111111","schedule":"Monthly"}]""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.GetSchedulesAsync();

        Assert.Equal("Monthly", Assert.Single(result).Schedule);
    }

    [Fact]
    public async Task GetScheduleCodesAsync_ReturnsDeserializedList()
    {
        const string json = """[{"scheduleCodeId":"22222222-2222-2222-2222-222222222222","scheduleCode":"M1"}]""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.GetScheduleCodesAsync();

        Assert.Equal("M1", Assert.Single(result).ScheduleCode);
    }

    [Fact]
    public async Task GetDaysAsync_ReturnsDeserializedList()
    {
        const string json = """[{"dayId":"33333333-3333-3333-3333-333333333333","day":"Monday"}]""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.GetDaysAsync();

        Assert.Equal("Monday", Assert.Single(result).Day);
    }

    [Fact]
    public async Task GetTestConsultantsAsync_ReturnsDeserializedList()
    {
        const string json = """[{"userId":"44444444-4444-4444-4444-444444444444","friendlyName":"Internal TC","isExternal":false}]""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.GetTestConsultantsAsync();

        var consultant = Assert.Single(result);
        Assert.Equal("Internal TC", consultant.FriendlyName);
        Assert.False(consultant.IsExternal);
    }

    [Fact]
    public async Task GetAssessorsAsync_ReturnsDeserializedList()
    {
        const string json = """[{"userId":"55555555-5555-5555-5555-555555555555","friendlyName":"Assessor One","isExternal":false}]""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.GetAssessorsAsync();

        Assert.Equal("Assessor One", Assert.Single(result).FriendlyName);
    }

    [Fact]
    public async Task GetViewersAsync_ReturnsDeserializedList()
    {
        const string json = """[{"viewerId":"66666666-6666-6666-6666-666666666666","name":"Viewer One","email":"viewer@example.com"}]""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.GetViewersAsync();

        Assert.Equal("Viewer One", Assert.Single(result).Name);
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
    public async Task GetAllYearsAsync_ReturnsDeserializedList()
    {
        const string json = """[{"yearId":2020,"year":"2020/21"}]""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.GetAllYearsAsync();

        Assert.Single(result);
        Assert.Equal("2020/21", result[0].Year);
    }

    [Fact]
    public async Task GetWeightedPricingYearsAsync_ReturnsDeserializedList()
    {
        const string json = """[{"yearId":2026,"year":"2026/27"}]""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.GetWeightedPricingYearsAsync();

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

    [Fact]
    public async Task GetSystemSettingsAsync_ReturnsDeserializedResponse()
    {
        const string json = """{"utNumber":"UT3/306"}""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.GetSystemSettingsAsync();

        Assert.Equal("UT3/306", result.UTNumber);
    }
}
