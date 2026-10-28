using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using PTL.ApiClient.Distribution;
using PTL.Contracts.Distribution;
using PTL.InternalWeb.Authorization;
using PTL.InternalWeb.Confirmation;
using PTL.InternalWeb.Features.Assessor;
using PTL.InternalWeb.Tests.TestSupport;
using PTL.SharedUI.Notifications;

namespace PTL.InternalWeb.Tests.Features.Assessor;

public class AssessorControllerTests
{
    private static readonly Guid TestId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid SchemeId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private readonly FakeAssessorApiClient _apiClient = new();

    private AssessorController Controller
    {
        get
        {
            var httpContext = new DefaultHttpContext();
            return new AssessorController(_apiClient)
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext },
                TempData = new TempDataDictionary(httpContext, new FakeTempDataProvider()),
                Url = new FakeUrlHelper(),
            };
        }
    }

    private static AssessorMenuResponse Menu() =>
        new(
            [2020, 2021],
            2021,
            [
                new AssessorMonthResponse(2021, 9, [new AssessorDistributionResponse(SchemeId, "14468/A/HI: PT0166 CPT", [new AssessorTestResponse(TestId, "Giemsa", false)])]),
                new AssessorMonthResponse(2021, 2, []),
            ]);

    private static AssessorItemListResponse ItemList(bool submitted = false, bool completed = true) =>
        new(
            TestId,
            "February 2021",
            "14468/A/HI: PT0166 CPT",
            "Giemsa",
            submitted,
            "Mary Dexter",
            submitted ? new DateTime(2021, 3, 17, 13, 17, 39) : null,
            [new AssessorSampleResponse(Guid.NewGuid(), "21/7140", [])],
            completed);

    [Fact]
    public void Controller_IsOpenOnlyToTheAssessorRole() =>
        Assert.Equal(PtlPolicies.Assessor, typeof(AssessorController).GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true).Cast<AuthorizeAttribute>().Single().Policy);

    // ---- Menu -----------------------------------------------------------------------------------

    [Fact]
    public async Task Index_ShowsTheMenuFromTheApiWithTheRequestedYear()
    {
        _apiClient.Menu = new(AssessorApiStatus.Ok, Menu());

        var result = await Controller.Index(2020, CancellationToken.None);

        var model = Assert.IsType<AssessorMenuViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal([2020, 2021], model.Years);
        Assert.Equal(2021, model.SelectedYear);
        Assert.Equal([2020], _apiClient.RequestedMenus.Select(y => y ?? 0));
    }

    [Fact]
    public async Task Index_TitlesTheMonthsAndKeepsTheirOrder()
    {
        _apiClient.Menu = new(AssessorApiStatus.Ok, Menu());

        var model = Assert.IsType<AssessorMenuViewModel>(Assert.IsType<ViewResult>(await Controller.Index(null, CancellationToken.None)).Model);

        Assert.Equal(["September 2021", "February 2021"], model.Months.Select(m => m.Title));
        Assert.Equal("14468/A/HI: PT0166 CPT", Assert.Single(model.Months[0].Distributions).DistributionTitle);
        Assert.Null(Assert.Single(_apiClient.RequestedMenus));
    }

    [Fact]
    public async Task Index_NoDistributions_ShowsAnEmptyMenu()
    {
        var result = await Controller.Index(null, CancellationToken.None);

        var model = Assert.IsType<AssessorMenuViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Empty(model.Years);
        Assert.Empty(model.Months);
    }

    [Fact]
    public async Task Index_Forbidden_IsRefusedLikeAnyRoleGatedPage()
    {
        _apiClient.Menu = new(AssessorApiStatus.Forbidden);

        Assert.IsType<ForbidResult>(await Controller.Index(null, CancellationToken.None));
    }

    [Fact]
    public async Task Index_NotFound_IsNotFound()
    {
        _apiClient.Menu = new(AssessorApiStatus.NotFound);

        Assert.IsType<NotFoundResult>(await Controller.Index(null, CancellationToken.None));
    }

    // ---- Assessment items -----------------------------------------------------------------------

    [Fact]
    public async Task AssessmentItemList_ShowsTheItemsOfTheTestAndKeepsTheYearForBack()
    {
        _apiClient.ItemList = new(AssessorApiStatus.Ok, ItemList());

        var result = await Controller.AssessmentItemList(TestId, 2021, CancellationToken.None);

        var model = Assert.IsType<AssessorItemListViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(("Giemsa", 2021), (model.List.TestName, model.Year));
        Assert.Equal([TestId], _apiClient.RequestedItemLists);
    }

    [Theory]
    [InlineData(AssessorApiStatus.Forbidden, typeof(ForbidResult))]
    [InlineData(AssessorApiStatus.NotFound, typeof(NotFoundResult))]
    public async Task AssessmentItemList_NotAllowedOrMissing_IsRefusedOrNotFound(AssessorApiStatus status, Type expected)
    {
        _apiClient.ItemList = new(status);

        Assert.IsType(expected, await Controller.AssessmentItemList(TestId, null, CancellationToken.None));
    }

    // ---- View models ----------------------------------------------------------------------------

    [Fact]
    public void SubmittedMessage_NotSubmitted_IsNone() =>
        Assert.Null(new AssessorItemListViewModel { List = ItemList() }.SubmittedMessage);

    [Fact]
    public void SubmittedMessage_Submitted_UsesTheLegacyWording() =>
        Assert.Equal(
            "Assessment was submitted successfully for this test by Mary Dexter on 17/03/2021",
            new AssessorItemListViewModel { List = ItemList(submitted: true) }.SubmittedMessage);

    [Fact]
    public void EntryUrl_CarriesTheTestTheSampleTestTheParticipantAndTheYear()
    {
        var sampleTestId = Guid.NewGuid();
        var participantId = Guid.NewGuid();

        Assert.Equal(
            $"/Assessor/AssessmentEntry?testId={TestId}&sampleTestId={sampleTestId}&participantId={participantId}&year=2021",
            AssessorItemListViewModel.EntryUrl(new AssessorItemResponse(sampleTestId, participantId, "1126", false), TestId, 2021));
    }

    [Fact]
    public void EntryUrl_WithoutAYear_LeavesItOut()
    {
        var item = new AssessorItemResponse(Guid.NewGuid(), Guid.NewGuid(), "1126", false);

        Assert.DoesNotContain("year=", AssessorItemListViewModel.EntryUrl(item, TestId, null), StringComparison.Ordinal);
    }

    [Fact]
    public void From_NullResponse_Throws() =>
        Assert.Throws<ArgumentNullException>(() => AssessorMenuViewModel.From(null!));

    [Fact]
    public void EntryUrl_NullItem_Throws() =>
        Assert.Throws<ArgumentNullException>(() => AssessorItemListViewModel.EntryUrl(null!, TestId, null));

    // ---- Assessment entry -----------------------------------------------------------------------

    private static readonly Guid SampleTestId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid ParticipantId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid Morphology = Guid.Parse("b0000000-0000-0000-0000-000000000001");
    private static readonly Guid Staining = Guid.Parse("b0000000-0000-0000-0000-000000000002");
    private static readonly Guid Shape = Guid.Parse("c0000000-0000-0000-0000-000000000001");
    private static readonly Guid Colour = Guid.Parse("c0000000-0000-0000-0000-000000000002");
    private static readonly Guid Intensity = Guid.Parse("c0000000-0000-0000-0000-000000000004");

    private static AssessorEntryResponse Entry(bool submitted = false, int overall = 3, int stainingScore = 0) =>
        new(
            TestId,
            SampleTestId,
            ParticipantId,
            "February 2021",
            "14468/A/HI: PT0166 CPT",
            "1126",
            "21/7140",
            "Alcian blue / PAS",
            submitted,
            "Good",
            overall,
            [new AssessorResultItemResponse("Isolate", "Positive")],
            [
                new AssessorCategoryResponse(Morphology, "Morphology", 2, [new(Shape, "Shape", true), new(Colour, "Colour", false)]),
                new AssessorCategoryResponse(Staining, "Staining", stainingScore, [new(Intensity, "Intensity", false)]),
            ]);

    private static AssessorEntryViewModel Posted(string? comment = "Fine", int? overall = 4, int? morphologyScore = 3, int? stainingScore = 5, int? year = 2021) =>
        new()
        {
            SampleTestId = SampleTestId,
            ParticipantId = ParticipantId,
            Year = year,
            Comment = comment,
            OverallScore = overall,
            Categories =
            [
                new AssessorCategoryInput { CategoryItemId = Morphology, Score = morphologyScore, CriterionItemIds = [Colour] },
                new AssessorCategoryInput { CategoryItemId = Staining, Score = stainingScore, CriterionItemIds = [] },
            ],
        };

    [Fact]
    public async Task AssessmentEntry_Get_ShowsTheSavedValuesAndLeavesUnsavedScoresUnchosen()
    {
        _apiClient.Entry = new(AssessorApiStatus.Ok, Entry(overall: 0));

        var result = await Controller.AssessmentEntry(SampleTestId, ParticipantId, 2021, CancellationToken.None);

        var model = Assert.IsType<AssessorEntryViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal((SampleTestId, ParticipantId, 2021, "Good", (int?)null), (model.SampleTestId, model.ParticipantId, model.Year, model.Comment, model.OverallScore));
        Assert.Equal([2, (int?)null], model.Categories.Select(c => c.Score));
        Assert.Equal([Shape], model.Categories[0].CriterionItemIds);
        Assert.Empty(model.Categories[1].CriterionItemIds);
        Assert.Equal([(SampleTestId, ParticipantId)], _apiClient.RequestedEntries);
    }

    [Theory]
    [InlineData(AssessorApiStatus.Forbidden, typeof(ForbidResult))]
    [InlineData(AssessorApiStatus.NotFound, typeof(NotFoundResult))]
    public async Task AssessmentEntry_Get_NotAllowedOrMissing_IsRefusedOrNotFound(AssessorApiStatus status, Type expected)
    {
        _apiClient.Entry = new(status);

        Assert.IsType(expected, await Controller.AssessmentEntry(SampleTestId, ParticipantId, null, CancellationToken.None));
    }

    [Theory]
    [InlineData(AssessorApiStatus.Forbidden, typeof(ForbidResult))]
    [InlineData(AssessorApiStatus.NotFound, typeof(NotFoundResult))]
    public async Task AssessmentEntry_Post_NotAllowedOrMissing_SavesNothing(AssessorApiStatus status, Type expected)
    {
        _apiClient.Entry = new(status);

        Assert.IsType(expected, await Controller.AssessmentEntry(Posted(), "save", CancellationToken.None));
        Assert.Empty(_apiClient.Saved);
    }

    [Fact]
    public async Task AssessmentEntry_Post_ATestAlreadySubmitted_ReturnsToTheItemsWithAnErrorAndSavesNothing()
    {
        _apiClient.Entry = new(AssessorApiStatus.Ok, Entry(submitted: true));
        var controller = Controller;

        var result = await controller.AssessmentEntry(Posted(), "save", CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(AssessorController.AssessmentItemList), redirect.ActionName);
        Assert.Equal((TestId, 2021), ((Guid)redirect.RouteValues!["testId"]!, (int)redirect.RouteValues["year"]!));
        var notification = controller.TempData.GetNotification();
        Assert.Equal((NotificationType.Error, AssessorController.SubmittedNotification), (notification?.Type, notification?.Message));
        Assert.Empty(_apiClient.Saved);
    }

    [Fact]
    public async Task AssessmentEntry_Post_MissingScores_ShowsTheFormAgainWithWhatWasEnteredAndSavesNothing()
    {
        _apiClient.Entry = new(AssessorApiStatus.Ok, Entry());
        var controller = Controller;
        controller.ModelState.AddModelError("OverallScore", "An overall score is required");

        var result = await controller.AssessmentEntry(Posted(comment: "Kept words", overall: null), "save", CancellationToken.None);

        var model = Assert.IsType<AssessorEntryViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(("Kept words", (int?)null), (model.Comment, model.OverallScore));
        Assert.Same(_apiClient.Entry.Value, model.Entry);
        Assert.Empty(_apiClient.Saved);
    }

    [Fact]
    public async Task AssessmentEntry_Post_Save_SavesAndReturnsToTheItemsWithAConfirmation()
    {
        _apiClient.Entry = new(AssessorApiStatus.Ok, Entry());
        var controller = Controller;

        var result = await controller.AssessmentEntry(Posted(), AssessorEntryViewModel.SaveCommand, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(AssessorController.AssessmentItemList), redirect.ActionName);
        Assert.Equal((TestId, 2021), ((Guid)redirect.RouteValues!["testId"]!, (int)redirect.RouteValues["year"]!));
        var notification = controller.TempData.GetNotification();
        Assert.Equal((NotificationType.Success, AssessorController.SavedNotification), (notification?.Type, notification?.Message));
    }

    [Fact]
    public async Task AssessmentEntry_Post_Apply_SavesAndStaysOnTheEntry()
    {
        _apiClient.Entry = new(AssessorApiStatus.Ok, Entry());

        var result = await Controller.AssessmentEntry(Posted(), AssessorEntryViewModel.ApplyCommand, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(AssessorController.AssessmentEntry), redirect.ActionName);
        Assert.Equal((SampleTestId, ParticipantId, TestId, 2021),
            ((Guid)redirect.RouteValues!["sampleTestId"]!, (Guid)redirect.RouteValues["participantId"]!, (Guid)redirect.RouteValues["testId"]!, (int)redirect.RouteValues["year"]!));
    }

    [Fact]
    public async Task AssessmentEntry_Post_SendsTheEnteredValuesToTheApi()
    {
        _apiClient.Entry = new(AssessorApiStatus.Ok, Entry());

        await Controller.AssessmentEntry(Posted(comment: "Fine", overall: 4), "save", CancellationToken.None);

        var (sampleTestId, participantId, request) = Assert.Single(_apiClient.Saved);
        Assert.Equal((SampleTestId, ParticipantId, "Fine", 4), (sampleTestId, participantId, request.Comment, request.OverallScore));
        Assert.Equal([Morphology, Staining], request.Categories.Select(c => c.CategoryItemId));
        Assert.Equal([3, 5], request.Categories.Select(c => c.Score));
        Assert.Equal([Colour], request.Categories[0].CriterionItemIds);
    }

    [Fact]
    public async Task AssessmentEntry_Post_TheApiRefusesTheValues_ShowsTheFieldsAtFault()
    {
        _apiClient.Entry = new(AssessorApiStatus.Ok, Entry());
        _apiClient.SaveResult = new(AssessorSaveStatus.Invalid, [new("Comment", "The assessment comment cannot exceed 2000 characters")]);
        var controller = Controller;

        var result = await controller.AssessmentEntry(Posted(), "save", CancellationToken.None);

        Assert.IsType<AssessorEntryViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal("The assessment comment cannot exceed 2000 characters", Assert.Single(controller.ModelState["Comment"]!.Errors).ErrorMessage);
    }

    [Fact]
    public async Task AssessmentEntry_Post_SubmittedMeanwhile_ReturnsToTheItemsWithAnError()
    {
        _apiClient.Entry = new(AssessorApiStatus.Ok, Entry());
        _apiClient.SaveResult = AssessorSaveApiResult.Of(AssessorSaveStatus.Submitted);
        var controller = Controller;

        var result = await controller.AssessmentEntry(Posted(), "save", CancellationToken.None);

        Assert.Equal(nameof(AssessorController.AssessmentItemList), Assert.IsType<RedirectToActionResult>(result).ActionName);
        Assert.Equal(NotificationType.Error, controller.TempData.GetNotification()?.Type);
    }

    [Theory]
    [InlineData(AssessorSaveStatus.Forbidden, typeof(ForbidResult))]
    [InlineData(AssessorSaveStatus.NotFound, typeof(NotFoundResult))]
    public async Task AssessmentEntry_Post_TheApiRefusesTheSave_IsRefusedOrNotFound(AssessorSaveStatus status, Type expected)
    {
        _apiClient.Entry = new(AssessorApiStatus.Ok, Entry());
        _apiClient.SaveResult = AssessorSaveApiResult.Of(status);

        Assert.IsType(expected, await Controller.AssessmentEntry(Posted(), "save", CancellationToken.None));
    }

    [Fact]
    public async Task AssessmentEntry_Post_NullModel_Throws() =>
        await Assert.ThrowsAsync<ArgumentNullException>(() => Controller.AssessmentEntry(null!, "save", CancellationToken.None));

    [Fact]
    public void Display_KeepsPostedValuesAndLinesTheCategoriesUpWithTheTest()
    {
        var model = Posted();
        model.Categories.Reverse();
        model.Categories.Add(new AssessorCategoryInput { CategoryItemId = Guid.NewGuid(), Score = 1 });

        model.Display(Entry());

        Assert.Equal([Morphology, Staining], model.Categories.Select(c => c.CategoryItemId));
        Assert.Equal([3, 5], model.Categories.Select(c => c.Score));
    }

    [Fact]
    public void Display_ACategoryThePostLeftOut_StartsUnscored()
    {
        var model = Posted();
        model.Categories.RemoveAt(1);

        model.Display(Entry());

        Assert.Equal(Staining, model.Categories[1].CategoryItemId);
        Assert.Null(model.Categories[1].Score);
        Assert.Empty(model.Categories[1].CriterionItemIds);
    }

    [Fact]
    public void ToRequest_AnUnchosenScoreIsZero()
    {
        var request = Posted(overall: null, morphologyScore: null).ToRequest();

        Assert.Equal(0, request.OverallScore);
        Assert.Equal(0, request.Categories[0].Score);
    }

    [Fact]
    public void ScoreOptions_UseTheLegacyWording()
    {
        Assert.Equal(["1 - Fail", "2 - Borderline Fail", "3 - Pass", "4 - Good", "5 - Excellent"], AssessorEntryViewModel.OverallScoreOptions.Select(o => o.Text));
        Assert.Equal(["1", "2", "3", "4", "5"], AssessorEntryViewModel.CategoryScoreOptions.Select(o => o.Value));
    }

    [Fact]
    public void From_NullEntry_Throws() =>
        Assert.Throws<ArgumentNullException>(() => AssessorEntryViewModel.From(null!, null));

    [Fact]
    public void Display_NullEntry_Throws() =>
        Assert.Throws<ArgumentNullException>(() => new AssessorEntryViewModel().Display(null!));

    // ---- Submit ---------------------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData(ConfirmationFlow.Cancelled)]
    public async Task Submit_NotYetConfirmed_AsksTheLegacyQuestionFirstAndSubmitsNothing(string? confirmation)
    {
        _apiClient.ItemList = new(AssessorApiStatus.Ok, ItemList(completed: true));

        var result = await Controller.Submit(TestId, 2021, confirmation, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal(ConfirmationFlow.ViewName, view.ViewName);
        var page = Assert.IsType<ConfirmationPageViewModel>(view.Model);
        Assert.Equal((AssessorController.SubmitQuestion, "Submit", false), (page.Title, page.ConfirmText, page.IsDestructive));
        Assert.Null(page.WarningText);
        Assert.NotNull(page.CancelUrl);
        Assert.Empty(_apiClient.Submitted);
    }

    [Fact]
    public async Task Submit_NotAllSamplesAssessed_WarnsWithTheLegacyNote()
    {
        _apiClient.ItemList = new(AssessorApiStatus.Ok, ItemList(completed: false));

        var result = await Controller.Submit(TestId, null, null, CancellationToken.None);

        var page = Assert.IsType<ConfirmationPageViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal((AssessorController.SubmitQuestion, AssessorController.IncompleteNote), (page.Title, page.WarningText));
        Assert.Equal("Note: Assessment has not been entered for all of the samples.", page.WarningText);
        Assert.Empty(_apiClient.Submitted);
    }

    [Theory]
    [InlineData(AssessorApiStatus.Forbidden, typeof(ForbidResult))]
    [InlineData(AssessorApiStatus.NotFound, typeof(NotFoundResult))]
    public async Task Submit_AskingForATestThatCannotBeOpened_IsRefusedOrNotFound(AssessorApiStatus status, Type expected)
    {
        _apiClient.ItemList = new(status);

        Assert.IsType(expected, await Controller.Submit(TestId, null, null, CancellationToken.None));
        Assert.Empty(_apiClient.Submitted);
    }

    [Fact]
    public async Task Submit_AskingAboutATestAlreadySubmitted_ReturnsToTheItemsWithAnError()
    {
        _apiClient.ItemList = new(AssessorApiStatus.Ok, ItemList(submitted: true));
        var controller = Controller;

        var result = await controller.Submit(TestId, 2021, null, CancellationToken.None);

        Assert.Equal(nameof(AssessorController.AssessmentItemList), Assert.IsType<RedirectToActionResult>(result).ActionName);
        var notification = controller.TempData.GetNotification();
        Assert.Equal((NotificationType.Error, AssessorController.SubmittedNotification), (notification?.Type, notification?.Message));
        Assert.Empty(_apiClient.Submitted);
    }

    [Fact]
    public async Task Submit_Confirmed_SubmitsAndReturnsToTheItemsOfTheSameTestAndYear()
    {
        var result = await Controller.Submit(TestId, 2021, ConfirmationFlow.Confirmed, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(AssessorController.AssessmentItemList), redirect.ActionName);
        Assert.Equal((TestId, 2021), ((Guid)redirect.RouteValues!["testId"]!, (int)redirect.RouteValues["year"]!));
        Assert.Equal([TestId], _apiClient.Submitted);
        Assert.Empty(_apiClient.RequestedItemLists);
    }

    [Fact]
    public async Task Submit_Confirmed_SubmittedByAnotherAssessorMeanwhile_ShowsAnError()
    {
        _apiClient.SubmitStatus = AssessorSubmitStatus.AlreadySubmitted;
        var controller = Controller;

        var result = await controller.Submit(TestId, null, ConfirmationFlow.Confirmed, CancellationToken.None);

        Assert.Equal(nameof(AssessorController.AssessmentItemList), Assert.IsType<RedirectToActionResult>(result).ActionName);
        Assert.Equal(NotificationType.Error, controller.TempData.GetNotification()?.Type);
    }

    [Theory]
    [InlineData(AssessorSubmitStatus.Forbidden, typeof(ForbidResult))]
    [InlineData(AssessorSubmitStatus.NotFound, typeof(NotFoundResult))]
    public async Task Submit_Confirmed_RefusedOrMissing_IsRefusedOrNotFound(AssessorSubmitStatus status, Type expected)
    {
        _apiClient.SubmitStatus = status;

        Assert.IsType(expected, await Controller.Submit(TestId, null, ConfirmationFlow.Confirmed, CancellationToken.None));
    }
}
