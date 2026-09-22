using System.Net;
using PTL.ApiClient;
using PTL.ApiClient.Tests;
using PTL.Contracts.Scheme;

namespace PTL.ApiClient.Tests.Scheme;

public class SchemeApiClientTests
{
    private static SchemeApiClient CreateClient(HttpStatusCode statusCode, string? jsonContent) =>
        new(new HttpClient(new StubHttpMessageHandler(statusCode, jsonContent)) { BaseAddress = new Uri("http://localhost") });

    [Fact]
    public async Task GetSchemeAsync_NotFound_ReturnsNull()
    {
        var client = CreateClient(HttpStatusCode.NotFound, null);

        var result = await client.GetSchemeAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetSchemeAsync_Success_ReturnsDeserializedScheme()
    {
        var client = CreateClient(HttpStatusCode.OK, FullSchemeJson());

        var result = await client.GetSchemeAsync(Guid.NewGuid());

        Assert.NotNull(result);
        Assert.Equal("PT1234", result!.Identifier);
        Assert.False(result.IsReadOnly);
    }

    [Fact]
    public async Task GetSchemesForYearAsync_ReturnsDeserializedSearchResponse()
    {
        const string json = "{\"items\":[],\"totalCount\":0,\"page\":1,\"pageSize\":20}";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.GetSchemesForYearAsync(new SchemeSearchRequest(2027));

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task GetSchemeHistoryAsync_ReturnsDeserializedList()
    {
        const string json = "[]";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.GetSchemeHistoryAsync(Guid.NewGuid());

        Assert.Empty(result);
    }

    [Fact]
    public async Task CreateSchemeAsync_Success_ReturnsSavedScheme()
    {
        var client = CreateClient(HttpStatusCode.OK, FullSchemeJson());

        var result = await client.CreateSchemeAsync(MinimalCreateRequest());

        Assert.True(result.Success);
        Assert.NotNull(result.Scheme);
        Assert.Empty(result.FieldErrors);
    }

    [Fact]
    public async Task CreateSchemeAsync_BadRequestWithNoErrors_ReturnsGenericFieldError()
    {
        var client = CreateClient(HttpStatusCode.BadRequest, "{}");

        var result = await client.CreateSchemeAsync(MinimalCreateRequest());

        Assert.False(result.Success);
        Assert.True(result.FieldErrors.ContainsKey(string.Empty));
    }

    [Fact]
    public async Task CreateSchemeAsync_ValidationFailure_ReturnsFieldErrors()
    {
        const string json = "{\"errors\":{\"Identifier\":[\"Identifier must match the format PT followed by 4 digits (e.g. PT1234).\"]}}";
        var client = CreateClient(HttpStatusCode.BadRequest, json);

        var result = await client.CreateSchemeAsync(MinimalCreateRequest());

        Assert.False(result.Success);
        Assert.True(result.FieldErrors.ContainsKey("Identifier"));
    }

    [Fact]
    public async Task UpdateSchemeAsync_NotFound_ReturnsFailureResult()
    {
        var client = CreateClient(HttpStatusCode.NotFound, null);

        var result = await client.UpdateSchemeAsync(Guid.NewGuid(), MinimalUpdateRequest());

        Assert.False(result.Success);
        Assert.Null(result.Scheme);
    }

    [Fact]
    public async Task UpdateSchemeAsync_Success_ReturnsSavedScheme()
    {
        var client = CreateClient(HttpStatusCode.OK, FullSchemeJson());

        var result = await client.UpdateSchemeAsync(Guid.NewGuid(), MinimalUpdateRequest());

        Assert.True(result.Success);
        Assert.NotNull(result.Scheme);
    }

    private static string FullSchemeJson() => """
        {
            "schemeId":"11111111-1111-1111-1111-111111111111",
            "sharedId":"22222222-2222-2222-2222-222222222222",
            "yearId":2026,
            "identifier":"PT1234",
            "name":"Test Scheme",
            "scheduleId":"33333333-3333-3333-3333-333333333333",
            "scheduleCodeId":"44444444-4444-4444-4444-444444444444",
            "startDate":null,
            "distributionMonthApr":true,
            "distributionMonthMay":false,
            "distributionMonthJun":false,
            "distributionMonthJul":false,
            "distributionMonthAug":false,
            "distributionMonthSep":false,
            "distributionAsAvailable":false,
            "distributionMonthOct":false,
            "distributionMonthNov":false,
            "distributionMonthDec":false,
            "distributionMonthJan":false,
            "distributionMonthFeb":false,
            "distributionMonthMar":false,
            "weekNumber":5,
            "dayOfWeekId":"55555555-5555-5555-5555-555555555555",
            "numberOfSamples":10,
            "sampleNoSequence":1,
            "sampleOrigin":"UK",
            "deadline":10,
            "subcontractor":"",
            "combinedPackaging":false,
            "postage":null,
            "customsVolume":null,
            "samplePackingInstructions":"",
            "requiresAssessment":false,
            "commentsRequired":false,
            "pilot":false,
            "limitedSampleAvailability":false,
            "accredited":false,
            "noVLALabs":false,
            "comerciallyAvailable":false,
            "customsDescription":null,
            "dataConsentDeclarationActive":false,
            "dataConsentDeclarationText":null,
            "instructions":"Instructions",
            "dateOfReceipt":false,
            "storageConditions":false,
            "conditionOnReceipt":false,
            "testConsultant1":null,
            "testConsultant2":null,
            "testConsultant3":null,
            "testConsultantTabulationId":null,
            "useExternalReference":false,
            "storeRatings":false,
            "assessor1":null,
            "assessor2":null,
            "assessor3":null,
            "assessor4":null,
            "standardTabulationText":null,
            "lastModified":"2026-01-01T00:00:00",
            "isReadOnly":false
        }
        """;

    private static SchemeRequest MinimalCreateRequest() => new(
        2027, "PT1234", "Test Scheme", Guid.NewGuid(), Guid.NewGuid(), null,
        true, false, false, false, false, false, false, false, false, false, false, false, false,
        0, Guid.NewGuid(), 5, "UK", 10, string.Empty, false, null, null, string.Empty,
        false, false, false, false, false, false, false,
        "Biological samples", false, null, "Instructions", false, false, false,
        null, null, null, null, false, false, null, null, null, null, null);

    private static SchemeRequest MinimalUpdateRequest() => MinimalCreateRequest();
}
