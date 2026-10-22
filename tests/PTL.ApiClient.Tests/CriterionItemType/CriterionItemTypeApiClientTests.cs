using System.Net;
using PTL.ApiClient;
using PTL.ApiClient.Tests;
using PTL.Contracts.CriterionItemType;

namespace PTL.ApiClient.Tests.CriterionItemType;

public class CriterionItemTypeApiClientTests
{
    private static CriterionItemTypeApiClient CreateClient(HttpStatusCode statusCode, string? jsonContent) =>
        new(new HttpClient(new StubHttpMessageHandler(statusCode, jsonContent)) { BaseAddress = new Uri("http://localhost") });

    [Fact]
    public async Task GetByYearAsync_Success_ReturnsDeserializedItems()
    {
        const string json = """[{"criterionItemTypeId":"11111111-1111-1111-1111-111111111111","name":"Appearance","startYearId":2026,"endYearId":9999,"noLongerInUse":false,"foreignInstances":0,"foreignInstancesString":""}]""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.GetByYearAsync(2026);

        Assert.Single(result);
        Assert.Equal("Appearance", result[0].Name);
    }

    [Fact]
    public async Task GetByYearAsync_NullResponse_ReturnsEmpty()
    {
        var client = CreateClient(HttpStatusCode.OK, "null");

        var result = await client.GetByYearAsync(2026);

        Assert.Empty(result);
    }

    [Fact]
    public async Task CreateAsync_Success_ReturnsSuccessfulResult()
    {
        const string json = """{"criterionItemTypeId":"11111111-1111-1111-1111-111111111111","name":"Appearance","startYearId":2026,"endYearId":9999,"noLongerInUse":false,"foreignInstances":0,"foreignInstancesString":""}""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.CreateAsync(new CriterionItemTypeSaveRequest("Appearance", false, 2026));

        Assert.True(result.Success);
        Assert.Equal("Appearance", result.CriterionItemType?.Name);
    }

    [Fact]
    public async Task CreateAsync_BadRequest_ReturnsFieldErrors()
    {
        const string json = """{"errors":{"Name":["Name required"]}}""";
        var client = CreateClient(HttpStatusCode.BadRequest, json);

        var result = await client.CreateAsync(new CriterionItemTypeSaveRequest(string.Empty, false, 2026));

        Assert.False(result.Success);
        Assert.Contains("Name", result.FieldErrors.Keys);
    }

    [Fact]
    public async Task UpdateAsync_NotFound_ReturnsFailure()
    {
        var client = CreateClient(HttpStatusCode.NotFound, null);

        var result = await client.UpdateAsync(Guid.NewGuid(), new CriterionItemTypeSaveRequest("Appearance", false, 2026));

        Assert.False(result.Success);
    }

    [Fact]
    public async Task UpdateAsync_Conflict_ReturnsFailureWithMessage()
    {
        const string json = """{"success":false,"criterionItemType":null,"fieldErrors":{"":["This Criterion Item is being used on 1 Scheme(s): Scheme A and cannot be edited."]}}""";
        var client = CreateClient(HttpStatusCode.Conflict, json);

        var result = await client.UpdateAsync(Guid.NewGuid(), new CriterionItemTypeSaveRequest("Appearance", false, 2026));

        Assert.False(result.Success);
        Assert.Contains(result.FieldErrors[string.Empty], m => m.Contains("Scheme A"));
    }

    [Fact]
    public async Task SetNoLongerInUseAsync_Success_ReturnsUpdatedItem()
    {
        const string json = """{"criterionItemTypeId":"11111111-1111-1111-1111-111111111111","name":"Appearance","startYearId":2026,"endYearId":9999,"noLongerInUse":true,"foreignInstances":0,"foreignInstancesString":""}""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.SetNoLongerInUseAsync(Guid.NewGuid(), new CriterionItemTypeNoLongerInUseRequest(true, 2026));

        Assert.True(result.Success);
        Assert.True(result.CriterionItemType?.NoLongerInUse);
    }

    [Fact]
    public async Task DeleteAsync_Success_ReturnsResponse()
    {
        const string json = """{"success":true,"message":null}""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.DeleteAsync(Guid.NewGuid(), 2026);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task DeleteAsync_NullResponse_ReturnsFailureFallback()
    {
        var client = CreateClient(HttpStatusCode.OK, "null");

        var result = await client.DeleteAsync(Guid.NewGuid(), 2026);

        Assert.False(result.Success);
    }
}
