using System.Net;
using PTL.ApiClient.Tests;
using PTL.Contracts.Contract;

namespace PTL.ApiClient.Tests.Contract;

public class ContractRenewalApiClientTests
{
    private static ContractRenewalApiClient CreateClient(HttpStatusCode statusCode, string? jsonContent) =>
        new(new HttpClient(new StubHttpMessageHandler(statusCode, jsonContent)) { BaseAddress = new Uri("http://localhost") });

    [Fact]
    public async Task GetRenewableContractsAsync_NullResponse_ReturnsSafeDefault()
    {
        var client = CreateClient(HttpStatusCode.OK, "null");

        var result = await client.GetRenewableContractsAsync(Guid.NewGuid());

        Assert.False(result.IsAllowed);
        Assert.Null(result.BlockedReason);
        Assert.Empty(result.ExistingSignatories);
        Assert.Empty(result.Contracts);
    }

    [Fact]
    public async Task GetRenewableContractsAsync_Success_ReturnsDeserializedResponse()
    {
        const string json = """
            {"isAllowed":true,"blockedReason":null,"existingSignatories":["Alice Example"],"contracts":[{"contractId":"11111111-1111-1111-1111-111111111111","suffix":"A","contractSignatory":"Alice Example","renewalInformation":"info","actionsRequired":"","isActive":true,"noOfItems":1}]}
            """;
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.GetRenewableContractsAsync(Guid.NewGuid());

        Assert.True(result.IsAllowed);
        Assert.Single(result.ExistingSignatories);
        Assert.Single(result.Contracts);
    }

    [Fact]
    public async Task GetRenewableItemsAsync_NullResponse_ReturnsEmptyItems()
    {
        var client = CreateClient(HttpStatusCode.OK, "null");

        var result = await client.GetRenewableItemsAsync(Guid.NewGuid());

        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task GetRenewableItemsAsync_Success_ReturnsDeserializedItems()
    {
        const string json = """
            {"items":[{"contractId":"11111111-1111-1111-1111-111111111111","suffix":"A","participantSchemeId":"22222222-2222-2222-2222-222222222222","labCode":"LAB1","labName":"Lab One","oldSchemeIdentifier":"S1","oldSchemeName":"Old","newSchemeIdentifier":"S2","newSchemeName":"New","isRenewable":true,"identifier":"S1"}]}
            """;
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.GetRenewableItemsAsync(Guid.NewGuid());

        Assert.Single(result.Items);
        Assert.True(result.Items[0].IsRenewable);
    }

    [Fact]
    public async Task RenewContractsAsync_Success_ReturnsDeserializedResponse()
    {
        var newContractId = Guid.NewGuid();
        var json = $$"""{"success":true,"newContractId":"{{newContractId}}","errorMessage":null}""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.RenewContractsAsync(Guid.NewGuid(), new RenewContractRequest([], [], null));

        Assert.True(result.Success);
        Assert.Equal(newContractId, result.NewContractId);
    }

    [Fact]
    public async Task RenewContractsAsync_NullResponse_ReturnsFailureDefault()
    {
        var client = CreateClient(HttpStatusCode.OK, "null");

        var result = await client.RenewContractsAsync(Guid.NewGuid(), new RenewContractRequest([], [], null));

        Assert.False(result.Success);
        Assert.Equal("The request was invalid.", result.ErrorMessage);
    }
}
