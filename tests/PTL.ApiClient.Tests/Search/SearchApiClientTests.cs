using System.Net;
using PTL.Contracts.Search;

namespace PTL.ApiClient.Tests.Search;

public class SearchApiClientTests
{
    private static (SearchApiClient Client, List<Uri?> Requests) CreateClient(HttpStatusCode statusCode, string? jsonContent)
    {
        var handler = new StubHttpMessageHandler(statusCode, jsonContent);
        var requests = new List<Uri?>();
        handler.OnRequest = request => requests.Add(request.RequestUri);
        return (new SearchApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") }), requests);
    }

    [Fact]
    public async Task SearchCustomersAsync_ReturnsDeserializedRowsInOrder()
    {
        const string json = """[{"customerId":"11111111-1111-1111-1111-111111111111","qalNumber":"QAL/2","accountNumber":"ACC2","name":"Zulu","contactName":"Zed","organisation":"Zulu Org","country":"France"},{"customerId":"22222222-2222-2222-2222-222222222222","qalNumber":"QAL/1","accountNumber":"ACC1","name":"Alpha","contactName":"Amy","organisation":"Alpha Org","country":"Spain"}]""";
        var (client, _) = CreateClient(HttpStatusCode.OK, json);

        var result = await client.SearchCustomersAsync(new CustomerSearchCriteriaRequest());

        Assert.Equal(2, result.Count);
        Assert.Equal("Zulu", result[0].Name);
        Assert.Equal(new Guid("11111111-1111-1111-1111-111111111111"), result[0].CustomerId);
        Assert.Equal("QAL/1", result[1].QalNumber);
        Assert.Equal("Spain", result[1].Country);
    }

    [Fact]
    public async Task SearchCustomersAsync_BuildsUrlWithEveryCriterion()
    {
        var countryId = Guid.NewGuid();
        var (client, requests) = CreateClient(HttpStatusCode.OK, "[]");

        await client.SearchCustomersAsync(new CustomerSearchCriteriaRequest("QAL/1", "ACC-1", "Alpha", "Amy", "Org", countryId));

        var uri = Assert.Single(requests)!;
        Assert.Equal("/api/search/customers", uri.AbsolutePath);
        Assert.Equal($"?qalNumber=QAL%2F1&accountNumber=ACC-1&name=Alpha&contactName=Amy&organisation=Org&countryId={countryId}", uri.Query);
    }

    [Fact]
    public async Task SearchCustomersAsync_EscapesQueryValues()
    {
        var (client, requests) = CreateClient(HttpStatusCode.OK, "[]");

        await client.SearchCustomersAsync(new CustomerSearchCriteriaRequest(Name: "Smith & Sons=1", ContactName: "O'Brien #2", Organisation: "a b+c?d"));

        var query = Assert.Single(requests)!.Query;
        Assert.Contains("name=Smith%20%26%20Sons%3D1", query, StringComparison.Ordinal);
        Assert.Contains("contactName=O%27Brien%20%232", query, StringComparison.Ordinal);
        Assert.Contains("organisation=a%20b%2Bc%3Fd", query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchCustomersAsync_NullCriteria_SendEmptyValues()
    {
        var (client, requests) = CreateClient(HttpStatusCode.OK, "[]");

        await client.SearchCustomersAsync(new CustomerSearchCriteriaRequest());

        Assert.Equal("?qalNumber=&accountNumber=&name=&contactName=&organisation=&countryId=", Assert.Single(requests)!.Query);
    }

    [Fact]
    public async Task SearchCustomersAsync_NullResponse_ReturnsEmptyList()
    {
        var (client, _) = CreateClient(HttpStatusCode.OK, "null");

        var result = await client.SearchCustomersAsync(new CustomerSearchCriteriaRequest());

        Assert.Empty(result);
    }

    [Fact]
    public async Task SearchCustomersAsync_EmptyArray_ReturnsEmptyList()
    {
        var (client, _) = CreateClient(HttpStatusCode.OK, "[]");

        var result = await client.SearchCustomersAsync(new CustomerSearchCriteriaRequest(Name: "nothing"));

        Assert.Empty(result);
    }

    [Fact]
    public async Task SearchCustomersAsync_ServerError_Throws()
    {
        var (client, _) = CreateClient(HttpStatusCode.InternalServerError, null);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.SearchCustomersAsync(new CustomerSearchCriteriaRequest()));
    }

    [Fact]
    public async Task SearchParticipantsAsync_ReturnsDeserializedRowsInOrder()
    {
        const string json = """[{"participantId":"33333333-3333-3333-3333-333333333333","customerId":"11111111-1111-1111-1111-111111111111","labCode":"LAB1","oldLabCode":"OLD2","labName":"Lab One","contactName":"Bob"},{"participantId":"33333333-3333-3333-3333-333333333333","customerId":"11111111-1111-1111-1111-111111111111","labCode":"LAB1","oldLabCode":"OLD1","labName":"Lab One","contactName":"Bob"}]""";
        var (client, _) = CreateClient(HttpStatusCode.OK, json);

        var result = await client.SearchParticipantsAsync(new ParticipantSearchCriteriaRequest());

        Assert.Equal(2, result.Count);
        Assert.Equal("OLD2", result[0].OldLabCode);
        Assert.Equal("OLD1", result[1].OldLabCode);
        Assert.Equal("Lab One", result[0].LabName);
        Assert.Equal(new Guid("33333333-3333-3333-3333-333333333333"), result[0].ParticipantId);
    }

    [Fact]
    public async Task SearchParticipantsAsync_BuildsUrlWithEscapedCriteria()
    {
        var (client, requests) = CreateClient(HttpStatusCode.OK, "[]");

        await client.SearchParticipantsAsync(new ParticipantSearchCriteriaRequest("LAB 1", "Lab & One", "Bob/Ann"));

        var uri = Assert.Single(requests)!;
        Assert.Equal("/api/search/participants", uri.AbsolutePath);
        Assert.Equal("?labCode=LAB%201&labName=Lab%20%26%20One&contactName=Bob%2FAnn", uri.Query);
    }

    [Fact]
    public async Task SearchParticipantsAsync_NullCriteria_SendEmptyValues()
    {
        var (client, requests) = CreateClient(HttpStatusCode.OK, "[]");

        await client.SearchParticipantsAsync(new ParticipantSearchCriteriaRequest());

        Assert.Equal("?labCode=&labName=&contactName=", Assert.Single(requests)!.Query);
    }

    [Fact]
    public async Task SearchParticipantsAsync_NullResponse_ReturnsEmptyList()
    {
        var (client, _) = CreateClient(HttpStatusCode.OK, "null");

        var result = await client.SearchParticipantsAsync(new ParticipantSearchCriteriaRequest());

        Assert.Empty(result);
    }

    [Fact]
    public async Task SearchParticipantsAsync_ServerError_Throws()
    {
        var (client, _) = CreateClient(HttpStatusCode.BadGateway, null);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.SearchParticipantsAsync(new ParticipantSearchCriteriaRequest()));
    }
}
