using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using PTL.Contracts.Scheme;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Features.Scheme;

public class SchemeControllerTests
{
    private static PTL.InternalWeb.Features.Scheme.SchemeController CreateController(FakeSchemeApiClient apiClient, FakeLookupApiClient? lookupApiClient = null) =>
        new(apiClient, lookupApiClient ?? new FakeLookupApiClient(), NullLogger<PTL.InternalWeb.Features.Scheme.SchemeController>.Instance);

    private static SchemeResponse SampleScheme(Guid schemeId, Guid sharedId, bool isReadOnly = false) => new(
        schemeId, sharedId, DateTime.UtcNow.Year + 1, "PT1234", "Test Scheme", Guid.NewGuid(), Guid.NewGuid(), null,
        true, false, false, false, false, false, false, false, false, false, false, false, false,
        0, Guid.NewGuid(), 5, 12345, "UK", 10, string.Empty, false, null, null, string.Empty,
        false, false, false, false, false, false, false,
        "Biological samples", false, null, "Instructions", false, false, false,
        null, null, null, null, false, false, null, null, null, null, null,
        DateTime.UtcNow, isReadOnly);

    [Fact]
    public async Task Index_ReturnsViewWithSearchResults()
    {
        var sharedId = Guid.NewGuid();
        var schemeId = Guid.NewGuid();
        var apiClient = new FakeSchemeApiClient
        {
            SearchResponse = new SchemeSearchResponse([new SchemeSummaryResponse(sharedId, 2027, schemeId, "PT1234", "Test Scheme", null, null, null, null, null, null)], 1, 1, 20)
        };
        var controller = CreateController(apiClient);

        var result = await controller.Index(2027, null, 1, 20, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<PTL.InternalWeb.Features.Scheme.SchemeListViewModel>(view.Model);
        Assert.Single(model.Schemes);
        Assert.Equal(1, model.TotalCount);
    }

    [Fact]
    public async Task Details_UnknownScheme_ReturnsNotFound()
    {
        var controller = CreateController(new FakeSchemeApiClient { SchemeResponse = null });

        var result = await controller.Details(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Details_ExistingScheme_ReturnsViewWithModel()
    {
        var schemeId = Guid.NewGuid();
        var controller = CreateController(new FakeSchemeApiClient { SchemeResponse = SampleScheme(schemeId, Guid.NewGuid()) });

        var result = await controller.Details(schemeId, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal(schemeId, Assert.IsType<SchemeResponse>(view.Model).SchemeId);
    }

    [Fact]
    public async Task History_ReturnsViewWithHistory()
    {
        var sharedId = Guid.NewGuid();
        var apiClient = new FakeSchemeApiClient
        {
            HistoryResponse = [new SchemeHistoryResponse(Guid.NewGuid(), sharedId, 2027, "PT1234", "Test Scheme")]
        };
        var controller = CreateController(apiClient);

        var result = await controller.History(sharedId, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<IReadOnlyList<SchemeHistoryResponse>>(view.Model);
        Assert.Single(model);
    }

    [Fact]
    public async Task Create_Get_ReturnsFormViewModel()
    {
        var controller = CreateController(new FakeSchemeApiClient());

        var result = await controller.Create(CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.IsType<PTL.InternalWeb.Features.Scheme.SchemeFormViewModel>(view.Model);
    }

    [Fact]
    public async Task Create_Post_InvalidModelState_ReturnsViewWithModel()
    {
        var controller = CreateController(new FakeSchemeApiClient());
        controller.ModelState.AddModelError("Identifier", "Enter the scheme identifier.");
        var model = new PTL.InternalWeb.Features.Scheme.SchemeFormViewModel();

        var result = await controller.Create(model, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(model, view.Model);
    }

    [Fact]
    public async Task Create_Post_ApiFailure_AddsErrorsAndReturnsView()
    {
        var apiClient = new FakeSchemeApiClient
        {
            SaveResult = new SchemeSaveResult(false, null, new Dictionary<string, string[]> { ["Identifier"] = ["Identifier must match the format PT followed by 4 digits (e.g. PT1234)."] })
        };
        var controller = CreateController(apiClient);
        var model = new PTL.InternalWeb.Features.Scheme.SchemeFormViewModel { YearId = 2027 };

        var result = await controller.Create(model, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.Same(model, view.Model);
    }

    [Fact]
    public async Task Create_Post_Success_RedirectsToDetails()
    {
        var schemeId = Guid.NewGuid();
        var apiClient = new FakeSchemeApiClient
        {
            SaveResult = new SchemeSaveResult(true, SampleScheme(schemeId, Guid.NewGuid()), new Dictionary<string, string[]>())
        };
        var controller = CreateController(apiClient);
        var model = new PTL.InternalWeb.Features.Scheme.SchemeFormViewModel { YearId = 2027, Identifier = "PT1234", Name = "Test Scheme" };

        var result = await controller.Create(model, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Details", redirect.ActionName);
        Assert.Equal(schemeId, redirect.RouteValues!["id"]);
    }

    [Fact]
    public async Task Edit_Get_UnknownScheme_ReturnsNotFound()
    {
        var controller = CreateController(new FakeSchemeApiClient { SchemeResponse = null });

        var result = await controller.Edit(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Edit_Get_ExistingScheme_ReturnsFormViewModel()
    {
        var schemeId = Guid.NewGuid();
        var controller = CreateController(new FakeSchemeApiClient { SchemeResponse = SampleScheme(schemeId, Guid.NewGuid()) });

        var result = await controller.Edit(schemeId, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<PTL.InternalWeb.Features.Scheme.SchemeFormViewModel>(view.Model);
        Assert.Equal(schemeId, model.SchemeId);
    }

    [Fact]
    public async Task Edit_Post_Success_RedirectsToDetails()
    {
        var schemeId = Guid.NewGuid();
        var apiClient = new FakeSchemeApiClient
        {
            SaveResult = new SchemeSaveResult(true, SampleScheme(schemeId, Guid.NewGuid()), new Dictionary<string, string[]>())
        };
        var controller = CreateController(apiClient);
        var model = new PTL.InternalWeb.Features.Scheme.SchemeFormViewModel { YearId = 2027, Identifier = "PT1234", Name = "Test Scheme" };

        var result = await controller.Edit(schemeId, model, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Details", redirect.ActionName);
        Assert.Equal(schemeId, redirect.RouteValues!["id"]);
    }
}
