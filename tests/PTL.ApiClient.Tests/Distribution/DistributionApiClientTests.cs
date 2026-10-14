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

    [Fact]
    public async Task GetIntendedResultsAsync_NotFound_ReturnsNull()
    {
        var client = CreateClient(HttpStatusCode.NotFound, null);

        var result = await client.GetIntendedResultsAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetIntendedResultsAsync_ServerError_Throws()
    {
        var client = CreateClient(HttpStatusCode.InternalServerError, null);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetIntendedResultsAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetIntendedResultsAsync_ReturnsDeserializedResponse()
    {
        const string json = """
            {"monthlyDistributionSchemeId":"11111111-1111-1111-1111-111111111111","distributionReferenceFull":"17152A/BA","schemeIdentifier":"PT0060","schemeName":"Mastitis bovine","requiresAuditReason":true,"tests":[{"testId":"44444444-4444-4444-4444-444444444444","testName":"Somatic cell count","resultItems":[{"testResultItemId":"55555555-5555-5555-5555-555555555555","name":"Result A","formatString":"^[0-9]+$","returnValueType":"Numeric","isRange":false}],"methodItems":[{"testMethodItemId":"66666666-6666-6666-6666-666666666666","name":"Method A","formatString":"^[0-9]+$","returnValueType":"Numeric","intendedResult":"42"}],"samples":[{"sampleId":"22222222-2222-2222-2222-222222222222","sampleNumber":"S1","internalDescription":"Internal 1","isApplicable":true,"values":[{"sampleResultItemId":"77777777-7777-7777-7777-777777777777","testResultItemId":"55555555-5555-5555-5555-555555555555","intendedResult":"12","intendedResult2":""}]}]}]}
            """;
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.GetIntendedResultsAsync(Guid.Parse("11111111-1111-1111-1111-111111111111"));

        Assert.NotNull(result);
        Assert.Equal("17152A/BA", result!.DistributionReferenceFull);
        Assert.True(result.RequiresAuditReason);
        var test = Assert.Single(result.Tests);
        Assert.Equal("Somatic cell count", test.TestName);
        Assert.Equal("42", Assert.Single(test.MethodItems).IntendedResult);
        var sample = Assert.Single(test.Samples);
        Assert.True(sample.IsApplicable);
        Assert.Equal("12", Assert.Single(sample.Values).IntendedResult);
    }

    [Fact]
    public async Task SaveIntendedResultsAsync_PutsToIntendedResultsEndpointAndReturnsDeserializedResult()
    {
        var schemeId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        const string json = """{"success":false,"fieldErrorsByItemId":{"66666666-6666-6666-6666-666666666666":[{"field":"IntendedResult","message":"Input must be of format: Numeric"}]},"auditReasonRequired":false}""";
        HttpRequestMessage? captured = null;
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, json) { OnRequest = request => captured = request };
        var client = new DistributionApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });

        var result = await client.SaveIntendedResultsAsync(schemeId, new Contracts.Distribution.IntendedResultsSaveRequest([], false));

        Assert.NotNull(captured);
        Assert.Equal(HttpMethod.Put, captured!.Method);
        Assert.Equal($"/api/distributions/schemes/{schemeId}/intended-results", captured.RequestUri!.AbsolutePath);
        Assert.NotNull(result);
        Assert.False(result!.Success);
        var fieldError = Assert.Single(result.FieldErrorsByItemId[Guid.Parse("66666666-6666-6666-6666-666666666666")]);
        Assert.Equal("Input must be of format: Numeric", fieldError.Message);
    }

    [Fact]
    public async Task SaveIntendedResultsAsync_ServerError_Throws()
    {
        var client = CreateClient(HttpStatusCode.InternalServerError, null);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.SaveIntendedResultsAsync(Guid.NewGuid(), new Contracts.Distribution.IntendedResultsSaveRequest([], false)));
    }

    [Fact]
    public async Task GetParticipantsAsync_NotFound_ReturnsNull()
    {
        var client = CreateClient(HttpStatusCode.NotFound, null);

        var result = await client.GetParticipantsAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetParticipantsAsync_ServerError_Throws()
    {
        var client = CreateClient(HttpStatusCode.InternalServerError, null);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetParticipantsAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetParticipantsAsync_ReturnsDeserializedResponse()
    {
        const string json = """
            {"monthlyDistributionSchemeId":"11111111-1111-1111-1111-111111111111","yearId":2026,"monthId":4,"schemeIdentifier":"PT0060","schemeName":"Mastitis bovine","distributionReferenceFull":"17152A/BA","isRepeatDistribution":true,"participants":[{"distributionParticipantId":"22222222-2222-2222-2222-222222222222","fullName":"L001: Lab One","deadlineDate":"2026-04-05T00:00:00","deadlineChangeReason":"","additionalParticipant":false}],"additionalParticipants":[{"participantSchemeId":"33333333-3333-3333-3333-333333333333","fullName":"L002: Lab Two"}]}
            """;
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.GetParticipantsAsync(Guid.Parse("11111111-1111-1111-1111-111111111111"));

        Assert.NotNull(result);
        Assert.Equal("17152A/BA", result!.DistributionReferenceFull);
        Assert.True(result.IsRepeatDistribution);
        var participant = Assert.Single(result.Participants);
        Assert.Equal("L001: Lab One", participant.FullName);
        Assert.Equal("L002: Lab Two", Assert.Single(result.AdditionalParticipants).FullName);
    }

    [Fact]
    public async Task ChangeDeadlineAsync_PutsToDeadlineEndpointAndReturnsDeserializedResult()
    {
        var schemeId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var participantId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        const string json = """{"success":false,"newDeadlineDateError":"Deadline Date required.","reasonError":null}""";
        HttpRequestMessage? captured = null;
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, json) { OnRequest = request => captured = request };
        var client = new DistributionApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });

        var result = await client.ChangeDeadlineAsync(schemeId, participantId, new Contracts.Distribution.ChangeDeadlineRequest(null, "A reason"));

        Assert.NotNull(captured);
        Assert.Equal(HttpMethod.Put, captured!.Method);
        Assert.Equal($"/api/distributions/schemes/{schemeId}/participants/{participantId}/deadline", captured.RequestUri!.AbsolutePath);
        Assert.NotNull(result);
        Assert.False(result!.Success);
        Assert.Equal("Deadline Date required.", result.NewDeadlineDateError);
    }

    [Fact]
    public async Task ChangeDeadlineAsync_ServerError_Throws()
    {
        var client = CreateClient(HttpStatusCode.InternalServerError, null);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.ChangeDeadlineAsync(Guid.NewGuid(), Guid.NewGuid(), new Contracts.Distribution.ChangeDeadlineRequest(DateTime.UtcNow, "reason")));
    }

    [Fact]
    public async Task RepeatDistributionAsync_PostsToRepeatEndpointAndReturnsDeserializedResult()
    {
        var schemeId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        const string json = """{"success":true,"error":null}""";
        HttpRequestMessage? captured = null;
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, json) { OnRequest = request => captured = request };
        var client = new DistributionApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });

        var request = new Contracts.Distribution.RepeatDistributionRequest(
            [new Contracts.Distribution.ParticipantSelectionRequest(Guid.NewGuid(), false)]);
        var result = await client.RepeatDistributionAsync(schemeId, request);

        Assert.NotNull(captured);
        Assert.Equal(HttpMethod.Post, captured!.Method);
        Assert.Equal($"/api/distributions/schemes/{schemeId}/repeat", captured.RequestUri!.AbsolutePath);
        Assert.NotNull(result);
        Assert.True(result!.Success);
    }

    [Fact]
    public async Task RepeatDistributionAsync_ServerError_Throws()
    {
        var client = CreateClient(HttpStatusCode.InternalServerError, null);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.RepeatDistributionAsync(Guid.NewGuid(), new Contracts.Distribution.RepeatDistributionRequest([])));
    }

    [Fact]
    public async Task GetPreparationAsync_ReturnsDeserializedResponse()
    {
        const string json = """
            {"monthlyDistributionId":"11111111-1111-1111-1111-111111111111","yearId":2026,"monthId":4,"schemes":[]}
            """;
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.GetPreparationAsync(2026, 4);

        Assert.NotNull(result);
        Assert.Equal(2026, result!.YearId);
        Assert.Empty(result.Schemes);
    }

    [Fact]
    public async Task GetPreparationAsync_NotFound_ReturnsNull()
    {
        var client = CreateClient(HttpStatusCode.NotFound, null);

        var result = await client.GetPreparationAsync(2026, 4);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetPreparationAsync_ServerError_Throws()
    {
        var client = CreateClient(HttpStatusCode.InternalServerError, null);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetPreparationAsync(2026, 4));
    }

    [Fact]
    public async Task SavePreparationAsync_PutsToPreparationEndpointAndReturnsDeserializedResult()
    {
        const string json = """{"success":true}""";
        HttpRequestMessage? captured = null;
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, json) { OnRequest = request => captured = request };
        var client = new DistributionApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });

        var request = new Contracts.Distribution.PreparationSaveRequest(
            [new Contracts.Distribution.PreparationStageUpdateRequest(Guid.NewGuid(), true, false, false)]);
        var result = await client.SavePreparationAsync(2026, 4, request);

        Assert.NotNull(captured);
        Assert.Equal(HttpMethod.Put, captured!.Method);
        Assert.Equal("/api/distributions/months/2026/4/preparation", captured.RequestUri!.AbsolutePath);
        Assert.NotNull(result);
        Assert.True(result!.Success);
    }

    [Fact]
    public async Task SavePreparationAsync_NotFound_ReturnsNull()
    {
        var client = CreateClient(HttpStatusCode.NotFound, null);

        var result = await client.SavePreparationAsync(2026, 4, new Contracts.Distribution.PreparationSaveRequest([]));

        Assert.Null(result);
    }

    private static DistributionApiClient CreateCapturingClient(
        HttpStatusCode statusCode, string? json, Action<HttpRequestMessage> capture) =>
        new(new HttpClient(new StubHttpMessageHandler(statusCode, json) { OnRequest = capture }) { BaseAddress = new Uri("http://localhost") });

    private static DistributionApiClient CreateClient(Func<HttpResponseMessage> responseFactory) =>
        new(new HttpClient(new StubHttpMessageHandler(responseFactory)) { BaseAddress = new Uri("http://localhost") });

    [Fact]
    public async Task GetPackagingAsync_GetsThePackagingEndpointWithModeAndWeekAndReturnsTheSheet()
    {
        const string json = """
            {"mode":1,"week":3,"yearId":2026,"monthId":5,"blocks":[{"printKey":"abc_20260513","date":"2026-05-13T00:00:00","heading":"L001: Lab One","packingInfo":"","dueToBePosted":false,"showAllPacked":true,"showAllPosted":false,"schemes":[]}]}
            """;
        HttpRequestMessage? captured = null;
        var client = CreateCapturingClient(HttpStatusCode.OK, json, request => captured = request);

        var result = await client.GetPackagingAsync(2026, 5, 1, 3);

        Assert.NotNull(captured);
        Assert.Equal(HttpMethod.Get, captured!.Method);
        Assert.Equal("/api/distributions/months/2026/5/packaging", captured.RequestUri!.AbsolutePath);
        Assert.Equal("?mode=1&week=3", captured.RequestUri.Query);
        Assert.NotNull(result);
        Assert.Equal(1, result!.Mode);
        Assert.Equal(3, result.Week);
        var block = Assert.Single(result.Blocks);
        Assert.Equal("abc_20260513", block.PrintKey);
        Assert.True(block.ShowAllPacked);
    }

    [Fact]
    public async Task GetPackagingAsync_NotFound_ReturnsNull()
    {
        var client = CreateClient(HttpStatusCode.NotFound, null);

        Assert.Null(await client.GetPackagingAsync(2026, 5, 0, 1));
    }

    [Fact]
    public async Task GetPackagingAsync_ServerError_Throws()
    {
        var client = CreateClient(HttpStatusCode.InternalServerError, null);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetPackagingAsync(2026, 5, 0, 1));
    }

    [Fact]
    public async Task SavePackagingAsync_PutsTheUpdatesAndReturnsTheResult()
    {
        HttpRequestMessage? captured = null;
        string? body = null;
        var client = CreateCapturingClient(HttpStatusCode.OK, """{"success":true}""", request =>
        {
            captured = request;
            body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
        });
        var distributionParticipantId = Guid.NewGuid();

        var result = await client.SavePackagingAsync(
            2026, 5, new Contracts.Distribution.PackagingSaveRequest(
                [new Contracts.Distribution.PackagingStageUpdateRequest(1, 2, distributionParticipantId, true, false)]));

        Assert.NotNull(captured);
        Assert.Equal(HttpMethod.Put, captured!.Method);
        Assert.Equal("/api/distributions/months/2026/5/packaging", captured.RequestUri!.AbsolutePath);
        Assert.Contains(distributionParticipantId.ToString(), body);
        Assert.NotNull(result);
        Assert.True(result!.Success);
    }

    [Fact]
    public async Task SavePackagingAsync_NotFound_ReturnsNull()
    {
        var client = CreateClient(HttpStatusCode.NotFound, null);

        Assert.Null(await client.SavePackagingAsync(2026, 5, new Contracts.Distribution.PackagingSaveRequest([])));
    }

    [Fact]
    public async Task SavePackagingAsync_ServerError_Throws()
    {
        var client = CreateClient(HttpStatusCode.InternalServerError, null);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.SavePackagingAsync(2026, 5, new Contracts.Distribution.PackagingSaveRequest([])));
    }

    [Fact]
    public async Task GetPackagingLabelsAsync_PostsTheSelectionAndReturnsTheLabels()
    {
        const string json = """{"labels":[{"lines":["A Contact","An Org"]}]}""";
        HttpRequestMessage? captured = null;
        string? body = null;
        var client = CreateCapturingClient(HttpStatusCode.OK, json, request =>
        {
            captured = request;
            body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
        });

        var result = await client.GetPackagingLabelsAsync(
            2026, 5, new Contracts.Distribution.PackagingSelectionRequest(0, 2, ["key1"]));

        Assert.NotNull(captured);
        Assert.Equal(HttpMethod.Post, captured!.Method);
        Assert.Equal("/api/distributions/months/2026/5/packaging/labels", captured.RequestUri!.AbsolutePath);
        Assert.Contains("key1", body);
        Assert.NotNull(result);
        Assert.Equal(["A Contact", "An Org"], Assert.Single(result!.Labels).Lines);
    }

    [Fact]
    public async Task GetPackagingLabelsAsync_NotFound_ReturnsNull()
    {
        var client = CreateClient(HttpStatusCode.NotFound, null);

        Assert.Null(await client.GetPackagingLabelsAsync(2026, 5, new Contracts.Distribution.PackagingSelectionRequest(0, 1, [])));
    }

    [Fact]
    public async Task GetPackagingLabelsAsync_ServerError_Throws()
    {
        var client = CreateClient(HttpStatusCode.InternalServerError, null);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.GetPackagingLabelsAsync(2026, 5, new Contracts.Distribution.PackagingSelectionRequest(0, 1, [])));
    }

    private static HttpResponseMessage DocxResponse(Action<System.Net.Http.Headers.HttpContentHeaders>? configureHeaders)
    {
        var content = new ByteArrayContent([1, 2, 3]);
        configureHeaders?.Invoke(content.Headers);
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    }

    [Fact]
    public async Task GetPackagingCustomsAsync_PostsTheSelectionAndReturnsTheDocumentWithItsEncodedFileName()
    {
        HttpRequestMessage? captured = null;
        var handler = new StubHttpMessageHandler(() => DocxResponse(headers =>
            headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment")
            {
                FileNameStar = "Daily packaging May 2026.docx",
            }))
        { OnRequest = request => captured = request };
        var client = new DistributionApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });

        var result = await client.GetPackagingCustomsAsync(
            2026, 5, new Contracts.Distribution.PackagingSelectionRequest(2, 0, ["key1"]));

        Assert.NotNull(captured);
        Assert.Equal(HttpMethod.Post, captured!.Method);
        Assert.Equal("/api/distributions/months/2026/5/packaging/customs", captured.RequestUri!.AbsolutePath);
        Assert.Equal([1, 2, 3], result.Content);
        Assert.Equal("Daily packaging May 2026.docx", result.FileName);
        Assert.Null(result.Error);
    }

    [Theory]
    [InlineData("\"Combined packaging May 2026 Week 1.docx\"")]
    [InlineData("Combined packaging May 2026 Week 1.docx")]
    public async Task GetPackagingCustomsAsync_PlainFileNameHasItsQuotesTrimmed(string headerFileName)
    {
        var client = CreateClient(() => DocxResponse(headers =>
            headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment") { FileName = headerFileName }));

        var result = await client.GetPackagingCustomsAsync(2026, 5, new Contracts.Distribution.PackagingSelectionRequest(1, 1, []));

        Assert.Equal("Combined packaging May 2026 Week 1.docx", result.FileName);
        Assert.Equal([1, 2, 3], result.Content);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task GetPackagingCustomsAsync_NoContentDisposition_FallsBackToCustomsDocx()
    {
        var client = CreateClient(() => DocxResponse(null));

        var result = await client.GetPackagingCustomsAsync(2026, 5, new Contracts.Distribution.PackagingSelectionRequest(1, 1, []));

        Assert.Equal("Customs.docx", result.FileName);
        Assert.Equal([1, 2, 3], result.Content);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task GetPackagingCustomsAsync_ContentDispositionWithoutAFileName_FallsBackToCustomsDocx()
    {
        var client = CreateClient(() => DocxResponse(headers =>
            headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment")));

        var result = await client.GetPackagingCustomsAsync(2026, 5, new Contracts.Distribution.PackagingSelectionRequest(1, 1, []));

        Assert.Equal("Customs.docx", result.FileName);
    }

    [Fact]
    public async Task GetPackagingCustomsAsync_BadRequestWithDetail_ReturnsTheBusinessMessageAsTheError()
    {
        var client = CreateClient(HttpStatusCode.BadRequest, """{"title":"Customs","status":400,"detail":"Please select schemes to print"}""");

        var result = await client.GetPackagingCustomsAsync(2026, 5, new Contracts.Distribution.PackagingSelectionRequest(2, 0, []));

        Assert.Null(result.Content);
        Assert.Equal(string.Empty, result.FileName);
        Assert.Equal("Please select schemes to print", result.Error);
    }

    [Theory]
    [InlineData("""{"status":400}""")]
    [InlineData("null")]
    public async Task GetPackagingCustomsAsync_BadRequestWithoutDetail_ReturnsTheDefaultMailMergeMessage(string json)
    {
        var client = CreateClient(HttpStatusCode.BadRequest, json);

        var result = await client.GetPackagingCustomsAsync(2026, 5, new Contracts.Distribution.PackagingSelectionRequest(2, 0, []));

        Assert.Null(result.Content);
        Assert.Equal("There was a problem with the Mail Merge", result.Error);
    }

    [Fact]
    public async Task GetPackagingCustomsAsync_OtherFailure_Throws()
    {
        var client = CreateClient(HttpStatusCode.InternalServerError, null);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.GetPackagingCustomsAsync(2026, 5, new Contracts.Distribution.PackagingSelectionRequest(2, 0, [])));
    }

    [Fact]
    public async Task GetPackagingCustomsAsync_NotFound_Throws()
    {
        var client = CreateClient(HttpStatusCode.NotFound, null);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.GetPackagingCustomsAsync(2026, 5, new Contracts.Distribution.PackagingSelectionRequest(2, 0, [])));
    }
}
