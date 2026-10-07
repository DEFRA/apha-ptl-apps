using System.Net;
using PTL.ApiClient;
using PTL.ApiClient.Tests;
using PTL.Contracts.AdministrationCharge;

namespace PTL.ApiClient.Tests.AdministrationCharge;

public class AdministrationChargeApiClientTests
{
    private static AdministrationChargeApiClient CreateClient(HttpStatusCode statusCode, string? jsonContent) =>
        new(new HttpClient(new StubHttpMessageHandler(statusCode, jsonContent)) { BaseAddress = new Uri("http://localhost") });

    [Fact]
    public async Task GetAdministrationChargesAsync_Success_ReturnsDeserializedCharges()
    {
        const string json = """
            [{"administrationChargeId":"11111111-1111-1111-1111-111111111111","name":"Administration Charge","prices":[{"currencyId":"22222222-2222-2222-2222-222222222222","price":25.00}]}]
            """;
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.GetAdministrationChargesAsync();

        Assert.Single(result);
        Assert.Equal("Administration Charge", result[0].Name);
        Assert.Equal(25.00m, result[0].Prices[0].Price);
    }

    [Fact]
    public async Task SetPriceAsync_Success_ReturnsSavedPrice()
    {
        const string json = """{"currencyId":"22222222-2222-2222-2222-222222222222","price":30.00}""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.SetPriceAsync(new UpdateAdministrationChargePriceRequest(Guid.NewGuid(), Guid.NewGuid(), 30.00m));

        Assert.True(result.Success);
        Assert.NotNull(result.Price);
        Assert.Empty(result.FieldErrors);
    }

    [Fact]
    public async Task SetPriceAsync_ValidationFailure_ReturnsFieldErrors()
    {
        const string json = """{"errors":{"Price":["Price must not be negative"]}}""";
        var client = CreateClient(HttpStatusCode.BadRequest, json);

        var result = await client.SetPriceAsync(new UpdateAdministrationChargePriceRequest(Guid.NewGuid(), Guid.NewGuid(), -1.00m));

        Assert.False(result.Success);
        Assert.True(result.FieldErrors.ContainsKey("Price"));
    }

    [Fact]
    public async Task SetPriceAsync_BadRequestWithNoErrors_ReturnsGenericFieldError()
    {
        var client = CreateClient(HttpStatusCode.BadRequest, "{}");

        var result = await client.SetPriceAsync(new UpdateAdministrationChargePriceRequest(Guid.NewGuid(), Guid.NewGuid(), -1.00m));

        Assert.False(result.Success);
        Assert.True(result.FieldErrors.ContainsKey(string.Empty));
    }

    [Fact]
    public async Task GetAdministrationChargesAsync_NullResponse_ReturnsEmptyList() =>
        Assert.Empty(await CreateClient(HttpStatusCode.OK, "null").GetAdministrationChargesAsync());

    [Fact]
    public async Task SetPriceAsync_BadRequestWithNullBody_ReturnsGenericFieldError()
    {
        var client = CreateClient(HttpStatusCode.BadRequest, "null");

        var result = await client.SetPriceAsync(new UpdateAdministrationChargePriceRequest(Guid.NewGuid(), Guid.NewGuid(), -1.00m));

        Assert.False(result.Success);
        Assert.True(result.FieldErrors.ContainsKey(string.Empty));
    }
}
