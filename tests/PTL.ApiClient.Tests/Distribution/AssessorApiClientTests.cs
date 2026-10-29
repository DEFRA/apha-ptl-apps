using System.Net;
using PTL.ApiClient.Distribution;
using PTL.Contracts.Distribution;

namespace PTL.ApiClient.Tests.Distribution;

// The Assessor calls - /api/assessor/menu and /api/assessor/tests/{testId}/items.
public class AssessorApiClientTests
{
    private static readonly Guid TestId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid SchemeId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static (AssessorApiClient Client, List<(HttpMethod Method, string PathAndQuery)> Requests) Create(HttpStatusCode statusCode, string? json)
    {
        var requests = new List<(HttpMethod, string)>();
        var handler = new StubHttpMessageHandler(statusCode, json) { OnRequest = r => requests.Add((r.Method, r.RequestUri!.PathAndQuery)) };
        return (new AssessorApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") }), requests);
    }

    private static string MenuJson() => $$$"""
        {"years":[2020,2021],"selectedYear":2021,"months":[{"yearId":2021,"monthId":2,"distributions":[{"monthlyDistributionSchemeId":"{{{SchemeId}}}",
        "distributionTitle":"14468/A/HI: PT0166 Cellular Pathology Technique (CPT)","tests":[{"testId":"{{{TestId}}}","testType":"Giemsa","isAssessmentSubmitted":true}]}]}]}
        """;

    [Fact]
    public async Task GetMenuAsync_WithoutAYear_RequestsTheBareUrlAndDeserialises()
    {
        var (client, requests) = Create(HttpStatusCode.OK, MenuJson());

        var result = await client.GetMenuAsync(null);

        Assert.Equal((HttpMethod.Get, "/api/assessor/menu"), Assert.Single(requests));
        Assert.True(result.IsOk);
        var menu = result.Value!;
        Assert.Equal([2020, 2021], menu.Years);
        Assert.Equal(2021, menu.SelectedYear);
        var distribution = Assert.Single(Assert.Single(menu.Months).Distributions);
        Assert.Equal((SchemeId, "14468/A/HI: PT0166 Cellular Pathology Technique (CPT)"), (distribution.MonthlyDistributionSchemeId, distribution.DistributionTitle));
        var test = Assert.Single(distribution.Tests);
        Assert.Equal((TestId, "Giemsa", true), (test.TestId, test.TestType, test.IsAssessmentSubmitted));
    }

    [Fact]
    public async Task GetMenuAsync_WithAYear_SendsItInTheQuery()
    {
        var (client, requests) = Create(HttpStatusCode.OK, MenuJson());

        await client.GetMenuAsync(2020);

        Assert.Equal("/api/assessor/menu?year=2020", Assert.Single(requests).PathAndQuery);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, AssessorApiStatus.Forbidden)]
    [InlineData(HttpStatusCode.NotFound, AssessorApiStatus.NotFound)]
    public async Task GetMenuAsync_RefusedOrMissing_MapsTheStatusWithoutAValue(HttpStatusCode statusCode, AssessorApiStatus expected)
    {
        var (client, _) = Create(statusCode, null);

        var result = await client.GetMenuAsync(null);

        Assert.Equal(expected, result.Status);
        Assert.False(result.IsOk);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task GetMenuAsync_ServerError_Throws()
    {
        var (client, _) = Create(HttpStatusCode.InternalServerError, null);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetMenuAsync(null));
    }

    [Fact]
    public async Task GetItemListAsync_RequestsTheTestsItemsAndDeserialises()
    {
        var sampleTestId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var json = $$$"""
            {"testId":"{{{TestId}}}","yearMonthTitle":"February 2021","distributionTitle":"14468/A/HI: PT0166 CPT","testName":"Giemsa","isAssessmentSubmitted":true,
            "submittedBy":"Mary Dexter","submittedDate":"2021-03-17T13:17:39","samples":[{"sampleId":"{{{SchemeId}}}","sampleNumber":"21/7140",
            "items":[{"sampleTestId":"{{{sampleTestId}}}","participantId":"{{{participantId}}}","labCode":"1126","hasAssessment":true}]}],"hasCompletedAssessment":true}
            """;
        var (client, requests) = Create(HttpStatusCode.OK, json);

        var result = await client.GetItemListAsync(TestId);

        Assert.Equal((HttpMethod.Get, $"/api/assessor/tests/{TestId}/items"), Assert.Single(requests));
        var list = result.Value!;
        Assert.Equal(("February 2021", "Giemsa", true, "Mary Dexter", (DateTime?)new DateTime(2021, 3, 17, 13, 17, 39)),
            (list.YearMonthTitle, list.TestName, list.IsAssessmentSubmitted, list.SubmittedBy, list.SubmittedDate));
        Assert.True(list.HasCompletedAssessment);
        var sample = Assert.Single(list.Samples);
        Assert.Equal("21/7140", sample.SampleNumber);
        var item = Assert.Single(sample.Items);
        Assert.Equal((sampleTestId, participantId, "1126", true), (item.SampleTestId, item.ParticipantId, item.LabCode, item.HasAssessment));
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, AssessorApiStatus.Forbidden)]
    [InlineData(HttpStatusCode.NotFound, AssessorApiStatus.NotFound)]
    public async Task GetItemListAsync_RefusedOrMissing_MapsTheStatusWithoutAValue(HttpStatusCode statusCode, AssessorApiStatus expected)
    {
        var (client, _) = Create(statusCode, null);

        var result = await client.GetItemListAsync(TestId);

        Assert.Equal(expected, result.Status);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task GetItemListAsync_ServerError_Throws()
    {
        var (client, _) = Create(HttpStatusCode.InternalServerError, null);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetItemListAsync(TestId));
    }

    private static readonly Guid SampleTestId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid ParticipantId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid CategoryId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly Guid CriterionId = Guid.Parse("88888888-8888-8888-8888-888888888888");

    [Fact]
    public async Task GetEntryAsync_RequestsTheEntryAndDeserialises()
    {
        var json = $$$"""
            {"testId":"{{{TestId}}}","sampleTestId":"{{{SampleTestId}}}","participantId":"{{{ParticipantId}}}","yearMonthTitle":"February 2021","distributionTitle":"14468/A/HI: PT0166 CPT",
            "labCode":"1126","sampleNumber":"21/7140","testName":"Giemsa","isAssessmentSubmitted":false,"comment":"Good","overallScore":3,
            "resultItems":[{"name":"Isolate","result":"Positive"}],
            "categories":[{"categoryItemId":"{{{CategoryId}}}","name":"Morphology","score":2,"criteria":[{"criterionItemId":"{{{CriterionId}}}","name":"Shape","isSelected":true}]}]}
            """;
        var (client, requests) = Create(HttpStatusCode.OK, json);

        var result = await client.GetEntryAsync(SampleTestId, ParticipantId);

        Assert.Equal((HttpMethod.Get, $"/api/assessor/entries/{SampleTestId}/{ParticipantId}"), Assert.Single(requests));
        var entry = result.Value!;
        Assert.Equal(("1126", "21/7140", "Giemsa", "Good", 3, false), (entry.LabCode, entry.SampleNumber, entry.TestName, entry.Comment, entry.OverallScore, entry.IsAssessmentSubmitted));
        Assert.Equal(("Isolate", "Positive"), (Assert.Single(entry.ResultItems).Name, entry.ResultItems[0].Result));
        var category = Assert.Single(entry.Categories);
        Assert.Equal((CategoryId, "Morphology", 2), (category.CategoryItemId, category.Name, category.Score));
        Assert.Equal((CriterionId, "Shape", true), (category.Criteria[0].CriterionItemId, category.Criteria[0].Name, category.Criteria[0].IsSelected));
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, AssessorApiStatus.Forbidden)]
    [InlineData(HttpStatusCode.NotFound, AssessorApiStatus.NotFound)]
    public async Task GetEntryAsync_RefusedOrMissing_MapsTheStatusWithoutAValue(HttpStatusCode statusCode, AssessorApiStatus expected)
    {
        var (client, _) = Create(statusCode, null);

        var result = await client.GetEntryAsync(SampleTestId, ParticipantId);

        Assert.Equal(expected, result.Status);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task GetEntryAsync_ServerError_Throws()
    {
        var (client, _) = Create(HttpStatusCode.InternalServerError, null);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetEntryAsync(SampleTestId, ParticipantId));
    }

    private static AssessorEntrySaveRequest SaveRequest() =>
        new("Good work", 4, [new AssessorCategorySaveRequest(CategoryId, 3, [CriterionId])]);

    [Fact]
    public async Task SaveEntryAsync_PutsTheValuesToTheEntry()
    {
        string? body = null;
        var requests = new List<(HttpMethod, string)>();
        var handler = new StubHttpMessageHandler(HttpStatusCode.NoContent, null)
        {
            OnRequest = r =>
            {
                requests.Add((r.Method, r.RequestUri!.PathAndQuery));
                body = r.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            },
        };
        var client = new AssessorApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });

        var result = await client.SaveEntryAsync(SampleTestId, ParticipantId, SaveRequest());

        Assert.Equal(AssessorSaveStatus.Saved, result.Status);
        Assert.Empty(result.Errors);
        Assert.Equal((HttpMethod.Put, $"/api/assessor/entries/{SampleTestId}/{ParticipantId}"), Assert.Single(requests));
        Assert.Contains("\"comment\":\"Good work\"", body, StringComparison.Ordinal);
        Assert.Contains("\"overallScore\":4", body, StringComparison.Ordinal);
        Assert.Contains($"\"categoryItemId\":\"{CategoryId}\"", body, StringComparison.Ordinal);
        Assert.Contains($"\"criterionItemIds\":[\"{CriterionId}\"]", body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, AssessorSaveStatus.NotFound)]
    [InlineData(HttpStatusCode.Forbidden, AssessorSaveStatus.Forbidden)]
    [InlineData(HttpStatusCode.Conflict, AssessorSaveStatus.Submitted)]
    public async Task SaveEntryAsync_Refused_MapsTheStatus(HttpStatusCode statusCode, AssessorSaveStatus expected)
    {
        var (client, _) = Create(statusCode, null);

        var result = await client.SaveEntryAsync(SampleTestId, ParticipantId, SaveRequest());

        Assert.Equal(expected, result.Status);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task SaveEntryAsync_BadRequest_CarriesTheFieldsAtFault()
    {
        var (client, _) = Create(HttpStatusCode.BadRequest, """{"errors":{"OverallScore":["An overall score is required"],"Categories[0].Score":["A category score is required"]}}""");

        var result = await client.SaveEntryAsync(SampleTestId, ParticipantId, SaveRequest());

        Assert.Equal(AssessorSaveStatus.Invalid, result.Status);
        Assert.Equal([("OverallScore", "An overall score is required"), ("Categories[0].Score", "A category score is required")], result.Errors.Select(e => (e.Key, e.Message)));
    }

    [Fact]
    public async Task SaveEntryAsync_BadRequestWithoutABody_HasNoFields()
    {
        var (client, _) = Create(HttpStatusCode.BadRequest, "null");

        var result = await client.SaveEntryAsync(SampleTestId, ParticipantId, SaveRequest());

        Assert.Equal(AssessorSaveStatus.Invalid, result.Status);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task SaveEntryAsync_ServerError_Throws()
    {
        var (client, _) = Create(HttpStatusCode.InternalServerError, null);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.SaveEntryAsync(SampleTestId, ParticipantId, SaveRequest()));
    }

    [Theory]
    [InlineData(HttpStatusCode.NoContent, AssessorSubmitStatus.Submitted)]
    [InlineData(HttpStatusCode.NotFound, AssessorSubmitStatus.NotFound)]
    [InlineData(HttpStatusCode.Forbidden, AssessorSubmitStatus.Forbidden)]
    [InlineData(HttpStatusCode.Conflict, AssessorSubmitStatus.AlreadySubmitted)]
    public async Task SubmitAsync_PostsToTheTestAndMapsTheStatus(HttpStatusCode statusCode, AssessorSubmitStatus expected)
    {
        var (client, requests) = Create(statusCode, null);

        var status = await client.SubmitAsync(TestId);

        Assert.Equal(expected, status);
        Assert.Equal((HttpMethod.Post, $"/api/assessor/tests/{TestId}/submit"), Assert.Single(requests));
    }

    [Fact]
    public async Task SubmitAsync_ServerError_Throws()
    {
        var (client, _) = Create(HttpStatusCode.InternalServerError, null);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.SubmitAsync(TestId));
    }
}
