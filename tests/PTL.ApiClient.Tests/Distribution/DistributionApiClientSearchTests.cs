using System.Net;
using PTL.ApiClient.Distribution;
using PTL.Contracts.Search;

namespace PTL.ApiClient.Tests.Distribution;

// Legacy Distributions/Search.aspx - Distribution Number / Sample Number tabs, calling PTL.Api's
// generic search endpoints (api/search/distributions, api/search/samples).
public class DistributionApiClientSearchTests
{
    private static (DistributionApiClient Client, List<Uri?> Requests) CreateClient(HttpStatusCode statusCode, string? jsonContent)
    {
        var handler = new StubHttpMessageHandler(statusCode, jsonContent);
        var requests = new List<Uri?>();
        handler.OnRequest = request => requests.Add(request.RequestUri);
        return (new DistributionApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") }), requests);
    }

    [Fact]
    public async Task SearchByDistributionNumberAsync_ReturnsDeserializedRowsInOrder()
    {
        const string json = """[{"monthlyDistributionSchemeId":"11111111-1111-1111-1111-111111111111","schemeId":"22222222-2222-2222-2222-222222222222","distributionNumber":"D1a/00002","schemeName":"D1a/S2 BT","schemeIdentifier":"D1a","year":"2026","month":"June"},{"monthlyDistributionSchemeId":"33333333-3333-3333-3333-333333333333","schemeId":"44444444-4444-4444-4444-444444444444","distributionNumber":"D1a/00001","schemeName":"D1a/S1 BT","schemeIdentifier":"D1a","year":"2026","month":"May"}]""";
        var (client, _) = CreateClient(HttpStatusCode.OK, json);

        var result = await client.SearchByDistributionNumberAsync(new DistributionNumberSearchCriteriaRequest());

        Assert.Equal(2, result.Count);
        Assert.Equal("D1a/00002", result[0].DistributionNumber);
        Assert.Equal(new Guid("11111111-1111-1111-1111-111111111111"), result[0].MonthlyDistributionSchemeId);
        Assert.Equal("D1a/00001", result[1].DistributionNumber);
        Assert.Equal("May", result[1].Month);
    }

    [Fact]
    public async Task SearchByDistributionNumberAsync_BuildsTheUrlWithTheEscapedCriterion()
    {
        var (client, requests) = CreateClient(HttpStatusCode.OK, "[]");

        await client.SearchByDistributionNumberAsync(new DistributionNumberSearchCriteriaRequest("D1a & Co/1"));

        var uri = Assert.Single(requests)!;
        Assert.Equal("/api/search/distributions", uri.AbsolutePath);
        Assert.Equal("?distributionNumber=D1a%20%26%20Co%2F1", uri.Query);
    }

    [Fact]
    public async Task SearchByDistributionNumberAsync_NullCriteria_SendsAnEmptyValue()
    {
        var (client, requests) = CreateClient(HttpStatusCode.OK, "[]");

        await client.SearchByDistributionNumberAsync(new DistributionNumberSearchCriteriaRequest());

        Assert.Equal("?distributionNumber=", Assert.Single(requests)!.Query);
    }

    [Fact]
    public async Task SearchByDistributionNumberAsync_NullResponse_ReturnsEmptyList()
    {
        var (client, _) = CreateClient(HttpStatusCode.OK, "null");

        var result = await client.SearchByDistributionNumberAsync(new DistributionNumberSearchCriteriaRequest());

        Assert.Empty(result);
    }

    [Fact]
    public async Task SearchByDistributionNumberAsync_EmptyArray_ReturnsEmptyList()
    {
        var (client, _) = CreateClient(HttpStatusCode.OK, "[]");

        var result = await client.SearchByDistributionNumberAsync(new DistributionNumberSearchCriteriaRequest("nothing"));

        Assert.Empty(result);
    }

    [Fact]
    public async Task SearchByDistributionNumberAsync_ServerError_Throws()
    {
        var (client, _) = CreateClient(HttpStatusCode.InternalServerError, null);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.SearchByDistributionNumberAsync(new DistributionNumberSearchCriteriaRequest()));
    }

    [Fact]
    public async Task SearchBySampleNumberAsync_ReturnsDeserializedRowsInOrder()
    {
        const string json = """[{"sampleId":"55555555-5555-5555-5555-555555555555","schemeId":"66666666-6666-6666-6666-666666666666","sampleNumber":"S00002","schemeName":"D1a/S1 BT","schemeIdentifier":"D1a","distributionNumber":"D1a/00001","year":"2026","month":"May"},{"sampleId":"77777777-7777-7777-7777-777777777777","schemeId":"66666666-6666-6666-6666-666666666666","sampleNumber":"S00001","schemeName":"D1a/S1 BT","schemeIdentifier":"D1a","distributionNumber":"D1a/00001","year":"2026","month":"May"}]""";
        var (client, _) = CreateClient(HttpStatusCode.OK, json);

        var result = await client.SearchBySampleNumberAsync(new SampleNumberSearchCriteriaRequest());

        Assert.Equal(2, result.Count);
        Assert.Equal("S00002", result[0].SampleNumber);
        Assert.Equal(new Guid("55555555-5555-5555-5555-555555555555"), result[0].SampleId);
        Assert.Equal("S00001", result[1].SampleNumber);
    }

    [Fact]
    public async Task SearchBySampleNumberAsync_BuildsTheUrlWithTheEscapedCriterion()
    {
        var (client, requests) = CreateClient(HttpStatusCode.OK, "[]");

        await client.SearchBySampleNumberAsync(new SampleNumberSearchCriteriaRequest("S 1/Ann"));

        var uri = Assert.Single(requests)!;
        Assert.Equal("/api/search/samples", uri.AbsolutePath);
        Assert.Equal("?sampleNumber=S%201%2FAnn", uri.Query);
    }

    [Fact]
    public async Task SearchBySampleNumberAsync_NullCriteria_SendsAnEmptyValue()
    {
        var (client, requests) = CreateClient(HttpStatusCode.OK, "[]");

        await client.SearchBySampleNumberAsync(new SampleNumberSearchCriteriaRequest());

        Assert.Equal("?sampleNumber=", Assert.Single(requests)!.Query);
    }

    [Fact]
    public async Task SearchBySampleNumberAsync_NullResponse_ReturnsEmptyList()
    {
        var (client, _) = CreateClient(HttpStatusCode.OK, "null");

        var result = await client.SearchBySampleNumberAsync(new SampleNumberSearchCriteriaRequest());

        Assert.Empty(result);
    }

    [Fact]
    public async Task SearchBySampleNumberAsync_EmptyArray_ReturnsEmptyList()
    {
        var (client, _) = CreateClient(HttpStatusCode.OK, "[]");

        var result = await client.SearchBySampleNumberAsync(new SampleNumberSearchCriteriaRequest("nothing"));

        Assert.Empty(result);
    }

    [Fact]
    public async Task SearchBySampleNumberAsync_ServerError_Throws()
    {
        var (client, _) = CreateClient(HttpStatusCode.BadGateway, null);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.SearchBySampleNumberAsync(new SampleNumberSearchCriteriaRequest()));
    }
}
