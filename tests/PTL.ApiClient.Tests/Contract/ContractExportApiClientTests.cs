using System.Net;
using PTL.ApiClient.Tests;

namespace PTL.ApiClient.Tests.Contract;

public class ContractExportApiClientTests
{
    private static ContractExportApiClient CreateClient(HttpStatusCode statusCode, string? jsonContent) =>
        new(new HttpClient(new StubHttpMessageHandler(statusCode, jsonContent)) { BaseAddress = new Uri("http://localhost") });

    [Fact]
    public async Task GetSampleAddressesAsync_ReturnsDeserializedList()
    {
        const string json = """
            [{"contractId":"11111111-1111-1111-1111-111111111111","participantId":"22222222-2222-2222-2222-222222222222","qalNumber":"QAL0001","labCode":"LAB1","contactName":"Alice Example","organisation":"Lab One Ltd","address1":"1 Street","address2":"Town","address3":"County","address4":"Country","address5":"Postcode","country":"United Kingdom","telephone":"020 1234 5678","fax":"020 1234 5679","email":"alice@example.com","vatNumber":"GB123456789","accountNumber":"ACC001","vatRating":"Standard","purchaseOrderNumber":"PO12345","feePayingSchemes":[],"nonFeePayingSchemes":[]}]
            """;
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.GetSampleAddressesAsync(Guid.NewGuid());

        Assert.Single(result);
        Assert.Equal("LAB1", result[0].LabCode);
    }

    [Fact]
    public async Task GetSampleAddressesAsync_NullResponse_ReturnsEmptyList()
    {
        var client = CreateClient(HttpStatusCode.OK, "null");

        var result = await client.GetSampleAddressesAsync(Guid.NewGuid());

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetRenewalAsync_NotFound_ReturnsNull()
    {
        var client = CreateClient(HttpStatusCode.NotFound, null);

        var result = await client.GetRenewalAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetRenewalAsync_Success_ReturnsDeserializedResponse()
    {
        const string json = """
            {"contractId":"11111111-1111-1111-1111-111111111111","customerId":"22222222-2222-2222-2222-222222222222","qalNumber":"QAL0001","organisationName":"Sample Labs Ltd","contactName":"Alice Example","address1":"1 Street","address2":"Town","address3":"County","address4":"Country","address5":"Postcode","country":"United Kingdom","contractStartDate":"2026-04-01T00:00:00","contractEndDate":"2027-03-31T00:00:00","renewalInformation":"Renewal info"}
            """;
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.GetRenewalAsync(Guid.NewGuid());

        Assert.NotNull(result);
        Assert.Equal("Renewal info", result!.RenewalInformation);
    }
}
