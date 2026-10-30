using System.Net;
using PTL.ApiClient.Distribution;

namespace PTL.ApiClient.Tests.Distribution;

public class DistributionApiClientTabulationForUserTests
{
    private static readonly Guid PublishedId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ParticipantId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid ViewerId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid TestConsultantId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid TestId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid AttachmentId = Guid.Parse("77777777-7777-7777-7777-777777777777");

    private const string ListJson = """
        {"tabulations":[{"tabulationId":"22222222-2222-2222-2222-222222222222","distributionReference":"D1a/S1",
         "schemeCode":"BT","schemeName":"Brucella","tabulationName":"Summary","distributionDate":"2026-05-01T00:00:00",
         "yearId":2026,"monthId":5}]}
        """;

    private static (DistributionApiClient Client, List<Uri> Requested) Create(HttpStatusCode statusCode, string? json)
    {
        var requested = new List<Uri>();
        var handler = new StubHttpMessageHandler(statusCode, json) { OnRequest = r => requested.Add(r.RequestUri!) };
        return (new DistributionApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") }), requested);
    }

    [Fact]
    public async Task GetTabulationsForUserAsync_PassesAllThreeRoleIdsAndDeserialises()
    {
        var (client, requested) = Create(HttpStatusCode.OK, ListJson);

        var result = await client.GetTabulationsForUserAsync(ParticipantId, ViewerId, TestConsultantId);

        Assert.Equal(
            $"/api/distributions/tabulations/for-user?participantId={ParticipantId}&viewerId={ViewerId}&testConsultantId={TestConsultantId}",
            Assert.Single(requested).PathAndQuery);
        var tabulation = Assert.Single(result.Tabulations);
        Assert.Equal("BT", tabulation.SchemeCode);
    }

    [Fact]
    public async Task GetPublishedTabulationForUserAsync_NotFound_ReturnsNull()
    {
        var (client, requested) = Create(HttpStatusCode.NotFound, null);

        var result = await client.GetPublishedTabulationForUserAsync(PublishedId, ParticipantId, null, null);

        Assert.Null(result);
        Assert.Equal(
            $"/api/distributions/published-tabulations/{PublishedId}/for-user?participantId={ParticipantId}&viewerId=&testConsultantId=",
            Assert.Single(requested).PathAndQuery);
    }

    [Fact]
    public async Task GetAssessmentResultsForUserAsync_NotFound_ReturnsNull()
    {
        var (client, requested) = Create(HttpStatusCode.NotFound, null);

        var result = await client.GetAssessmentResultsForUserAsync(PublishedId, TestId, null, ViewerId, null);

        Assert.Null(result);
        Assert.Equal(
            $"/api/distributions/published-tabulations/{PublishedId}/tests/{TestId}/assessment-results/for-user?participantId=&viewerId={ViewerId}&testConsultantId=",
            Assert.Single(requested).PathAndQuery);
    }

    [Fact]
    public async Task GetPublishedTabulationAttachmentForUserAsync_NotFound_ReturnsNull()
    {
        var (client, requested) = Create(HttpStatusCode.NotFound, null);

        var result = await client.GetPublishedTabulationAttachmentForUserAsync(PublishedId, AttachmentId, null, null, TestConsultantId);

        Assert.Null(result);
        Assert.Equal(
            $"/api/distributions/published-tabulations/{PublishedId}/attachments/{AttachmentId}/for-user?participantId=&viewerId=&testConsultantId={TestConsultantId}",
            Assert.Single(requested).PathAndQuery);
    }
}
