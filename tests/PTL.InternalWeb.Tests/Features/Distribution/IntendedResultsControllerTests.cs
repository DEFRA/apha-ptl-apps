using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using PTL.ApiClient.Distribution;
using PTL.Contracts.Distribution;
using PTL.InternalWeb.Features.Distribution;
using PTL.InternalWeb.Tests.TestSupport;
using PTL.SharedUI.Notifications;

namespace PTL.InternalWeb.Tests.Features.Distribution;

// Intended Results screen - legacy Scheduling/IntendedResults.aspx. Covers the GET mapping, the
// two legacy buttons (Save, Cancel) and the rehydration that stands in for legacy's Session-held
// IntendedResultsSet on a failed post.
public class IntendedResultsControllerTests
{
    private static DistributionController CreateController(IDistributionApiClient apiClient) =>
        new(apiClient)
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), new FakeTempDataProvider())
        };

    private static readonly Guid SchemeId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SampleId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid TestId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid MethodItemId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid ResultItemId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid SampleResultItemId = Guid.Parse("77777777-7777-7777-7777-777777777777");

    private static IntendedResultsResponse Response(bool requiresAuditReason = false, bool sampleIsApplicable = true) =>
        new(
            SchemeId,
            "17152A/BA",
            "PT0060",
            "Mastitis bovine",
            requiresAuditReason,
            [
                new IntendedResultsTestResponse(
                    TestId,
                    "Somatic cell count",
                    [new IntendedResultsResultItemResponse(ResultItemId, "Result A", "^[0-9]+$", "Numeric", false)],
                    [new IntendedResultsMethodItemResponse(MethodItemId, "Method A", "^[0-9]+$", "Numeric", "Server value")],
                    [
                        new IntendedResultsSampleRowResponse(
                            SampleId, "S1", "Internal 1", sampleIsApplicable,
                            [new IntendedResultsSampleResultValueResponse(SampleResultItemId, ResultItemId, "Server result", "")]),
                    ]),
            ]);

    private static IntendedResultsViewModel PostedModel(string methodValue = "User typed", string sampleValue = "User typed value", bool sampleIsApplicable = true) => new()
    {
        Tests =
        [
            new IntendedResultsTestViewModel
            {
                TestId = TestId,
                ResultItems = [new IntendedResultsResultItemViewModel { TestResultItemId = ResultItemId, IsRange = false }],
                MethodItems = [new IntendedResultsMethodItemViewModel { TestMethodItemId = MethodItemId, IntendedResult = methodValue }],
                Samples =
                [
                    new IntendedResultsSampleRowViewModel
                    {
                        SampleId = SampleId,
                        IsApplicable = sampleIsApplicable,
                        Values = [new IntendedResultsSampleValueViewModel { SampleResultItemId = SampleResultItemId, TestResultItemId = ResultItemId, IntendedResult = sampleValue }],
                    },
                ],
            },
        ],
    };

    [Fact]
    public async Task IntendedResults_Get_UnknownScheme_RedirectsToSchedule()
    {
        var controller = CreateController(new FakeDistributionApiClient());

        var result = await controller.IntendedResults(2026, 4, SchemeId, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(DistributionController.Schedule), redirect.ActionName);
        Assert.Equal(2026, redirect.RouteValues!["year"]);
        Assert.Equal(4, redirect.RouteValues["monthId"]);
    }

    [Fact]
    public async Task IntendedResults_Get_MapsResponseOntoViewModel()
    {
        var apiClient = new FakeDistributionApiClient { IntendedResults = Response() };
        var controller = CreateController(apiClient);

        var result = await controller.IntendedResults(2026, 4, SchemeId, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<IntendedResultsViewModel>(view.Model);
        Assert.Equal(SchemeId, model.MonthlyDistributionSchemeId);
        Assert.Equal(2026, model.YearId);
        Assert.Equal(4, model.MonthId);
        Assert.Equal("17152A/BA PT0060: Mastitis bovine", model.DistributionHeading);
        Assert.False(model.RequiresAuditReason);
        Assert.False(model.IsAuditStep);

        var test = Assert.Single(model.Tests);
        Assert.Equal("Somatic cell count", test.TestName);
        var resultItem = Assert.Single(test.ResultItems);
        Assert.Equal(ResultItemId, resultItem.TestResultItemId);
        var methodItem = Assert.Single(test.MethodItems);
        Assert.Equal("Server value", methodItem.IntendedResult);
        var sample = Assert.Single(test.Samples);
        Assert.Equal("S1", sample.SampleNumber);
        Assert.True(sample.IsApplicable);
        Assert.Equal("Server result", Assert.Single(sample.Values).IntendedResult);
    }

    // Legacy's RequireAudit is a property of the STORED data (true the moment any result was ever
    // entered), not of the current request - a plain GET must never jump straight to the audit
    // step, only an actual Save attempt (legacy ButtonSave_Click) does that.
    [Fact]
    public async Task IntendedResults_Get_DistributionAlreadyHasResults_StillRendersTheMainFormNotTheAuditStep()
    {
        var apiClient = new FakeDistributionApiClient { IntendedResults = Response(requiresAuditReason: true) };
        var controller = CreateController(apiClient);

        var result = await controller.IntendedResults(2026, 4, SchemeId, CancellationToken.None);

        var model = Assert.IsType<IntendedResultsViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.True(model.RequiresAuditReason);
        Assert.False(model.IsAuditStep);
    }

    [Fact]
    public async Task IntendedResults_Post_CancelCommand_RedirectsToScheduleWithoutSaving()
    {
        var apiClient = new FakeDistributionApiClient();
        var controller = CreateController(apiClient);

        var result = await controller.IntendedResults(2026, 4, SchemeId, PostedModel(), "cancel", CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(DistributionController.Schedule), redirect.ActionName);
        Assert.Empty(apiClient.SavedIntendedResults);
    }

    // Legacy Audit.aspx's Cancel on this screen always returns to Scheduling - there is only one
    // destination, unlike Sample Numbers.
    [Fact]
    public async Task IntendedResults_Post_CancelAuditCommand_RedirectsToScheduleWithoutSaving()
    {
        var apiClient = new FakeDistributionApiClient();
        var controller = CreateController(apiClient);

        var result = await controller.IntendedResults(2026, 4, SchemeId, PostedModel(), "cancelAudit", CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(DistributionController.Schedule), redirect.ActionName);
        Assert.Empty(apiClient.SavedIntendedResults);
    }

    [Fact]
    public async Task IntendedResults_Post_SaveCommand_SavesAndRedirectsToSchedule()
    {
        var apiClient = new FakeDistributionApiClient
        {
            IntendedResults = Response(),
            IntendedResultsSaveResult = new IntendedResultsSaveResult(true, new Dictionary<Guid, IntendedResultsFieldError[]>()),
        };
        var controller = CreateController(apiClient);

        var result = await controller.IntendedResults(2026, 4, SchemeId, PostedModel(), "save", CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(DistributionController.Schedule), redirect.ActionName);
        Assert.Equal(2026, redirect.RouteValues!["year"]);
        Assert.Equal(4, redirect.RouteValues["monthId"]);

        var saved = Assert.Single(apiClient.SavedIntendedResults);
        var test = Assert.Single(saved.Tests);
        Assert.Equal("User typed", Assert.Single(test.MethodItems).IntendedResult);
        Assert.Equal("User typed value", Assert.Single(Assert.Single(test.Samples).Values).IntendedResult);
    }

    [Fact]
    public async Task IntendedResults_Post_SaveSucceeds_ShowsSuccessNotification()
    {
        var apiClient = new FakeDistributionApiClient
        {
            IntendedResults = Response(),
            IntendedResultsSaveResult = new IntendedResultsSaveResult(true, new Dictionary<Guid, IntendedResultsFieldError[]>()),
        };
        var controller = CreateController(apiClient);

        await controller.IntendedResults(2026, 4, SchemeId, PostedModel(), "save", CancellationToken.None);

        var notification = controller.TempData.GetNotification();
        Assert.Equal(NotificationType.Success, notification!.Type);
        Assert.Equal("Intended Results updated successfully.", notification.Message);
    }

    [Theory]
    [InlineData("cancel")]
    [InlineData("cancelAudit")]
    public async Task IntendedResults_Post_Cancelled_ShowsNoNotification(string command)
    {
        var apiClient = new FakeDistributionApiClient { IntendedResults = Response() };
        var controller = CreateController(apiClient);

        await controller.IntendedResults(2026, 4, SchemeId, PostedModel(), command, CancellationToken.None);

        Assert.Null(controller.TempData.GetNotification());
    }

    [Fact]
    public async Task IntendedResults_Post_ApiUnavailable_ShowsErrorNotification()
    {
        var apiClient = new FakeDistributionApiClient { IntendedResults = Response(), IntendedResultsSaveResult = null };
        var controller = CreateController(apiClient);

        var result = await controller.IntendedResults(2026, 4, SchemeId, PostedModel(), "save", CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        var notification = controller.TempData.GetNotification();
        Assert.Equal(NotificationType.Error, notification!.Type);
        Assert.Equal("Intended Results could not be saved. Please try again later.", notification.Message);
    }

    [Fact]
    public async Task IntendedResults_Post_FieldErrors_ShowsNoNotificationBecauseTheSummaryCarriesThem()
    {
        var apiClient = new FakeDistributionApiClient
        {
            IntendedResults = Response(),
            IntendedResultsSaveResult = new IntendedResultsSaveResult(
                false,
                new Dictionary<Guid, IntendedResultsFieldError[]>
                {
                    [MethodItemId] = [new IntendedResultsFieldError("IntendedResult", "Input must be of format: Numeric")],
                }),
        };
        var controller = CreateController(apiClient);

        await controller.IntendedResults(2026, 4, SchemeId, PostedModel(), "save", CancellationToken.None);

        Assert.Null(controller.TempData.GetNotification());
    }

    [Fact]
    public async Task IntendedResults_Post_InvalidModelState_RehydratesDisplayDataAndKeepsTypedValues()
    {
        var apiClient = new FakeDistributionApiClient { IntendedResults = Response() };
        var controller = CreateController(apiClient);
        controller.ModelState.AddModelError("AuditWhy", "Input can not exceed 2000 characters");

        var result = await controller.IntendedResults(2026, 4, SchemeId, PostedModel(), "save", CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<IntendedResultsViewModel>(view.Model);
        Assert.Empty(apiClient.SavedIntendedResults);

        // Display-only data comes back from the server, never from the form.
        Assert.Equal("17152A/BA PT0060: Mastitis bovine", model.DistributionHeading);
        Assert.Equal("Somatic cell count", Assert.Single(model.Tests).TestName);

        // The user's typed edits survive.
        var test = Assert.Single(model.Tests);
        Assert.Equal("User typed", Assert.Single(test.MethodItems).IntendedResult);
        Assert.Equal("User typed value", Assert.Single(Assert.Single(test.Samples).Values).IntendedResult);
    }

    [Fact]
    public async Task IntendedResults_Post_InvalidModelState_SchemeNoLongerReadable_RedisplaysPostedModel()
    {
        var apiClient = new FakeDistributionApiClient();
        var controller = CreateController(apiClient);
        controller.ModelState.AddModelError("AuditWhy", "Input can not exceed 2000 characters");
        var posted = PostedModel();

        var result = await controller.IntendedResults(2026, 4, SchemeId, posted, "save", CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(posted, view.Model);
        Assert.Equal(SchemeId, posted.MonthlyDistributionSchemeId);
        Assert.Equal(2026, posted.YearId);
        Assert.Equal(4, posted.MonthId);
    }

    [Fact]
    public async Task IntendedResults_Post_UnsuccessfulResult_MethodItemFieldError_AddsModelErrorAndRedisplays()
    {
        var apiClient = new FakeDistributionApiClient
        {
            IntendedResults = Response(),
            IntendedResultsSaveResult = new IntendedResultsSaveResult(
                false,
                new Dictionary<Guid, IntendedResultsFieldError[]>
                {
                    [MethodItemId] = [new IntendedResultsFieldError("IntendedResult", "Input must be of format: Numeric")],
                }),
        };
        var controller = CreateController(apiClient);

        var result = await controller.IntendedResults(2026, 4, SchemeId, PostedModel(), "save", CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.Contains(
            "Input must be of format: Numeric",
            controller.ModelState["Tests[0].MethodItems[0].IntendedResult"]!.Errors.Select(e => e.ErrorMessage));
    }

    [Fact]
    public async Task IntendedResults_Post_UnsuccessfulResult_SampleValueFieldError_AddsModelErrorAndRedisplays()
    {
        var apiClient = new FakeDistributionApiClient
        {
            IntendedResults = Response(),
            IntendedResultsSaveResult = new IntendedResultsSaveResult(
                false,
                new Dictionary<Guid, IntendedResultsFieldError[]>
                {
                    [SampleResultItemId] = [new IntendedResultsFieldError("IntendedResult2", "Input must be of format: Numeric")],
                }),
        };
        var controller = CreateController(apiClient);

        var result = await controller.IntendedResults(2026, 4, SchemeId, PostedModel(), "save", CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.Contains(
            "Input must be of format: Numeric",
            controller.ModelState["Tests[0].Samples[0].Values[0].IntendedResult2"]!.Errors.Select(e => e.ErrorMessage));
    }

    [Fact]
    public async Task IntendedResults_Post_UnsuccessfulResult_UnknownItemId_IsIgnored()
    {
        var apiClient = new FakeDistributionApiClient
        {
            IntendedResults = Response(),
            IntendedResultsSaveResult = new IntendedResultsSaveResult(
                false,
                new Dictionary<Guid, IntendedResultsFieldError[]>
                {
                    [Guid.NewGuid()] = [new IntendedResultsFieldError("IntendedResult", "Input must be of format: Numeric")],
                }),
        };
        var controller = CreateController(apiClient);

        var result = await controller.IntendedResults(2026, 4, SchemeId, PostedModel(), "save", CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.Empty(controller.ModelState);
    }

    [Fact]
    public async Task IntendedResults_Post_NullResult_IsHandledAndRedisplays()
    {
        var apiClient = new FakeDistributionApiClient { IntendedResults = Response(), IntendedResultsSaveResult = null };
        var controller = CreateController(apiClient);

        var result = await controller.IntendedResults(2026, 4, SchemeId, PostedModel(), "save", CancellationToken.None);

        var model = Assert.IsType<IntendedResultsViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal("User typed", Assert.Single(model.Tests).MethodItems.Single().IntendedResult);
        Assert.Empty(controller.ModelState);
    }

    private static IntendedResultsSaveResult AuditRequired() =>
        new(false, new Dictionary<Guid, IntendedResultsFieldError[]>(), AuditReasonRequired: true, AuditObjectName: "Intended Results Set");

    // Legacy RequireAudit -> Audit.aspx: diverted BEFORE anything is written.
    [Fact]
    public async Task IntendedResults_Post_AuditReasonRequired_RendersTheAuditStepInsteadOfRedirecting()
    {
        var apiClient = new FakeDistributionApiClient { IntendedResults = Response(), IntendedResultsSaveResult = AuditRequired() };
        var controller = CreateController(apiClient);

        var result = await controller.IntendedResults(2026, 4, SchemeId, PostedModel(), "save", CancellationToken.None);

        var model = Assert.IsType<IntendedResultsViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.True(model.RequiresAuditReason);
        Assert.True(model.IsAuditStep);
        Assert.Equal("Intended Results Set", model.AuditObjectName);
        Assert.Equal("save", model.PendingCommand);
        Assert.Equal("User typed", Assert.Single(model.Tests).MethodItems.Single().IntendedResult);
        Assert.Empty(controller.ModelState);
    }

    [Fact]
    public async Task IntendedResults_Post_AuditReasonSupplied_FlowsThroughToTheApi()
    {
        var apiClient = new FakeDistributionApiClient
        {
            IntendedResults = Response(),
            IntendedResultsSaveResult = new IntendedResultsSaveResult(true, new Dictionary<Guid, IntendedResultsFieldError[]>()),
        };
        var controller = CreateController(apiClient);
        var posted = PostedModel();
        posted.RequiresAuditReason = true;
        posted.IsAuditStep = true;
        posted.AuditWhy = "Corrected after re-checking the original data";

        var result = await controller.IntendedResults(2026, 4, SchemeId, posted, "save", CancellationToken.None);

        Assert.Equal("Corrected after re-checking the original data", Assert.Single(apiClient.SavedIntendedResults).AuditWhy);
        Assert.True(Assert.Single(apiClient.SavedIntendedResults).RequiresAuditReason);
        Assert.IsType<RedirectToActionResult>(result);
    }

    // Resubmitting the audit step with a blank reason must re-render the audit step.
    [Fact]
    public async Task IntendedResults_Post_InvalidAuditResubmit_KeepsRenderingTheAuditStep()
    {
        var apiClient = new FakeDistributionApiClient { IntendedResults = Response() };
        var controller = CreateController(apiClient);
        controller.ModelState.AddModelError("AuditWhy", "The reason for these changes must be specified");
        var posted = PostedModel();
        posted.RequiresAuditReason = true;
        posted.IsAuditStep = true;
        posted.AuditObjectName = "Intended Results Set";
        posted.PendingCommand = "save";
        posted.AuditWhy = "   ";

        var result = await controller.IntendedResults(2026, 4, SchemeId, posted, "save", CancellationToken.None);

        var model = Assert.IsType<IntendedResultsViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.True(model.RequiresAuditReason);
        Assert.True(model.IsAuditStep);
        Assert.Equal("Intended Results Set", model.AuditObjectName);
        Assert.Equal("save", model.PendingCommand);
        Assert.Equal("   ", model.AuditWhy);
        Assert.Empty(apiClient.SavedIntendedResults);
    }

    // Mirrors legacy's disabled controls: a non-applicable sample/test pair's server-fetched
    // (stored) values stand as-is, never overlaid by anything posted for it.
    [Fact]
    public async Task IntendedResults_Post_NonApplicableSample_RehydrationDoesNotOverlayPostedValues()
    {
        var apiClient = new FakeDistributionApiClient { IntendedResults = Response(sampleIsApplicable: false) };
        var controller = CreateController(apiClient);
        controller.ModelState.AddModelError("AuditWhy", "Input can not exceed 2000 characters");
        var posted = PostedModel(sampleValue: "Should not appear", sampleIsApplicable: false);

        var result = await controller.IntendedResults(2026, 4, SchemeId, posted, "save", CancellationToken.None);

        var model = Assert.IsType<IntendedResultsViewModel>(Assert.IsType<ViewResult>(result).Model);
        var value = Assert.Single(Assert.Single(Assert.Single(model.Tests).Samples).Values);
        Assert.Equal("Server result", value.IntendedResult);
    }

    [Fact]
    public async Task IntendedResults_Post_RehydrationIgnoresTestsNotPostedBack()
    {
        var apiClient = new FakeDistributionApiClient { IntendedResults = Response() };
        var controller = CreateController(apiClient);
        controller.ModelState.AddModelError("AuditWhy", "Input can not exceed 2000 characters");
        var posted = new IntendedResultsViewModel { Tests = [] };

        var result = await controller.IntendedResults(2026, 4, SchemeId, posted, "save", CancellationToken.None);

        var model = Assert.IsType<IntendedResultsViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal("Server value", Assert.Single(model.Tests).MethodItems.Single().IntendedResult);
    }
}
