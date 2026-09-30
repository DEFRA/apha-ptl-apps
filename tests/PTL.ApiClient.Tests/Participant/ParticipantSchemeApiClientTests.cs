using System.Net;
using PTL.ApiClient.Tests;
using PTL.Contracts.Participant;

namespace PTL.ApiClient.Tests.Participant;

public class ParticipantSchemeApiClientTests
{
    private static ParticipantSchemeApiClient CreateClient(HttpStatusCode statusCode, string? jsonContent) =>
        new(new HttpClient(new StubHttpMessageHandler(statusCode, jsonContent)) { BaseAddress = new Uri("http://localhost") });

    private const string SampleJson = """
        {"participantSchemeId":"11111111-1111-1111-1111-111111111111","contractId":"22222222-2222-2222-2222-222222222222","participantId":"33333333-3333-3333-3333-333333333333","schemeId":"44444444-4444-4444-4444-444444444444","distributionMonthJan":true,"distributionMonthFeb":false,"distributionMonthMar":false,"distributionMonthApr":false,"distributionMonthMay":false,"distributionMonthJun":false,"distributionMonthJul":false,"distributionMonthAug":false,"distributionMonthSep":false,"distributionMonthOct":false,"distributionMonthNov":false,"distributionMonthDec":false,"canEditJan":true,"canEditFeb":true,"canEditMar":true,"canEditApr":true,"canEditMay":true,"canEditJun":true,"canEditJul":true,"canEditAug":true,"canEditSep":true,"canEditOct":true,"canEditNov":true,"canEditDec":true,"numberOfSetsRequired":2,"externalReference":"EXT-001","contact":"Alice","isRemoved":false,"importExportLicenceRequired":true,"customsCertificateRequired":false,"nonFeePaying":false,"packingInstructions":"Fragile","isWeightedPricing":true,"dataConsentDeclarationGiven":true,"isOverrideJan":false,"isOverrideFeb":false,"isOverrideMar":false,"isOverrideApr":false,"isOverrideMay":false,"isOverrideJun":false,"isOverrideJul":false,"isOverrideAug":false,"isOverrideSep":false,"isOverrideOct":false,"isOverrideNov":false,"isOverrideDec":false,"price":42.5,"participantDisplayName":"Lab One: LAB1","schemeDisplayName":"S1: Salmonella"}
        """;

    private static CreateParticipantSchemeRequest CreateRequest() => new(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
        true, false, false, false, false, false, false, false, false, false, false, false,
        2, "EXT-001", "Alice", true, false, false, "Fragile", true, true,
        false, false, false, false, false, false, false, false, false, false, false, false);

    private static UpdateParticipantSchemeRequest UpdateRequest() => new(
        true, false, false, false, false, false, false, false, false, false, false, false,
        2, "EXT-001", "Alice", true, false, false, "Fragile", true, true,
        false, false, false, false, false, false, false, false, false, false, false, false);

    [Fact]
    public async Task GetParticipantSchemeAsync_NotFound_ReturnsNull()
    {
        var client = CreateClient(HttpStatusCode.NotFound, null);

        var result = await client.GetParticipantSchemeAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetParticipantSchemeAsync_Success_ReturnsDeserializedResponse()
    {
        var client = CreateClient(HttpStatusCode.OK, SampleJson);

        var result = await client.GetParticipantSchemeAsync(Guid.NewGuid());

        Assert.NotNull(result);
        Assert.Equal("Lab One: LAB1", result!.ParticipantDisplayName);
    }

    [Fact]
    public async Task CreateParticipantSchemeAsync_Success_ReturnsSaveResult()
    {
        var client = CreateClient(HttpStatusCode.Created, SampleJson);

        var result = await client.CreateParticipantSchemeAsync(CreateRequest());

        Assert.True(result.Success);
        Assert.NotNull(result.ParticipantScheme);
    }

    [Fact]
    public async Task CreateParticipantSchemeAsync_ValidationFailure_ReturnsFieldErrors()
    {
        const string problemJson = """{"errors":{"CountryId":["Country must be selected"]}}""";
        var client = CreateClient(HttpStatusCode.BadRequest, problemJson);

        var result = await client.CreateParticipantSchemeAsync(CreateRequest());

        Assert.False(result.Success);
        Assert.Contains("CountryId", result.FieldErrors.Keys);
    }

    [Fact]
    public async Task UpdateParticipantSchemeAsync_NotFound_ReturnsFailureResult()
    {
        var client = CreateClient(HttpStatusCode.NotFound, null);

        var result = await client.UpdateParticipantSchemeAsync(Guid.NewGuid(), UpdateRequest());

        Assert.False(result.Success);
    }

    [Fact]
    public async Task UpdateParticipantSchemeAsync_Success_ReturnsSaveResult()
    {
        var client = CreateClient(HttpStatusCode.OK, SampleJson);

        var result = await client.UpdateParticipantSchemeAsync(Guid.NewGuid(), UpdateRequest());

        Assert.True(result.Success);
    }

    [Fact]
    public async Task DeleteParticipantSchemeAsync_NotFound_ReturnsFailureResult()
    {
        var client = CreateClient(HttpStatusCode.NotFound, null);

        var result = await client.DeleteParticipantSchemeAsync(Guid.NewGuid());

        Assert.False(result.Success);
    }

    [Fact]
    public async Task DeleteParticipantSchemeAsync_BadRequest_ReturnsFieldErrors()
    {
        const string problemJson = """{"errors":{"ParticipantSchemeId":["This participant scheme cannot be removed."]}}""";
        var client = CreateClient(HttpStatusCode.BadRequest, problemJson);

        var result = await client.DeleteParticipantSchemeAsync(Guid.NewGuid());

        Assert.False(result.Success);
        Assert.Contains("ParticipantSchemeId", result.FieldErrors.Keys);
    }

    [Fact]
    public async Task DeleteParticipantSchemeAsync_Success_ReturnsSuccessResult()
    {
        var client = CreateClient(HttpStatusCode.NoContent, null);

        var result = await client.DeleteParticipantSchemeAsync(Guid.NewGuid());

        Assert.True(result.Success);
        Assert.Null(result.ParticipantScheme);
    }
}
