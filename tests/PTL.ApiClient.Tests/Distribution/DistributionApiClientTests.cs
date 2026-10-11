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

    [Fact]
    public async Task GetSampleNumbersAsync_NotFound_ReturnsNoPageAndNoErrors()
    {
        var client = CreateClient(HttpStatusCode.NotFound, null);

        var result = await client.GetSampleNumbersAsync(Guid.NewGuid());

        Assert.Null(result.Page);
        Assert.Empty(result.Errors);
    }

    // A 400 means the distribution structure could not be built from the Scheme - the business
    // reason has to survive the hop so the screen can show it instead of a 500.
    [Fact]
    public async Task GetSampleNumbersAsync_BadRequest_SurfacesTheValidationMessages()
    {
        const string json = """
            {"title":"One or more validation errors occurred.","status":400,"errors":{"DistributionStructure":["Scheme PT0060 has no Test Consultant Tabulation."]}}
            """;
        var client = CreateClient(HttpStatusCode.BadRequest, json);

        var result = await client.GetSampleNumbersAsync(Guid.NewGuid());

        Assert.Null(result.Page);
        Assert.Equal(["Scheme PT0060 has no Test Consultant Tabulation."], result.Errors);
    }

    [Fact]
    public async Task GetSampleNumbersAsync_BadRequest_SurfacesEveryMessageFromEveryKey()
    {
        const string json = """
            {"status":400,"errors":{"DistributionStructure":["First problem","Second problem"],"Other":["Third problem"]}}
            """;
        var client = CreateClient(HttpStatusCode.BadRequest, json);

        var result = await client.GetSampleNumbersAsync(Guid.NewGuid());

        Assert.Equal(["First problem", "Second problem", "Third problem"], result.Errors);
    }

    [Fact]
    public async Task GetSampleNumbersAsync_BadRequestWithNoErrors_FallsBackToADefaultMessage()
    {
        var client = CreateClient(HttpStatusCode.BadRequest, """{"status":400,"errors":{}}""");

        var result = await client.GetSampleNumbersAsync(Guid.NewGuid());

        Assert.Null(result.Page);
        Assert.Equal(["Sample Numbers could not be opened for this distribution."], result.Errors);
    }

    [Fact]
    public async Task GetSampleNumbersAsync_BadRequestWithNoProblemBody_FallsBackToADefaultMessage()
    {
        var client = CreateClient(HttpStatusCode.BadRequest, "null");

        var result = await client.GetSampleNumbersAsync(Guid.NewGuid());

        Assert.Equal(["Sample Numbers could not be opened for this distribution."], result.Errors);
    }

    [Fact]
    public async Task GetSampleNumbersAsync_ServerError_Throws()
    {
        var client = CreateClient(HttpStatusCode.InternalServerError, null);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetSampleNumbersAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetSampleNumbersAsync_ReturnsDeserializedResponse()
    {
        const string json = """
            {"monthlyDistributionSchemeId":"11111111-1111-1111-1111-111111111111","distributionReferenceFull":"17152A/BA","schemeIdentifier":"PT0060","schemeName":"Mastitis bovine","numberOfSetsRequired":9,"comments":"Existing","requiresAssessment":true,"isReadyForAssessment":false,"samples":[{"sampleId":"22222222-2222-2222-2222-222222222222","sampleNumber":"S1","internalDescription":"Internal 1","externalDescription":"","tabulationDescription":"","testConsultantsDescription":"","allocations":[{"sampleTestId":"33333333-3333-3333-3333-333333333333","testId":"44444444-4444-4444-4444-444444444444","isApplicable":true}]}],"tests":[{"testId":"44444444-4444-4444-4444-444444444444","testName":"Somatic cell count","testResultItemNames":["Result A"],"testMethodItemNames":["Method A"],"categoryItemNames":["Appearance (Colour)"]}]}
            """;
        var client = CreateClient(HttpStatusCode.OK, json);

        var pageResult = await client.GetSampleNumbersAsync(Guid.Parse("11111111-1111-1111-1111-111111111111"));

        Assert.Empty(pageResult.Errors);
        var result = pageResult.Page;
        Assert.NotNull(result);
        Assert.Equal("17152A/BA", result!.DistributionReferenceFull);
        Assert.Equal(9, result.NumberOfSetsRequired);
        Assert.True(result.RequiresAssessment);
        Assert.False(result.IsReadyForAssessment);
        var sample = Assert.Single(result.Samples);
        Assert.Equal("S1", sample.SampleNumber);
        Assert.True(Assert.Single(sample.Allocations).IsApplicable);
        Assert.Equal(["Appearance (Colour)"], Assert.Single(result.Tests).CategoryItemNames);
    }

    [Fact]
    public async Task SaveSampleNumbersAsync_PutsToSampleNumbersEndpointAndReturnsDeserializedResult()
    {
        var schemeId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        const string json = """{"success":false,"pageErrors":["Comments must not exceed 8000 characters"],"fieldErrorsBySampleId":{"22222222-2222-2222-2222-222222222222":[{"field":"SampleNumber","message":"Sample Number Required"}]}}""";
        HttpRequestMessage? captured = null;
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, json) { OnRequest = request => captured = request };
        var client = new DistributionApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });

        var result = await client.SaveSampleNumbersAsync(schemeId, new Contracts.Distribution.SampleNumbersSaveRequest("ok", []));

        Assert.NotNull(captured);
        Assert.Equal(HttpMethod.Put, captured!.Method);
        Assert.Equal($"/api/distributions/schemes/{schemeId}/sample-numbers", captured.RequestUri!.AbsolutePath);
        Assert.NotNull(result);
        Assert.False(result!.Success);
        Assert.Equal("Comments must not exceed 8000 characters", Assert.Single(result.PageErrors));
        Assert.Equal("Sample Number Required", Assert.Single(result.FieldErrorsBySampleId[Guid.Parse("22222222-2222-2222-2222-222222222222")]).Message);
    }
}
