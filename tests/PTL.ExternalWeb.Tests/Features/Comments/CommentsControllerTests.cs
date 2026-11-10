using Microsoft.AspNetCore.Mvc;
using PTL.ApiClient;
using PTL.ApiClient.Distribution;
using PTL.Contracts.Distribution;
using PTL.ExternalWeb.Features.Comments;

namespace PTL.ExternalWeb.Tests.Features.Comments;

public class CommentsControllerTests
{
    [Fact]
    public async Task Index_WithSchemeNameFilter_FiltersDistributionsByTitleSchemeNamePart()
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

        var result = await controller.Index(null, null, "Brucella", null, null, CancellationToken.None);

        var model = Assert.IsType<CommentsListViewModel>(Assert.IsType<ViewResult>(result).Model);
        var row = Assert.Single(model.Distributions);
        Assert.Equal("D1a", row.DistributionReference);
        Assert.Equal("S1 BT", row.SchemeCode);
        Assert.Equal("Brucella", row.SchemeName);
    }

    [Fact]
    public async Task Index_WithoutSort_DefaultsToTargetDateDescending()
    {
        var api = new FakeTestConsultantApiClient
        {
            Distributions =
                new TestConsultantDistributionsResponse(
                [
                    new TestConsultantDistributionResponse(Guid.NewGuid(), 2026, 6, "B2/PT1: Alpha", new DateTime(2026, 7, 14), false, "Awaiting comments"),
                    new TestConsultantDistributionResponse(Guid.NewGuid(), 2026, 6, "A1/PT9: Zulu", new DateTime(2026, 7, 14), false, "Submitted"),
                    new TestConsultantDistributionResponse(Guid.NewGuid(), 2026, 6, "C3/PT2: Beta", new DateTime(2026, 1, 13), false, "Submitted")
                ])
        };
        var controller = new CommentsController(api);

        var result = await controller.Index(null, null, null, null, null, CancellationToken.None);

        var model = Assert.IsType<CommentsListViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal("targetDate", model.SortBy);
        Assert.Equal("desc", model.SortDirection);
        Assert.Equal("A1,B2,C3", string.Join(",", model.Distributions.Select(d => d.DistributionReference)));
    }

    [Theory]
    [InlineData("reference", "asc", "A1,B2")]
    [InlineData("reference", "desc", "B2,A1")]
    [InlineData("schemeCode", "asc", "B2,A1")]
    [InlineData("schemeName", "asc", "B2,A1")]
    [InlineData("targetDate", "asc", "B2,A1")]
    [InlineData("commentsProvidedBy", "asc", "B2,A1")]
    [InlineData("submitted", "asc", "B2,A1")]
    public async Task Index_WithSort_SortsDistributions(string sortBy, string sortDir, string expectedOrder)
    {
        var api = new FakeTestConsultantApiClient
        {
            Distributions =
                new TestConsultantDistributionsResponse(
                [
                    new TestConsultantDistributionResponse(Guid.NewGuid(), 2026, 6, "A1/PT9: Zulu", new DateTime(2026, 6, 30), false, "Submitted"),
                    new TestConsultantDistributionResponse(Guid.NewGuid(), 2026, 6, "B2/PT1: Alpha", new DateTime(2026, 5, 30), false, "Awaiting comments")
                ])
        };
        var controller = new CommentsController(api);

        var result = await controller.Index(null, null, null, sortBy, sortDir, CancellationToken.None);

        var model = Assert.IsType<CommentsListViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(expectedOrder, string.Join(",", model.Distributions.Select(d => d.DistributionReference)));
    }

    [Fact]
    public async Task Entry_Get_WhenTabulationLoads_ReturnsEntryViewModel()
    {
        var api = new FakeTestConsultantApiClient { Tabulation = Ok(Tabulation(submitted: false)) };
        var controller = new CommentsController(api);

        var result = await controller.Entry(Guid.NewGuid(), "/Comments/Index?schemeName=Brucella", CancellationToken.None);

        var model = Assert.IsType<CommentsEntryViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal("Title", model.Title);
        Assert.False(model.IsSubmitted);
        Assert.Equal("/Comments/Index?schemeName=Brucella", model.ReturnUrl);
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
    public async Task Entry_Post_Save_Success_RedirectsToReturnUrl_AndSendsDraftRequest()
    {
        var api = new FakeTestConsultantApiClient
        {
            Tabulation = Ok(Tabulation(submitted: false)),
            SaveResult = new TestConsultantApiResult<TestConsultantTabulationSaveResult>(TestConsultantApiStatus.Ok, new TestConsultantTabulationSaveResult(true, false, new Dictionary<string, string>()))
        };
        var controller = new CommentsController(api);
        var schemeId = Guid.NewGuid();

        var result = await controller.Entry(schemeId, new CommentsEntryViewModel
        {
            Comments = "<p>Draft</p>",
            Acknowledged = false,
            ReturnUrl = "/Comments/Index?schemeName=Brucella&sortBy=schemeName&sortDir=asc"
        }, "save", CancellationToken.None);

        var redirect = Assert.IsType<LocalRedirectResult>(result);
        Assert.Equal("/Comments/Index?schemeName=Brucella&sortBy=schemeName&sortDir=asc", redirect.Url);
        var request = Assert.Single(api.SavedRequests);
        Assert.False(request.Submit);
        Assert.Equal("<p>Draft</p>", request.Comments);
    }

    [Fact]
    public async Task Entry_Post_SaveApiFailure_ReturnsNotFound()
    {
        var api = new FakeTestConsultantApiClient
        {
            Tabulation = Ok(Tabulation(submitted: false)),
            SaveResult = new TestConsultantApiResult<TestConsultantTabulationSaveResult>(TestConsultantApiStatus.NotFound)
        };
        var controller = new CommentsController(api);

        var result = await controller.Entry(Guid.NewGuid(), new CommentsEntryViewModel { Comments = "<p>Draft</p>", Acknowledged = true }, "save", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Entry_Post_Cancel_RedirectsToReturnUrl_WithoutLoadingOrSaving()
    {
        var api = new FakeTestConsultantApiClient { Tabulation = Ok(Tabulation(submitted: false)) };
        var controller = new CommentsController(api);

        var result = await controller.Entry(Guid.NewGuid(), new CommentsEntryViewModel
        {
            ReturnUrl = "/Comments/Index?distributionReference=D1a"
        }, "cancel", CancellationToken.None);

        var redirect = Assert.IsType<LocalRedirectResult>(result);
        Assert.Equal("/Comments/Index?distributionReference=D1a", redirect.Url);
        Assert.Equal(0, api.GetTabulationCalls);
        Assert.Empty(api.SavedRequests);
        Assert.Empty(api.ExportedRequests);
    }

    [Fact]
    public async Task Entry_Post_Draw_UsesFilterAndRedisplaysWithoutSaving()
    {
        var tabulation = Tabulation(submitted: false) with { SampleNumbers = ["26/1", "26/2"], HasMethodItems = true };
        var api = new FakeTestConsultantApiClient { Tabulation = Ok(tabulation) };
        var controller = new CommentsController(api);

        var result = await controller.Entry(Guid.NewGuid(), new CommentsEntryViewModel
        {
            Comments = "<p>Edit</p>",
            IsFiltered = true,
            AllSamples = ["26/1", "26/2"],
            ShownSamples = ["26/1"],
            ShowMethodItems = false,
            ReturnUrl = "/Comments/Index?schemeName=Brucella"
        }, "draw", CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<CommentsEntryViewModel>(view.Model);
        Assert.Equal("<p>Edit</p>", model.Comments);
        Assert.Equal("/Comments/Index?schemeName=Brucella", model.ReturnUrl);
        Assert.Equal("/Comments/Index?schemeName=Brucella", controller.ViewData["ListReturnUrl"]);
        Assert.Empty(api.SavedRequests);
        var filter = Assert.Single(api.GetTabulationFilters);
        Assert.Equal(["26/2"], filter.HiddenSamples);
        Assert.True(filter.HideMethodItems);
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
    public async Task Export_ReturnsFile_WhenApiReturnsAttachment()
    {
        var api = new FakeTestConsultantApiClient
        {
            ExportResult = new TestConsultantApiResult<TabulationAttachmentFile>(
                TestConsultantApiStatus.Ok,
                new TabulationAttachmentFile([1, 2, 3], "application/vnd.ms-excel", "attachment; filename=test.xlsx"))
        };
        var controller = new CommentsController(api)
        {
            ControllerContext = new ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() }
        };

        var result = await controller.Export(Guid.NewGuid(), new CommentsEntryViewModel
        {
            IsFiltered = true,
            AllSamples = ["26/1", "26/2"],
            ShownSamples = ["26/2"],
            ShowMethodItems = false
        }, CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/vnd.ms-excel", file.ContentType);
        Assert.Equal(new byte[] { 1, 2, 3 }, file.FileContents);
        var filter = Assert.Single(api.ExportedRequests);
        Assert.Equal(["26/1"], filter.HiddenSamples);
        Assert.True(filter.HideMethodItems);
    }

    [Fact]
    public async Task Entry_Get_NotFoundFromApi_ReturnsNotFound()
    {
        var api = new FakeTestConsultantApiClient
        {
            Tabulation = new TestConsultantApiResult<TestConsultantTabulationResponse>(TestConsultantApiStatus.NotFound)
        };
        var controller = new CommentsController(api);

        var result = await controller.Entry(Guid.NewGuid(), null, CancellationToken.None);

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

    [Fact]
    public async Task Export_WhenApiReturnsNotFound_ReturnsNotFound()
    {
        var api = new FakeTestConsultantApiClient
        {
            ExportResult = new TestConsultantApiResult<TabulationAttachmentFile>(TestConsultantApiStatus.NotFound)
        };
        var controller = new CommentsController(api);

        var result = await controller.Export(Guid.NewGuid(), new CommentsEntryViewModel(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Index_WithUnparseableTitle_UsesFallbackValues()
    {
        var api = new FakeTestConsultantApiClient
        {
            Distributions = new TestConsultantDistributionsResponse(
            [
                new TestConsultantDistributionResponse(Guid.NewGuid(), 2026, 6, "Unparseable title", new DateTime(2026, 6, 30), false, "Awaiting comments", "")
            ])
        };
        var controller = new CommentsController(api);

        var result = await controller.Index(null, null, "unparseable", null, null, CancellationToken.None);

        var model = Assert.IsType<CommentsListViewModel>(Assert.IsType<ViewResult>(result).Model);
        var row = Assert.Single(model.Distributions);
        Assert.Equal("Unparseable title", row.DistributionReference);
        Assert.Equal(string.Empty, row.SchemeCode);
        Assert.Equal("Unparseable title", row.SchemeName);
    }

    [Fact]
    public async Task Index_WhenCommentsProvidedByAlreadySet_DoesNotLoadEntryTabulation()
    {
        var api = new FakeTestConsultantApiClient
        {
            Distributions = new TestConsultantDistributionsResponse(
            [
                new TestConsultantDistributionResponse(Guid.NewGuid(), 2026, 6, "D1a/S1 BT: Brucella", new DateTime(2026, 6, 30), false, "Comments submitted", "Dr Who")
            ])
        };
        var controller = new CommentsController(api);

        var result = await controller.Index(null, null, null, null, null, CancellationToken.None);

        var model = Assert.IsType<CommentsListViewModel>(Assert.IsType<ViewResult>(result).Model);
        var row = Assert.Single(model.Distributions);
        Assert.Equal("Dr Who", row.CommentsProvidedBy);
        Assert.Equal("Comments submitted", row.CommentStatus);
        Assert.Equal(0, api.GetTabulationCalls);
    }

    private static TestConsultantApiResult<TestConsultantTabulationResponse> Ok(TestConsultantTabulationResponse value) =>
        new(TestConsultantApiStatus.Ok, value);

    private static TestConsultantDistributionResponse Distribution(string title) =>
        new(Guid.NewGuid(), 2026, 6, title, new DateTime(2026, 6, 30), false, "Awaiting comments", "");

    [Fact]
    public async Task Index_WithDistributionReferenceAndSchemeCode_FiltersByParsedFields()
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

        var result = await controller.Index("D2a", "S2 PTX", null, null, null, CancellationToken.None);

        var model = Assert.IsType<CommentsListViewModel>(Assert.IsType<ViewResult>(result).Model);
        var row = Assert.Single(model.Distributions);
        Assert.Equal("D2a", row.DistributionReference);
        Assert.Equal("S2 PTX", row.SchemeCode);
        Assert.Equal("Salmonella", row.SchemeName);
    }

    [Fact]
    public async Task Index_WhenCommentsProvidedByMissing_PopulatesFromEntryHeading()
    {
        var schemeId = Guid.NewGuid();
        var api = new FakeTestConsultantApiClient
        {
            Distributions = new TestConsultantDistributionsResponse(
            [
                new TestConsultantDistributionResponse(
                    schemeId,
                    2026,
                    6,
                    "D1a/S1 BT: Brucella",
                    new DateTime(2026, 6, 30),
                    true,
                    "Comments submitted",
                    "")
            ]),
            Tabulation = Ok(Tabulation(submitted: true) with
            {
                Comments = new TestConsultantCommentsStateResponse(true, "Comments provided by External Test Consultant Name 14:", "<p>Saved</p>", true)
            })
        };
        var controller = new CommentsController(api);

        var result = await controller.Index(null, null, null, null, null, CancellationToken.None);

        var model = Assert.IsType<CommentsListViewModel>(Assert.IsType<ViewResult>(result).Model);
        var row = Assert.Single(model.Distributions);
        Assert.Equal("External Test Consultant Name 14", row.CommentsProvidedBy);
    }

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
        public TestConsultantApiResult<TabulationAttachmentFile> ExportResult { get; set; } = new(TestConsultantApiStatus.NotFound);
        public TestConsultantApiResult<TestConsultantTabulationSaveResult> SaveResult { get; set; } =
            new(TestConsultantApiStatus.Ok, new TestConsultantTabulationSaveResult(true, false, new Dictionary<string, string>()));

        public List<TestConsultantTabulationSaveRequest> SavedRequests { get; } = [];
        public List<TestConsultantTabulationFilter> GetTabulationFilters { get; } = [];
        public List<TestConsultantTabulationFilter> ExportedRequests { get; } = [];

        public int GetTabulationCalls { get; private set; }

        public Task<TestConsultantDistributionsResponse> GetDistributionsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Distributions);

        public Task<TestConsultantApiResult<TestConsultantTabulationResponse>> GetTabulationAsync(
            Guid monthlyDistributionSchemeId, TestConsultantTabulationFilter filter, CancellationToken cancellationToken = default)
        {
            GetTabulationCalls++;
            GetTabulationFilters.Add(filter);
            return Task.FromResult(Tabulation);
        }

        public Task<TestConsultantApiResult<PTL.ApiClient.Distribution.TabulationAttachmentFile>> ExportTabulationAsync(
            Guid monthlyDistributionSchemeId, TestConsultantTabulationFilter filter, CancellationToken cancellationToken = default)
        {
            ExportedRequests.Add(filter);
            return Task.FromResult(ExportResult);
        }

        public Task<TestConsultantApiResult<TestConsultantTabulationSaveResult>> SaveCommentsAsync(
            Guid monthlyDistributionSchemeId, TestConsultantTabulationSaveRequest request, CancellationToken cancellationToken = default)
        {
            SavedRequests.Add(request);
            return Task.FromResult(SaveResult);
        }
    }
}
