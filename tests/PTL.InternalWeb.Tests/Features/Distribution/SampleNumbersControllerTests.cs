using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using PTL.ApiClient.Distribution;
using PTL.Contracts.Distribution;
using PTL.InternalWeb.Features.Distribution;
using PTL.InternalWeb.Tests.TestSupport;
using PTL.SharedUI.Notifications;

namespace PTL.InternalWeb.Tests.Features.Distribution;

// Sample Numbers screen - legacy Scheduling/SampleNumbers.aspx. Covers the GET mapping, the three
// legacy buttons (Save, Save and go to Intended Results, Cancel) and the rehydration that stands in
// for legacy's Session-held IntendedResultsSet on a failed post.
public class SampleNumbersControllerTests
{
    private static DistributionController CreateController(IDistributionApiClient apiClient) =>
        new(apiClient, new FakeSearchApiClient(), new FakeLookupApiClient())
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), new FakeTempDataProvider())
        };
    private static readonly Guid SchemeId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SampleId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid SampleTestId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid TestId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private static SampleNumbersResponse Response(bool requiresAssessment = false, bool isReadyForAssessment = false) =>
        new(
            SchemeId,
            "17152A/BA",
            "PT0060",
            "Mastitis bovine",
            9,
            "Server comments",
            requiresAssessment,
            isReadyForAssessment,
            [
                new SampleNumberRowResponse(SampleId, "S1", "Internal 1", "External 1", "Tabulation 1", "TC 1",
                    [new SampleAllocationResponse(SampleTestId, TestId, true)]),
            ],
            [new SampleNumbersTestResponse(TestId, "Somatic cell count", ["Result A"], ["Method A"], ["Appearance (Colour)"])]);

    private static SampleNumbersViewModel PostedModel(string comments = "User comments", bool allocationIsApplicable = false) => new()
    {
        Comments = comments,
        Samples =
        [
            new SampleNumberRowViewModel
            {
                SampleId = SampleId,
                SampleNumber = "S1-typed",
                InternalDescription = "Internal typed",
                ExternalDescription = "External typed",
                TabulationDescription = "Tabulation typed",
                TestConsultantsDescription = "TC typed",
                Allocations = [new SampleAllocationViewModel { SampleTestId = SampleTestId, TestId = TestId, IsApplicable = allocationIsApplicable }],
            },
        ],
    };

    [Fact]
    public async Task SampleNumbers_Get_UnknownScheme_ReturnsNotFound()
    {
        var controller = CreateController(new FakeDistributionApiClient());

        var result = await controller.SampleNumbers(2026, 4, SchemeId, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    // The structure could not be built from the Scheme - legacy would have thrown on the bare
    // guidDictionary lookup; the user gets the business reason instead of a 500.
    [Fact]
    public async Task SampleNumbers_Get_StructureUnavailable_RendersTheUnavailableViewWithTheReasons()
    {
        var apiClient = new FakeDistributionApiClient
        {
            SampleNumbersErrors = ["Scheme PT0060 has no Test Consultant Tabulation.", "Correct the Scheme first."],
        };
        var controller = CreateController(apiClient);

        var result = await controller.SampleNumbers(2026, 4, SchemeId, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("SampleNumbersUnavailable", view.ViewName);
        var model = Assert.IsType<SampleNumbersUnavailableViewModel>(view.Model);
        Assert.Equal(2026, model.YearId);
        Assert.Equal(4, model.MonthId);
        Assert.Equal(["Scheme PT0060 has no Test Consultant Tabulation.", "Correct the Scheme first."], model.Errors);
    }

    // Errors win over a page: a distribution whose structure failed must never render the grid.
    [Fact]
    public async Task SampleNumbers_Get_ErrorsAlongsideAPage_StillRendersTheUnavailableView()
    {
        var apiClient = new FakeDistributionApiClient
        {
            SampleNumbers = Response(),
            SampleNumbersErrors = ["Correct the Scheme first."],
        };
        var controller = CreateController(apiClient);

        var result = await controller.SampleNumbers(2026, 4, SchemeId, CancellationToken.None);

        Assert.Equal("SampleNumbersUnavailable", Assert.IsType<ViewResult>(result).ViewName);
    }

    [Fact]
    public async Task SampleNumbers_Get_MapsResponseOntoViewModel()
    {
        var apiClient = new FakeDistributionApiClient { SampleNumbers = Response() };
        var controller = CreateController(apiClient);

        var result = await controller.SampleNumbers(2026, 4, SchemeId, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<SampleNumbersViewModel>(view.Model);
        Assert.Equal(SchemeId, model.MonthlyDistributionSchemeId);
        Assert.Equal(2026, model.YearId);
        Assert.Equal(4, model.MonthId);
        Assert.Equal("17152A/BA PT0060: Mastitis bovine", model.DistributionHeading);
        Assert.Equal(9, model.NumberOfSetsRequired);
        Assert.Equal("Server comments", model.Comments);
        Assert.True(model.AllocationEditable);

        var sample = Assert.Single(model.Samples);
        Assert.Equal(SampleId, sample.SampleId);
        Assert.Equal("S1", sample.SampleNumber);
        Assert.Equal("Internal 1", sample.InternalDescription);
        Assert.Equal("External 1", sample.ExternalDescription);
        Assert.Equal("Tabulation 1", sample.TabulationDescription);
        Assert.Equal("TC 1", sample.TestConsultantsDescription);

        var allocation = Assert.Single(sample.Allocations);
        Assert.Equal(SampleTestId, allocation.SampleTestId);
        Assert.Equal(TestId, allocation.TestId);
        Assert.True(allocation.IsApplicable);

        var test = Assert.Single(model.Tests);
        Assert.Equal("Somatic cell count", test.TestName);
        Assert.Equal(["Result A"], test.TestResultItemNames);
        Assert.Equal(["Method A"], test.TestMethodItemNames);
        Assert.Equal(["Appearance (Colour)"], test.CategoryItemNames);
    }

    [Fact]
    public async Task SampleNumbers_Get_ReleasedAssessmentScheme_LocksAllocations()
    {
        var apiClient = new FakeDistributionApiClient { SampleNumbers = Response(requiresAssessment: true, isReadyForAssessment: true) };
        var controller = CreateController(apiClient);

        var result = await controller.SampleNumbers(2026, 4, SchemeId, CancellationToken.None);

        var model = Assert.IsType<SampleNumbersViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.False(model.AllocationEditable);
    }

    [Fact]
    public async Task SampleNumbers_Post_CancelCommand_RedirectsToScheduleWithoutSaving()
    {
        var apiClient = new FakeDistributionApiClient();
        var controller = CreateController(apiClient);

        var result = await controller.SampleNumbers(2026, 4, SchemeId, PostedModel(), "cancel", CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(DistributionController.Schedule), redirect.ActionName);
        Assert.Equal(2026, redirect.RouteValues!["year"]);
        Assert.Equal(4, redirect.RouteValues["monthId"]);
        Assert.Empty(apiClient.SavedSampleNumbers);
    }

    [Fact]
    public async Task SampleNumbers_Post_SaveCommand_SavesAndRedirectsToSchedule()
    {
        var apiClient = new FakeDistributionApiClient
        {
            SampleNumbers = Response(),
            SampleNumbersSaveResult = new SampleNumbersSaveResult(true, [], new Dictionary<Guid, SampleNumbersFieldError[]>()),
        };
        var controller = CreateController(apiClient);

        var result = await controller.SampleNumbers(2026, 4, SchemeId, PostedModel(), "save", CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(DistributionController.Schedule), redirect.ActionName);
        Assert.Equal(2026, redirect.RouteValues!["year"]);
        Assert.Equal(4, redirect.RouteValues["monthId"]);

        var saved = Assert.Single(apiClient.SavedSampleNumbers);
        Assert.Equal("User comments", saved.Comments);
        var sample = Assert.Single(saved.Samples);
        Assert.Equal(SampleId, sample.SampleId);
        Assert.Equal("S1-typed", sample.SampleNumber);
        Assert.Equal("Internal typed", sample.InternalDescription);
        Assert.Equal(SampleTestId, Assert.Single(sample.Allocations).SampleTestId);
    }

    // The save notification follows the convention used by every other migrated save screen.
    [Theory]
    [InlineData("save")]
    [InlineData("saveAndGoToIntendedResults")]
    public async Task SampleNumbers_Post_SaveSucceeds_ShowsSuccessNotification(string command)
    {
        var apiClient = new FakeDistributionApiClient
        {
            SampleNumbers = Response(),
            SampleNumbersSaveResult = new SampleNumbersSaveResult(true, [], new Dictionary<Guid, SampleNumbersFieldError[]>()),
        };
        var controller = CreateController(apiClient);

        await controller.SampleNumbers(2026, 4, SchemeId, PostedModel(), command, CancellationToken.None);

        var notification = controller.TempData.GetNotification();
        Assert.Equal(NotificationType.Success, notification!.Type);
        Assert.Equal("Sample Numbers updated successfully.", notification.Message);
    }

    [Theory]
    [InlineData("cancel")]
    [InlineData("cancelAudit")]
    public async Task SampleNumbers_Post_Cancelled_ShowsNoNotification(string command)
    {
        var apiClient = new FakeDistributionApiClient { SampleNumbers = Response() };
        var controller = CreateController(apiClient);

        await controller.SampleNumbers(2026, 4, SchemeId, PostedModel(), command, CancellationToken.None);

        Assert.Null(controller.TempData.GetNotification());
    }

    // A validation failure is reported in the error summary, so a banner would be noise - but an
    // unreachable API produces no field errors at all and must still tell the user something.
    [Fact]
    public async Task SampleNumbers_Post_ApiUnavailable_ShowsErrorNotification()
    {
        var apiClient = new FakeDistributionApiClient { SampleNumbers = Response(), SampleNumbersSaveResult = null };
        var controller = CreateController(apiClient);

        var result = await controller.SampleNumbers(2026, 4, SchemeId, PostedModel(), "save", CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        var notification = controller.TempData.GetNotification();
        Assert.Equal(NotificationType.Error, notification!.Type);
        Assert.Equal("Sample Numbers could not be saved. Please try again later.", notification.Message);
    }

    [Fact]
    public async Task SampleNumbers_Post_FieldErrors_ShowsNoNotificationBecauseTheSummaryCarriesThem()
    {
        var apiClient = new FakeDistributionApiClient
        {
            SampleNumbers = Response(),
            SampleNumbersSaveResult = new SampleNumbersSaveResult(
                false,
                [],
                new Dictionary<Guid, SampleNumbersFieldError[]>
                {
                    [SampleId] = [new SampleNumbersFieldError("SampleNumber", "Enter the Sample Number")],
                }),
        };
        var controller = CreateController(apiClient);

        await controller.SampleNumbers(2026, 4, SchemeId, PostedModel(), "save", CancellationToken.None);

        Assert.Null(controller.TempData.GetNotification());
    }

    [Fact]
    public async Task SampleNumbers_Post_SaveAndGoToIntendedResults_RedirectsToIntendedResultsStage()
    {
        var apiClient = new FakeDistributionApiClient
        {
            SampleNumbers = Response(),
            SampleNumbersSaveResult = new SampleNumbersSaveResult(true, [], new Dictionary<Guid, SampleNumbersFieldError[]>()),
        };
        var controller = CreateController(apiClient);

        var result = await controller.SampleNumbers(2026, 4, SchemeId, PostedModel(), "saveAndGoToIntendedResults", CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(DistributionController.IntendedResults), redirect.ActionName);
        Assert.Equal(SchemeId, redirect.RouteValues!["schemeId"]);
        Assert.Single(apiClient.SavedSampleNumbers);
    }

    [Fact]
    public async Task SampleNumbers_Post_InvalidModelState_RehydratesDisplayDataAndKeepsTypedValues()
    {
        var apiClient = new FakeDistributionApiClient { SampleNumbers = Response() };
        var controller = CreateController(apiClient);
        controller.ModelState.AddModelError("Samples[0].SampleNumber", "Sample Number Required");

        var result = await controller.SampleNumbers(2026, 4, SchemeId, PostedModel(), "save", CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<SampleNumbersViewModel>(view.Model);
        Assert.Empty(apiClient.SavedSampleNumbers);

        // Display-only data comes back from the server, never from the form.
        Assert.Equal("17152A/BA PT0060: Mastitis bovine", model.DistributionHeading);
        Assert.Equal(9, model.NumberOfSetsRequired);
        Assert.Equal("Somatic cell count", Assert.Single(model.Tests).TestName);

        // The user's edits survive.
        Assert.Equal("User comments", model.Comments);
        var sample = Assert.Single(model.Samples);
        Assert.Equal("S1-typed", sample.SampleNumber);
        Assert.Equal("Internal typed", sample.InternalDescription);
        Assert.Equal("External typed", sample.ExternalDescription);
        Assert.Equal("Tabulation typed", sample.TabulationDescription);
        Assert.Equal("TC typed", sample.TestConsultantsDescription);
        Assert.False(Assert.Single(sample.Allocations).IsApplicable);
    }

    [Fact]
    public async Task SampleNumbers_Post_InvalidModelState_SchemeNoLongerReadable_RedisplaysPostedModel()
    {
        var apiClient = new FakeDistributionApiClient();
        var controller = CreateController(apiClient);
        controller.ModelState.AddModelError("Samples[0].SampleNumber", "Sample Number Required");
        var posted = PostedModel();

        var result = await controller.SampleNumbers(2026, 4, SchemeId, posted, "save", CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(posted, view.Model);
        Assert.Equal(SchemeId, posted.MonthlyDistributionSchemeId);
        Assert.Equal(2026, posted.YearId);
        Assert.Equal(4, posted.MonthId);
    }

    [Fact]
    public async Task SampleNumbers_Post_ReleasedAssessmentScheme_DoesNotOverlayPostedAllocations()
    {
        var apiClient = new FakeDistributionApiClient { SampleNumbers = Response(requiresAssessment: true, isReadyForAssessment: true) };
        var controller = CreateController(apiClient);
        controller.ModelState.AddModelError("Comments", "Comments must not exceed 8000 characters");

        var result = await controller.SampleNumbers(2026, 4, SchemeId, PostedModel(allocationIsApplicable: false), "save", CancellationToken.None);

        var model = Assert.IsType<SampleNumbersViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.False(model.AllocationEditable);
        Assert.True(Assert.Single(Assert.Single(model.Samples).Allocations).IsApplicable);
    }

    [Fact]
    public async Task SampleNumbers_Post_UnsuccessfulResult_AddsPageAndFieldErrorsAndRedisplays()
    {
        var apiClient = new FakeDistributionApiClient
        {
            SampleNumbers = Response(),
            SampleNumbersSaveResult = new SampleNumbersSaveResult(
                false,
                ["Comments must not exceed 8000 characters"],
                new Dictionary<Guid, SampleNumbersFieldError[]>
                {
                    [SampleId] = [new SampleNumbersFieldError("SampleNumber", "Sample Number Required")],
                }),
        };
        var controller = CreateController(apiClient);

        var result = await controller.SampleNumbers(2026, 4, SchemeId, PostedModel(), "save", CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.Contains("Comments must not exceed 8000 characters", controller.ModelState["Comments"]!.Errors.Select(e => e.ErrorMessage));
        Assert.Contains("Sample Number Required", controller.ModelState["Samples[0].SampleNumber"]!.Errors.Select(e => e.ErrorMessage));
    }

    [Fact]
    public async Task SampleNumbers_Post_UnsuccessfulResult_UnknownSampleId_IsIgnored()
    {
        var apiClient = new FakeDistributionApiClient
        {
            SampleNumbers = Response(),
            SampleNumbersSaveResult = new SampleNumbersSaveResult(
                false,
                [],
                new Dictionary<Guid, SampleNumbersFieldError[]>
                {
                    [Guid.NewGuid()] = [new SampleNumbersFieldError("SampleNumber", "Sample Number Required")],
                }),
        };
        var controller = CreateController(apiClient);

        var result = await controller.SampleNumbers(2026, 4, SchemeId, PostedModel(), "save", CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.Empty(controller.ModelState);
    }

    [Fact]
    public async Task SampleNumbers_Post_NullResult_IsHandledAndRedisplays()
    {
        var apiClient = new FakeDistributionApiClient { SampleNumbers = Response(), SampleNumbersSaveResult = null };
        var controller = CreateController(apiClient);

        var result = await controller.SampleNumbers(2026, 4, SchemeId, PostedModel(), "save", CancellationToken.None);

        var model = Assert.IsType<SampleNumbersViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal("User comments", model.Comments);
        Assert.Empty(controller.ModelState);
    }

    [Fact]
    public async Task SampleNumbers_Post_RehydrationIgnoresSamplesNotPostedBack()
    {
        var apiClient = new FakeDistributionApiClient { SampleNumbers = Response() };
        var controller = CreateController(apiClient);
        controller.ModelState.AddModelError("Comments", "Comments must not exceed 8000 characters");
        var posted = new SampleNumbersViewModel { Comments = "User comments", Samples = [] };

        var result = await controller.SampleNumbers(2026, 4, SchemeId, posted, "save", CancellationToken.None);

        var model = Assert.IsType<SampleNumbersViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal("S1", Assert.Single(model.Samples).SampleNumber);
    }

    private static SampleNumbersSaveResult AuditRequired() =>
        new(false, [], new Dictionary<Guid, SampleNumbersFieldError[]>(), AuditReasonRequired: true, AuditObjectName: "Intended Results Set");

    // Legacy SampleNumbers.aspx.vb redirects to ~/Audit.aspx instead of saving when IsAuditDirty.
    [Fact]
    public async Task SampleNumbers_Post_AuditReasonRequired_RendersTheAuditStepInsteadOfRedirecting()
    {
        var apiClient = new FakeDistributionApiClient { SampleNumbers = Response(), SampleNumbersSaveResult = AuditRequired() };
        var controller = CreateController(apiClient);

        var result = await controller.SampleNumbers(2026, 4, SchemeId, PostedModel(), "save", CancellationToken.None);

        var model = Assert.IsType<SampleNumbersViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.True(model.RequiresAuditReason);
        Assert.Equal("Intended Results Set", model.AuditObjectName);
        Assert.Equal("save", model.PendingCommand);
        Assert.Equal("User comments", model.Comments);
        Assert.Empty(controller.ModelState);
    }

    [Fact]
    public async Task SampleNumbers_Post_AuditReasonRequired_RemembersTheOriginalDestination()
    {
        var apiClient = new FakeDistributionApiClient { SampleNumbers = Response(), SampleNumbersSaveResult = AuditRequired() };
        var controller = CreateController(apiClient);

        var result = await controller.SampleNumbers(2026, 4, SchemeId, PostedModel(), "saveAndGoToIntendedResults", CancellationToken.None);

        var model = Assert.IsType<SampleNumbersViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal("saveAndGoToIntendedResults", model.PendingCommand);
    }

    [Fact]
    public async Task SampleNumbers_Post_AuditReasonSupplied_FlowsThroughToTheApi()
    {
        var apiClient = new FakeDistributionApiClient
        {
            SampleNumbers = Response(),
            SampleNumbersSaveResult = new SampleNumbersSaveResult(true, [], new Dictionary<Guid, SampleNumbersFieldError[]>()),
        };
        var controller = CreateController(apiClient);
        var posted = PostedModel();
        posted.RequiresAuditReason = true;
        posted.AuditWhy = "Corrected a typo";

        var result = await controller.SampleNumbers(2026, 4, SchemeId, posted, "save", CancellationToken.None);

        Assert.Equal("Corrected a typo", Assert.Single(apiClient.SavedSampleNumbers).AuditWhy);
        Assert.IsType<RedirectToActionResult>(result);
    }

    // Resubmitting the audit step with a blank reason must re-render the audit step, not fall back
    // to the sample table - so the rehydration has to carry the audit state across.
    [Fact]
    public async Task SampleNumbers_Post_InvalidAuditResubmit_KeepsRenderingTheAuditStep()
    {
        var apiClient = new FakeDistributionApiClient { SampleNumbers = Response() };
        var controller = CreateController(apiClient);
        controller.ModelState.AddModelError("AuditWhy", "The reason for these changes must be specified");
        var posted = PostedModel();
        posted.RequiresAuditReason = true;
        posted.AuditObjectName = "Intended Results Set";
        posted.PendingCommand = "saveAndGoToIntendedResults";
        posted.AuditWhy = "   ";

        var result = await controller.SampleNumbers(2026, 4, SchemeId, posted, "save", CancellationToken.None);

        var model = Assert.IsType<SampleNumbersViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.True(model.RequiresAuditReason);
        Assert.Equal("Intended Results Set", model.AuditObjectName);
        Assert.Equal("saveAndGoToIntendedResults", model.PendingCommand);
        Assert.Equal("   ", model.AuditWhy);
        Assert.Empty(apiClient.SavedSampleNumbers);
    }

    // Legacy Audit.aspx's Cancel returns to the destination the user chose WITHOUT saving.
    [Fact]
    public async Task SampleNumbers_Post_CancelAudit_ReturnsToScheduleWithoutSaving()
    {
        var apiClient = new FakeDistributionApiClient { SampleNumbers = Response() };
        var controller = CreateController(apiClient);
        var posted = PostedModel();
        posted.RequiresAuditReason = true;
        posted.PendingCommand = "save";

        var result = await controller.SampleNumbers(2026, 4, SchemeId, posted, "cancelAudit", CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(DistributionController.Schedule), redirect.ActionName);
        Assert.Equal(2026, redirect.RouteValues!["year"]);
        Assert.Equal(4, redirect.RouteValues["monthId"]);
        Assert.Empty(apiClient.SavedSampleNumbers);
    }

    [Fact]
    public async Task SampleNumbers_Post_CancelAudit_FromTheIntendedResultsButton_ReturnsToIntendedResults()
    {
        var apiClient = new FakeDistributionApiClient { SampleNumbers = Response() };
        var controller = CreateController(apiClient);
        var posted = PostedModel();
        posted.RequiresAuditReason = true;
        posted.PendingCommand = "saveAndGoToIntendedResults";

        var result = await controller.SampleNumbers(2026, 4, SchemeId, posted, "cancelAudit", CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(DistributionController.IntendedResults), redirect.ActionName);
        Assert.Equal(SchemeId, redirect.RouteValues!["schemeId"]);
        Assert.Empty(apiClient.SavedSampleNumbers);
    }

    // Cancel on the audit step is a no-save path, so it must not even call the API to rehydrate.
    [Fact]
    public async Task SampleNumbers_Post_CancelAudit_WithNoPendingCommand_ReturnsToSchedule()
    {
        var apiClient = new FakeDistributionApiClient();
        var controller = CreateController(apiClient);

        var result = await controller.SampleNumbers(2026, 4, SchemeId, PostedModel(), "cancelAudit", CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(DistributionController.Schedule), redirect.ActionName);
        Assert.Empty(apiClient.SavedSampleNumbers);
    }

    // The audit diversion takes priority over the error-rendering path, so no ModelState errors
    // are added and the user sees the reason prompt rather than a validation summary.
    [Fact]
    public async Task SampleNumbers_Post_AuditReasonRequired_SchemeNoLongerReadable_StillRendersTheAuditStep()
    {
        var apiClient = new FakeDistributionApiClient { SampleNumbers = null, SampleNumbersSaveResult = AuditRequired() };
        var controller = CreateController(apiClient);

        var result = await controller.SampleNumbers(2026, 4, SchemeId, PostedModel(), "save", CancellationToken.None);

        var model = Assert.IsType<SampleNumbersViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.True(model.RequiresAuditReason);
        Assert.Equal("Intended Results Set", model.AuditObjectName);
    }
}
