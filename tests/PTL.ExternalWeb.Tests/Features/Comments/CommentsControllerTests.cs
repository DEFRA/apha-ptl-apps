using Microsoft.AspNetCore.Mvc;
using PTL.ApiClient;
using PTL.ApiClient.Distribution;
using PTL.Contracts.Distribution;
using PTL.ExternalWeb.Features.Comments;

namespace PTL.ExternalWeb.Tests.Features.Comments;

public class CommentsControllerTests
{
    [Fact]
    public async Task Index_WithSearch_FiltersDistributionsByTitle()
    {
        var api = new FakeTestConsultantApiClient
        {
            Distributions =
                new TestConsultantDistributionsResponse(
                [
                    Distribution("D1a/S1 BT: Brucella"),
                    Distribution("D2a/S2 PTX: Salmonella")
                ])
        };
        var controller = new CommentsController(api);

        var result = await controller.Index("Brucella", CancellationToken.None);

        var model = Assert.IsType<CommentsListViewModel>(Assert.IsType<ViewResult>(result).Model);
        var row = Assert.Single(model.Distributions);
        Assert.Contains("Brucella", row.DistributionTitle, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Entry_Get_WhenTabulationLoads_ReturnsEntryViewModel()
    {
        var api = new FakeTestConsultantApiClient { Tabulation = Ok(Tabulation(submitted: false)) };
        var controller = new CommentsController(api);

        var result = await controller.Entry(Guid.NewGuid(), CancellationToken.None);

        var model = Assert.IsType<CommentsEntryViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal("Title", model.Title);
        Assert.False(model.IsSubmitted);
    }

    [Fact]
    public async Task Entry_Post_SubmitWithoutAcknowledgement_AddsValidationError_AndDoesNotSave()
    {
        var api = new FakeTestConsultantApiClient { Tabulation = Ok(Tabulation(submitted: false)) };
        var controller = new CommentsController(api);
        var schemeId = Guid.NewGuid();

        var result = await controller.Entry(schemeId, new CommentsEntryViewModel { Comments = "<p>Text</p>", Acknowledged = false }, "submit", CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.True(controller.ModelState.ContainsKey(nameof(CommentsEntryViewModel.Acknowledged)));
        Assert.Empty(api.SavedRequests);
    }

    [Fact]
    public async Task Entry_Post_Save_Success_RedirectsToIndex_AndSendsDraftRequest()
    {
        var api = new FakeTestConsultantApiClient
        {
            Tabulation = Ok(Tabulation(submitted: false)),
            SaveResult = new TestConsultantApiResult<TestConsultantTabulationSaveResult>(TestConsultantApiStatus.Ok, new TestConsultantTabulationSaveResult(true, false, new Dictionary<string, string>()))
        };
        var controller = new CommentsController(api);
        var schemeId = Guid.NewGuid();

        var result = await controller.Entry(schemeId, new CommentsEntryViewModel { Comments = "<p>Draft</p>", Acknowledged = false }, "save", CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(CommentsController.Index), redirect.ActionName);
        var request = Assert.Single(api.SavedRequests);
        Assert.False(request.Submit);
        Assert.Equal("<p>Draft</p>", request.Comments);
    }

    [Fact]
    public async Task Entry_Post_Cancel_RedirectsToIndex_WithoutLoadingOrSaving()
    {
        var api = new FakeTestConsultantApiClient { Tabulation = Ok(Tabulation(submitted: false)) };
        var controller = new CommentsController(api);

        var result = await controller.Entry(Guid.NewGuid(), new CommentsEntryViewModel(), "cancel", CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(CommentsController.Index), redirect.ActionName);
        Assert.Equal(0, api.GetTabulationCalls);
        Assert.Empty(api.SavedRequests);
    }

    [Fact]
    public async Task Entry_Post_SubmittedComments_AreLocked_AndNotSaved()
    {
        var api = new FakeTestConsultantApiClient { Tabulation = Ok(Tabulation(submitted: true)) };
        var controller = new CommentsController(api);

        var result = await controller.Entry(Guid.NewGuid(), new CommentsEntryViewModel { Comments = "<p>Edit</p>", Acknowledged = true }, "save", CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.Equal("These comments have already been submitted and can no longer be edited.", Assert.Single(controller.ModelState[string.Empty]!.Errors).ErrorMessage);
        Assert.Empty(api.SavedRequests);
    }

    [Fact]
    public async Task Entry_Post_ModelStateInvalid_ReturnsView_AndDoesNotSave()
    {
        var api = new FakeTestConsultantApiClient { Tabulation = Ok(Tabulation(submitted: false)) };
        var controller = new CommentsController(api);
        controller.ModelState.AddModelError(nameof(CommentsEntryViewModel.Comments), "Invalid");

        var result = await controller.Entry(Guid.NewGuid(), new CommentsEntryViewModel { Comments = "<p>Text</p>", Acknowledged = true }, "save", CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.Empty(api.SavedRequests);
    }

    [Fact]
    public async Task Entry_Post_SaveReturnsLocked_AddsLockedError()
    {
        var api = new FakeTestConsultantApiClient
        {
            Tabulation = Ok(Tabulation(submitted: false)),
            SaveResult = new TestConsultantApiResult<TestConsultantTabulationSaveResult>(
                TestConsultantApiStatus.Ok,
                new TestConsultantTabulationSaveResult(false, true, new Dictionary<string, string>()))
        };
        var controller = new CommentsController(api);

        var result = await controller.Entry(Guid.NewGuid(), new CommentsEntryViewModel { Comments = "<p>Text</p>", Acknowledged = true }, "save", CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.Equal("These comments have already been submitted and can no longer be edited.", Assert.Single(controller.ModelState[string.Empty]!.Errors).ErrorMessage);
    }

    [Fact]
    public async Task Entry_Post_SaveReturnsFieldErrors_MapsCommentsErrorToCommentsKey()
    {
        var api = new FakeTestConsultantApiClient
        {
            Tabulation = Ok(Tabulation(submitted: false)),
            SaveResult = new TestConsultantApiResult<TestConsultantTabulationSaveResult>(
                TestConsultantApiStatus.Ok,
                new TestConsultantTabulationSaveResult(false, false, new Dictionary<string, string>
                {
                    ["Comments"] = "Comments are invalid",
                    ["Other"] = "Other failure"
                }))
        };
        var controller = new CommentsController(api);

        var result = await controller.Entry(Guid.NewGuid(), new CommentsEntryViewModel { Comments = "<p>Text</p>", Acknowledged = true }, "save", CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.Equal("Comments are invalid", Assert.Single(controller.ModelState[nameof(CommentsEntryViewModel.Comments)]!.Errors).ErrorMessage);
        Assert.Equal("Other failure", Assert.Single(controller.ModelState[string.Empty]!.Errors).ErrorMessage);
    }

    [Fact]
    public async Task Entry_Get_NotFoundFromApi_ReturnsNotFound()
    {
        var api = new FakeTestConsultantApiClient
        {
            Tabulation = new TestConsultantApiResult<TestConsultantTabulationResponse>(TestConsultantApiStatus.NotFound)
        };
        var controller = new CommentsController(api);

        var result = await controller.Entry(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Entry_Post_ForbiddenFromApi_ReturnsForbid()
    {
        var api = new FakeTestConsultantApiClient
        {
            Tabulation = new TestConsultantApiResult<TestConsultantTabulationResponse>(TestConsultantApiStatus.Forbidden)
        };
        var controller = new CommentsController(api);

        var result = await controller.Entry(Guid.NewGuid(), new CommentsEntryViewModel { Acknowledged = true }, "save", CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
    }

    private static TestConsultantApiResult<TestConsultantTabulationResponse> Ok(TestConsultantTabulationResponse value) =>
        new(TestConsultantApiStatus.Ok, value);

    private static TestConsultantDistributionResponse Distribution(string title) =>
        new(Guid.NewGuid(), 2026, 6, title, new DateTime(2026, 6, 30), false, "Awaiting comments");

    private static TestConsultantTabulationResponse Tabulation(bool submitted) =>
        new(
            Guid.NewGuid(),
            "Title",
            "30 June 2026",
            "",
            "",
            HasMethodItems: false,
            SampleNumbers: [],
            ResultsTables: [],
            ResultsRatings: null,
            FullTables: [],
            FullRatings: null,
            ParticipantInformation: null,
            SampleInformation: [],
            Comments: new TestConsultantCommentsStateResponse(submitted, "Add your comments here:", "<p>Saved</p>", true));

    private sealed class FakeTestConsultantApiClient : ITestConsultantTabulationApiClient
    {
        public TestConsultantDistributionsResponse Distributions { get; set; } = new([]);
        public TestConsultantApiResult<TestConsultantTabulationResponse> Tabulation { get; set; } = new(TestConsultantApiStatus.NotFound);
        public TestConsultantApiResult<TestConsultantTabulationSaveResult> SaveResult { get; set; } =
            new(TestConsultantApiStatus.Ok, new TestConsultantTabulationSaveResult(true, false, new Dictionary<string, string>()));

        public List<TestConsultantTabulationSaveRequest> SavedRequests { get; } = [];

        public int GetTabulationCalls { get; private set; }

        public Task<TestConsultantDistributionsResponse> GetDistributionsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Distributions);

        public Task<TestConsultantApiResult<TestConsultantTabulationResponse>> GetTabulationAsync(
            Guid monthlyDistributionSchemeId, TestConsultantTabulationFilter filter, CancellationToken cancellationToken = default)
        {
            GetTabulationCalls++;
            return Task.FromResult(Tabulation);
        }

        public Task<TestConsultantApiResult<PTL.ApiClient.Distribution.TabulationAttachmentFile>> ExportTabulationAsync(
            Guid monthlyDistributionSchemeId, TestConsultantTabulationFilter filter, CancellationToken cancellationToken = default) =>
            Task.FromResult(new TestConsultantApiResult<PTL.ApiClient.Distribution.TabulationAttachmentFile>(TestConsultantApiStatus.NotFound));

        public Task<TestConsultantApiResult<TestConsultantTabulationSaveResult>> SaveCommentsAsync(
            Guid monthlyDistributionSchemeId, TestConsultantTabulationSaveRequest request, CancellationToken cancellationToken = default)
        {
            SavedRequests.Add(request);
            return Task.FromResult(SaveResult);
        }
    }
}
