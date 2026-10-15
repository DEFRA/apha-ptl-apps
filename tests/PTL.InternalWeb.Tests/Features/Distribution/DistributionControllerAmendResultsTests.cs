using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using PTL.Contracts.Distribution;
using PTL.InternalWeb.Features.Distribution;
using PTL.InternalWeb.Tests.TestSupport;
using Keys = PTL.Contracts.Distribution.AmendResultsFieldKeys;

namespace PTL.InternalWeb.Tests.Features.Distribution;

// Amend Results - legacy Scheduling/AmendResults.aspx (+ Audit.aspx for the reason step).
public class DistributionControllerAmendResultsTests
{
    private static readonly Guid SchemeId = Guid.Parse("aaaaaaaa-0000-0000-0000-0000000000e1");
    private static readonly Guid TestId = Guid.Parse("aaaaaaaa-0000-0000-0000-0000000000e2");
    private static readonly Guid MethodItem = Guid.Parse("aaaaaaaa-0000-0000-0000-0000000000e3");
    private static readonly Guid ResultItem = Guid.Parse("aaaaaaaa-0000-0000-0000-0000000000e4");
    private static readonly Guid CategoryItem = Guid.Parse("aaaaaaaa-0000-0000-0000-0000000000e5");
    private static readonly Guid Sample1 = Guid.Parse("aaaaaaaa-0000-0000-0000-0000000000e6");
    private static readonly Guid Sample2 = Guid.Parse("aaaaaaaa-0000-0000-0000-0000000000e7");
    private static readonly Guid WithResults = Guid.Parse("dddddddd-0000-0000-0000-0000000000e1");
    private static readonly Guid NilReturn = Guid.Parse("dddddddd-0000-0000-0000-0000000000e2");
    private static readonly Guid RatingA = Guid.Parse("bbbbbbbb-0000-0000-0000-0000000000e1");
    private static readonly Guid RatingB = Guid.Parse("bbbbbbbb-0000-0000-0000-0000000000e2");

    private const string AuditObject = "Results for Distribution D1a/S1";
    private const string StoredNilReason = "Not received";

    internal static AmendResultsResponse Page(bool rangeStored = false) =>
        new(
            SchemeId, "D1a/S1 BT: Brucella", RequiresAssessment: true, StoreRatings: true,
            [
                new AmendTestResponse(
                    TestId, "Brucella", IsAssessmentSubmitted: true,
                    [new AmendItemResponse(MethodItem, "Method", @"^\d+$", "Number", false)],
                    [new AmendItemResponse(ResultItem, "Result", "Pos|Neg", "", rangeStored)],
                    [new AmendCategoryItemResponse(CategoryItem, "Accuracy")],
                    [new AmendSampleResponse(Sample1, "S1-001"), new AmendSampleResponse(Sample2, "S1-002")],
                    [new AmendIntendedValueResponse(MethodItem, Guid.Empty, "10", "")],
                    [new AmendIntendedValueResponse(ResultItem, Sample1, "Pos", ""), new AmendIntendedValueResponse(ResultItem, Sample2, "Neg", "")]),
            ],
            [
                new AmendParticipantResponse(
                    WithResults, "L001", "", HasResults: true, HasRatingResults: true,
                    [new AmendParticipantValueResponse(TestId, MethodItem, Guid.Empty, "12")],
                    [new AmendParticipantValueResponse(TestId, ResultItem, Sample1, "Pos"), new AmendParticipantValueResponse(TestId, ResultItem, Sample2, "Neg")],
                    [new AmendParticipantScoreResponse(TestId, CategoryItem, Sample1, 3), new AmendParticipantScoreResponse(TestId, CategoryItem, Sample2, 4)],
                    [new AmendAssessmentResponse(TestId, Sample1, "Good", 5)],
                    [new AmendRatingResponse(Sample1, RatingA)]),
                new AmendParticipantResponse(NilReturn, "L002", StoredNilReason, HasResults: false, HasRatingResults: false, [], [], [], [], []),
            ],
            [new AmendRatingOptionResponse(RatingA, "Satisfactory"), new AmendRatingOptionResponse(RatingB, "Unsatisfactory")],
            [new AmendSampleResponse(Sample1, "S1-001")],
            [new AmendSampleStatisticsResponse(Sample1, "4.50", "3", "10", "75%")]);

    private static AmendResultsSaveResult Saved() => new(true, false, string.Empty, new Dictionary<string, string>());

    private static AmendResultsSaveResult AuditRequired() => new(false, true, AuditObject, new Dictionary<string, string>());

    private static FakeDistributionApiClient ApiClient(AmendResultsSaveResult? save = null, AmendResultsResponse? page = null) =>
        new() { AmendResults = page ?? Page(), AmendResultsSaveResult = save };

    private static DistributionController CreateController(FakeDistributionApiClient apiClient, params (string Key, string[] Values)[] form)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.ContentType = "application/x-www-form-urlencoded";
        httpContext.Request.Form = new FormCollection(form.ToDictionary(f => f.Key, f => new StringValues(f.Values)));
        return new(apiClient) { ControllerContext = new ControllerContext { HttpContext = httpContext } };
    }

    private static (string, string[]) Field(string key, params string[] values) => (key, values);

    private static Task<IActionResult> PostAsync(
        DistributionController controller, string? command, bool isAuditStep = false, string? pendingCommand = null, string? auditWhy = null) =>
        controller.AmendResults(2026, 5, SchemeId, command, isAuditStep, pendingCommand, isAuditStep ? AuditObject : null, auditWhy, CancellationToken.None);

    private static async Task<AmendResultsSaveRequest> SavedRequestAsync(params (string Key, string[] Values)[] form)
    {
        var apiClient = ApiClient(Saved());
        await PostAsync(CreateController(apiClient, form), "save");
        return Assert.Single(apiClient.SavedAmendResults);
    }

    private static void AssertRedirectToTabulations(IActionResult result)
    {
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Tabulations", redirect.ActionName);
        Assert.Null(redirect.ControllerName);
        Assert.Equal(2026, redirect.RouteValues!["year"]);
        Assert.Equal(5, redirect.RouteValues["monthId"]);
        Assert.False(redirect.RouteValues.ContainsKey("schemeId"));
    }

    private static AmendResultsViewModel AssertRedisplayed(IActionResult result)
    {
        var view = Assert.IsType<ViewResult>(result);
        Assert.Null(view.ViewName);
        return Assert.IsType<AmendResultsViewModel>(view.Model);
    }

    // ---- GET ----

    [Fact]
    public async Task Get_ApiReturnsNull_ReturnsNotFound()
    {
        var result = await CreateController(new FakeDistributionApiClient()).AmendResults(2026, 5, SchemeId, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Get_ReturnsTheViewWithThePageAndRouteValues()
    {
        var page = Page();

        var result = await CreateController(ApiClient(page: page)).AmendResults(2026, 5, SchemeId, CancellationToken.None);

        var model = AssertRedisplayed(result);
        Assert.Equal((2026, 5, SchemeId), (model.YearId, model.MonthId, model.MonthlyDistributionSchemeId));
        Assert.Same(page, model.Page);
        Assert.Empty(model.Values);
        Assert.False(model.IsAuditStep);
        Assert.Null(model.PendingCommand);
    }

    // ---- POST: cancel ----

    [Theory]
    [InlineData("cancel")]
    [InlineData("CANCEL")]
    public async Task Post_Cancel_ReturnsToTabulationsWithoutSaving(string command)
    {
        var apiClient = ApiClient(Saved());

        var result = await PostAsync(CreateController(apiClient), command);

        AssertRedirectToTabulations(result);
        Assert.Empty(apiClient.SavedAmendResults);
    }

    // No Apply any more: cancelling the audit step always leaves for Tabulations.
    [Theory]
    [InlineData("save")]
    [InlineData(null)]
    [InlineData("apply")]
    public async Task Post_CancelAudit_ReturnsToTabulationsWithoutSaving(string? pendingCommand)
    {
        var apiClient = ApiClient(Saved());

        var result = await PostAsync(CreateController(apiClient), "CancelAudit", isAuditStep: true, pendingCommand: pendingCommand);

        AssertRedirectToTabulations(result);
        Assert.Empty(apiClient.SavedAmendResults);
    }

    [Fact]
    public async Task Post_PageMissing_ReturnsNotFoundWithoutSaving()
    {
        var apiClient = new FakeDistributionApiClient { AmendResultsSaveResult = Saved() };

        var result = await PostAsync(CreateController(apiClient), "save");

        Assert.IsType<NotFoundResult>(result);
        Assert.Empty(apiClient.SavedAmendResults);
    }

    // ---- POST: audit reason ----

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Post_AuditStepWithoutAReason_RedisplaysTheAuditStepWithAnErrorAndDoesNotSave(string? auditWhy)
    {
        var apiClient = ApiClient(Saved());
        var controller = CreateController(apiClient, Field(Keys.IntendedMethod(MethodItem), "11"));

        var result = await PostAsync(controller, "apply", isAuditStep: true, pendingCommand: "apply", auditWhy: auditWhy);

        var model = AssertRedisplayed(result);
        Assert.True(model.IsAuditStep);
        Assert.Equal(("apply", AuditObject), (model.PendingCommand, model.AuditObjectName));
        Assert.Equal("11", model.Values[Keys.IntendedMethod(MethodItem)]);
        Assert.Equal("The reason for these changes must be specified", Assert.Single(controller.ModelState["AuditWhy"]!.Errors).ErrorMessage);
        Assert.Empty(apiClient.SavedAmendResults);
    }

    [Fact]
    public async Task Post_AuditStepWithAReason_SendsTheReason()
    {
        var apiClient = ApiClient(Saved());

        await PostAsync(CreateController(apiClient), "save", isAuditStep: true, pendingCommand: "save", auditWhy: "Typo corrected");

        Assert.Equal("Typo corrected", Assert.Single(apiClient.SavedAmendResults).AuditWhy);
    }

    [Fact]
    public async Task Post_NotAnAuditStep_NeverSendsAReason()
    {
        var apiClient = ApiClient(Saved());

        await PostAsync(CreateController(apiClient), "save", auditWhy: "stale");

        Assert.Null(Assert.Single(apiClient.SavedAmendResults).AuditWhy);
    }

    // ---- POST: request mapping ----

    [Fact]
    public async Task Post_MapsEveryPostedInputToTheSaveRequest()
    {
        var request = await SavedRequestAsync(
            Field(Keys.IntendedMethod(MethodItem), "11"),
            Field(Keys.IntendedResult(Sample1, ResultItem, 1), "Neg"),
            Field(Keys.IntendedResult(Sample1, ResultItem, 2), "Pos"),
            Field(Keys.Range(ResultItem), "true"),
            Field(Keys.Range(ResultItem) + "_shown", "1"),
            Field(Keys.ParticipantMethod(WithResults, MethodItem), "13"),
            Field(Keys.ParticipantResult(WithResults, Sample2, ResultItem), "Pos"),
            Field(Keys.CategoryScore(WithResults, Sample1, CategoryItem), "1"),
            Field(Keys.AssessmentComment(WithResults, TestId, Sample1), "Better"),
            Field(Keys.AssessmentScore(WithResults, TestId, Sample1), "2"),
            Field(Keys.Rating(WithResults, Sample1), RatingB.ToString()),
            Field(Keys.NilReturn(NilReturn), "Lost in post"));

        Assert.Equal([new AmendIntendedValueRequest(MethodItem, Guid.Empty, "11", null)], request.IntendedMethodResults);
        Assert.Equal([new AmendIntendedValueRequest(ResultItem, Sample1, "Neg", "Pos")], request.IntendedResultItemResults);
        Assert.Equal([new AmendRangeRequest(ResultItem, true)], request.Ranges);
        Assert.Equal([new AmendParticipantValueRequest(WithResults, MethodItem, Guid.Empty, "13")], request.MethodResults);
        Assert.Equal([new AmendParticipantValueRequest(WithResults, ResultItem, Sample2, "Pos")], request.ResultItemResults);
        Assert.Equal(
            [new AmendParticipantScoreRequest(WithResults, CategoryItem, Sample1, 1), new AmendParticipantScoreRequest(WithResults, CategoryItem, Sample2, 4)],
            request.CategoryResults);
        Assert.Equal([new AmendAssessmentRequest(WithResults, TestId, Sample1, "Better", 2)], request.Assessments);
        Assert.Equal([new AmendNilReturnRequest(NilReturn, "Lost in post")], request.NilReturns);
        Assert.Equal([new AmendRatingRequest(WithResults, Sample1, RatingB)], request.Ratings);
        Assert.Null(request.AuditWhy);
    }

    [Fact]
    public async Task Post_IntendedResultWithoutASecondValue_SendsANullValue2()
    {
        var request = await SavedRequestAsync(Field(Keys.IntendedResult(Sample2, ResultItem, 1), "Pos"));

        Assert.Equal([new AmendIntendedValueRequest(ResultItem, Sample2, "Pos", null)], request.IntendedResultItemResults);
    }

    [Fact]
    public async Task Post_NothingPosted_OmitsEveryInputAndKeepsTheStoredCategoryScores()
    {
        var request = await SavedRequestAsync(Field("__RequestVerificationToken", "token"), Field("unrelated", "x"));

        Assert.Empty(request.IntendedMethodResults);
        Assert.Empty(request.IntendedResultItemResults);
        Assert.Empty(request.Ranges);
        Assert.Empty(request.MethodResults);
        Assert.Empty(request.ResultItemResults);
        Assert.Empty(request.Assessments);
        Assert.Empty(request.NilReturns);
        Assert.Empty(request.Ratings);
        Assert.Equal(
            [new AmendParticipantScoreRequest(WithResults, CategoryItem, Sample1, 3), new AmendParticipantScoreRequest(WithResults, CategoryItem, Sample2, 4)],
            request.CategoryResults);
    }

    [Fact]
    public async Task Post_IntendedSecondValueAlone_IsNotSent()
    {
        var request = await SavedRequestAsync(Field(Keys.IntendedResult(Sample1, ResultItem, 2), "Pos"));

        Assert.Empty(request.IntendedResultItemResults);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Post_RangeShownButUnticked_IsSentAsNotARange(bool rangeStored)
    {
        var apiClient = ApiClient(Saved(), Page(rangeStored));

        await PostAsync(CreateController(apiClient, Field(Keys.Range(ResultItem) + "_shown", "1")), "save");

        Assert.Equal([new AmendRangeRequest(ResultItem, false)], Assert.Single(apiClient.SavedAmendResults).Ranges);
    }

    [Fact]
    public async Task Post_RangeTickedWithoutTheShownMarker_IsNotSent()
    {
        var request = await SavedRequestAsync(Field(Keys.Range(ResultItem), "true"));

        Assert.Empty(request.Ranges);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("2.5")]
    public async Task Post_UnparseableScores_FallBackToTheStoredScores(string posted)
    {
        var request = await SavedRequestAsync(
            Field(Keys.CategoryScore(WithResults, Sample2, CategoryItem), posted),
            Field(Keys.AssessmentComment(WithResults, TestId, Sample1), "Good"),
            Field(Keys.AssessmentScore(WithResults, TestId, Sample1), posted));

        Assert.Equal(4, request.CategoryResults.Single(c => c.SampleId == Sample2).Score);
        Assert.Equal([new AmendAssessmentRequest(WithResults, TestId, Sample1, "Good", 5)], request.Assessments);
    }

    [Fact]
    public async Task Post_AssessmentScoreWithoutAComment_IsNotSent()
    {
        var request = await SavedRequestAsync(Field(Keys.AssessmentScore(WithResults, TestId, Sample1), "1"));

        Assert.Empty(request.Assessments);
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("")]
    public async Task Post_UnparseableRating_IsIgnored(string posted)
    {
        var request = await SavedRequestAsync(Field(Keys.Rating(WithResults, Sample1), posted));

        Assert.Empty(request.Ratings);
    }

    [Fact]
    public async Task Post_RepeatedNilReturnBoxes_TheOneThatDiffersFromTheStoredReasonWins()
    {
        var request = await SavedRequestAsync(Field(Keys.NilReturn(NilReturn), StoredNilReason, "Lab closed", StoredNilReason));

        Assert.Equal([new AmendNilReturnRequest(NilReturn, "Lab closed")], request.NilReturns);
    }

    [Fact]
    public async Task Post_RepeatedNilReturnBoxesAllUnchanged_SendTheStoredReason()
    {
        var request = await SavedRequestAsync(Field(Keys.NilReturn(NilReturn), StoredNilReason, StoredNilReason));

        Assert.Equal([new AmendNilReturnRequest(NilReturn, StoredNilReason)], request.NilReturns);
    }

    [Fact]
    public async Task Post_NilReturnClearedOnOneBox_SendsTheEmptyReason()
    {
        var request = await SavedRequestAsync(Field(Keys.NilReturn(NilReturn), StoredNilReason, ""));

        Assert.Equal([new AmendNilReturnRequest(NilReturn, "")], request.NilReturns);
    }

    [Fact]
    public async Task Post_RedisplayKeepsOnlyTheAmendInputsUsingTheLastPostedValue()
    {
        var apiClient = ApiClient(AuditRequired());
        var controller = CreateController(apiClient,
            Field("__RequestVerificationToken", "token"),
            Field("command", "save"),
            Field(Keys.ParticipantMethod(WithResults, MethodItem), "13", "14"));

        var model = AssertRedisplayed(await PostAsync(controller, "save"));

        Assert.Equal([Keys.ParticipantMethod(WithResults, MethodItem)], model.Values.Keys);
        Assert.Equal("14", model.Values[Keys.ParticipantMethod(WithResults, MethodItem)]);
    }

    // ---- POST: results ----

    [Fact]
    public async Task Post_SaveReturnsNull_ReturnsNotFound()
    {
        var apiClient = ApiClient();

        var result = await PostAsync(CreateController(apiClient), "save");

        Assert.IsType<NotFoundResult>(result);
        Assert.Single(apiClient.SavedAmendResults);
    }

    [Theory]
    [InlineData("save")]
    [InlineData("apply")]
    public async Task Post_AuditReasonRequired_SwitchesToTheAuditStepKeepingTheCommand(string command)
    {
        var apiClient = ApiClient(AuditRequired());
        var controller = CreateController(apiClient, Field(Keys.IntendedMethod(MethodItem), "11"));

        var result = await PostAsync(controller, command);

        var model = AssertRedisplayed(result);
        Assert.True(model.IsAuditStep);
        Assert.Equal(AuditObject, model.AuditObjectName);
        Assert.Equal(command, model.PendingCommand);
        Assert.Equal((2026, 5, SchemeId), (model.YearId, model.MonthId, model.MonthlyDistributionSchemeId));
        Assert.Equal("11", model.Values[Keys.IntendedMethod(MethodItem)]);
        Assert.True(controller.ModelState.IsValid);
    }

    [Fact]
    public async Task Post_AuditStepStillNeedingAReason_KeepsThePendingCommand()
    {
        var apiClient = ApiClient(AuditRequired());

        var result = await PostAsync(CreateController(apiClient), "save", isAuditStep: true, pendingCommand: "apply", auditWhy: "Because");

        var model = AssertRedisplayed(result);
        Assert.True(model.IsAuditStep);
        Assert.Equal("apply", model.PendingCommand);
    }

    [Fact]
    public async Task Post_FieldErrors_AreAddedToModelStateUnderTheSameKeys()
    {
        var methodKey = Keys.IntendedMethod(MethodItem);
        var nilKey = Keys.NilReturn(NilReturn);
        var apiClient = ApiClient(new AmendResultsSaveResult(false, false, string.Empty, new Dictionary<string, string>
        {
            [methodKey] = "Input must be of format: Number",
            [nilKey] = "Nil return reason is too long",
        }));
        var controller = CreateController(apiClient, Field(methodKey, "abc"));

        var result = await PostAsync(controller, "apply");

        var model = AssertRedisplayed(result);
        Assert.False(model.IsAuditStep);
        Assert.Equal("abc", model.Values[methodKey]);
        Assert.Equal(2, controller.ModelState.ErrorCount);
        Assert.Equal("Input must be of format: Number", Assert.Single(controller.ModelState[methodKey]!.Errors).ErrorMessage);
        Assert.Equal("Nil return reason is too long", Assert.Single(controller.ModelState[nilKey]!.Errors).ErrorMessage);
    }

    [Theory]
    [InlineData("save")]
    [InlineData("SAVE")]
    [InlineData(null)]
    public async Task Post_SavedWithSave_ReturnsToTabulations(string? command)
    {
        var apiClient = ApiClient(Saved());

        AssertRedirectToTabulations(await PostAsync(CreateController(apiClient), command));
        Assert.Single(apiClient.SavedAmendResults);
    }

    // A stale "apply" post is treated as Save.
    [Theory]
    [InlineData("apply")]
    [InlineData("APPLY")]
    public async Task Post_SavedWithAStaleApplyCommand_ReturnsToTabulations(string command)
    {
        var apiClient = ApiClient(Saved());

        AssertRedirectToTabulations(await PostAsync(CreateController(apiClient), command));
        Assert.Single(apiClient.SavedAmendResults);
    }

    [Theory]
    [InlineData("save", "save")]
    [InlineData("save", "apply")]
    [InlineData("apply", "save")]
    public async Task Post_AuditStepSaved_ReturnsToTabulationsAndSendsTheReason(string button, string pendingCommand)
    {
        var apiClient = ApiClient(Saved());

        var result = await PostAsync(CreateController(apiClient), button, isAuditStep: true, pendingCommand: pendingCommand, auditWhy: "Because");

        AssertRedirectToTabulations(result);
        Assert.Equal("Because", Assert.Single(apiClient.SavedAmendResults).AuditWhy);
    }
}
