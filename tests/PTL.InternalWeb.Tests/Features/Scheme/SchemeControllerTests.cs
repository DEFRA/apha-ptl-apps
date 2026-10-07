using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using PTL.Contracts.Lookup;
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
    public async Task Index_ReturnsEverySchemeFamily()
    {
        var sharedId = Guid.NewGuid();
        var schemeId = Guid.NewGuid();
        var apiClient = new FakeSchemeApiClient
        {
            SearchResponse = new SchemeSearchResponse([new SchemeSummaryResponse(sharedId, 2027, schemeId, "PT1234", "Test Scheme", null, null, null, null, null, null, "PT1234", "Test Scheme")], 1, 1, 15)
        };
        var controller = CreateController(apiClient);

        var result = await controller.Index(1, 15, null, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<PTL.InternalWeb.Features.Scheme.SchemeListViewModel>(view.Model);
        var scheme = Assert.Single(model.Schemes);
        Assert.Equal("PT1234", scheme.Identifier);
        Assert.Equal(1, model.TotalCount);
        Assert.Equal(15, model.PageSize);
    }

    [Fact]
    public async Task PrintableSchemes_ReturnsEverySchemeFamilyWithNoSearchTerm()
    {
        var sharedId = Guid.NewGuid();
        var schemeId = Guid.NewGuid();
        var apiClient = new FakeSchemeApiClient
        {
            SearchResponse = new SchemeSearchResponse([new SchemeSummaryResponse(sharedId, 2027, schemeId, "PT1234", "Test Scheme", null, null, null, null, null, null, "PT1234", "Test Scheme")], 1, 1, 25)
        };
        var controller = CreateController(apiClient);

        var result = await controller.PrintableSchemes(1, 25, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<PTL.InternalWeb.Features.Scheme.SchemeListViewModel>(view.Model);
        Assert.Null(model.SearchTerm);
        Assert.Single(model.Schemes);
    }

    [Fact]
    public async Task PrintableScheme_UnknownScheme_ReturnsNotFound()
    {
        var controller = CreateController(new FakeSchemeApiClient { SchemeResponse = null });

        var result = await controller.PrintableScheme(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task PrintableScheme_ExistingScheme_ReturnsWorksheetModel()
    {
        var schemeId = Guid.NewGuid();
        var controller = CreateController(new FakeSchemeApiClient { SchemeResponse = SampleScheme(schemeId, Guid.NewGuid()) });

        var result = await controller.PrintableScheme(schemeId, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<PTL.InternalWeb.Features.Scheme.PrintableSchemeWorksheet>(view.Model);
        Assert.Equal("PT1234: Test Scheme", model.SchemeHeading);
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
        var model = Assert.IsType<PTL.InternalWeb.Features.Scheme.SchemeFormViewModel>(view.Model);
        Assert.Equal(schemeId, model.SchemeId);
        Assert.True(model.IsViewMode);
    }

    [Fact]
    public async Task Renew_UnknownScheme_ReturnsNotFound()
    {
        var controller = CreateController(new FakeSchemeApiClient { RenewResponse = null });

        var result = await controller.Renew(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Renew_ExistingScheme_ReturnsCreateViewWithDraftKeepingTheFamilyButNoSchemeId()
    {
        var sharedId = Guid.NewGuid();
        var renewed = SampleScheme(Guid.Empty, sharedId) with { YearId = 2028 };
        var controller = CreateController(new FakeSchemeApiClient { RenewResponse = renewed });

        var result = await controller.Renew(Guid.NewGuid(), CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("Create", view.ViewName);
        var model = Assert.IsType<PTL.InternalWeb.Features.Scheme.SchemeFormViewModel>(view.Model);
        Assert.Null(model.SchemeId);
        Assert.Equal(sharedId, model.SharedId);
        Assert.Equal(2028, model.YearId);
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
        var model = Assert.IsType<IReadOnlyList<PTL.InternalWeb.Features.Scheme.SchemeHistoryRowViewModel>>(view.Model, exactMatch: false);
        Assert.Single(model);
    }

    [Fact]
    public async Task History_FormatsYearAsTheLegacyFinancialYearLabel()
    {
        var sharedId = Guid.NewGuid();
        var apiClient = new FakeSchemeApiClient
        {
            HistoryResponse = [new SchemeHistoryResponse(Guid.NewGuid(), sharedId, 2026, "PT1234", "Test Scheme")]
        };
        var lookupApiClient = new FakeLookupApiClient { AllYears = [new YearResponse(2026, "2026/27")] };
        var controller = CreateController(apiClient, lookupApiClient);

        var result = await controller.History(sharedId, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<IReadOnlyList<PTL.InternalWeb.Features.Scheme.SchemeHistoryRowViewModel>>(view.Model, exactMatch: false);
        Assert.Equal("2026/27", Assert.Single(model).YearLabel);
    }

    [Fact]
    public async Task Create_Get_ReturnsFormViewModel()
    {
        var controller = CreateController(new FakeSchemeApiClient());

        var result = await controller.Create(cancellationToken: CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.IsType<PTL.InternalWeb.Features.Scheme.SchemeFormViewModel>(view.Model);
    }

    [Fact]
    public async Task Create_Get_DefaultsToNextYearAndDerivesStartDate()
    {
        var lookup = new FakeLookupApiClient
        {
            Years = [new PTL.Contracts.Lookup.YearResponse(2025, "2025/26"), new PTL.Contracts.Lookup.YearResponse(2026, "2026/27")],
            AllYears = [new PTL.Contracts.Lookup.YearResponse(2025, "2025/26"), new PTL.Contracts.Lookup.YearResponse(2026, "2026/27")],
            SystemSettings = new PTL.Contracts.Lookup.SystemSettingsResponse(string.Empty, new DateTime(2025, 4, 1))
        };
        var controller = CreateController(new FakeSchemeApiClient(), lookup);

        var result = await controller.Create(cancellationToken: CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<PTL.InternalWeb.Features.Scheme.SchemeFormViewModel>(view.Model);
        Assert.Equal(2026, model.YearId);
        Assert.Equal("2026/27", model.YearLabel);
        Assert.Equal(new DateTime(2026, 4, 1), model.StartDate);
    }

    [Fact]
    public async Task Create_Get_LocksMonthsWhoseDistributionIsAlreadyInitialised()
    {
        var lookup = new FakeLookupApiClient
        {
            Years = [new PTL.Contracts.Lookup.YearResponse(2025, "2025/26"), new PTL.Contracts.Lookup.YearResponse(2026, "2026/27")],
            SchemeMonthEditability = new PTL.Contracts.Scheme.SchemeMonthEditabilityResponse(
                Jan: true, Feb: true, Mar: true, Apr: true, May: true, Jun: false,
                Jul: true, Aug: true, Sep: true, Oct: true, Nov: true, Dec: true)
        };
        var controller = CreateController(new FakeSchemeApiClient(), lookup);

        var result = await controller.Create(cancellationToken: CancellationToken.None);

        var model = Assert.IsType<PTL.InternalWeb.Features.Scheme.SchemeFormViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.False(model.CanEditJun);
        Assert.True(model.CanEditApr);
    }

    [Fact]
    public async Task CreateForCurrentYear_Get_DefaultsToCurrentYearAndRendersTheCreateView()
    {
        var lookup = new FakeLookupApiClient
        {
            Years = [new PTL.Contracts.Lookup.YearResponse(2025, "2025/26"), new PTL.Contracts.Lookup.YearResponse(2026, "2026/27")],
            AllYears = [new PTL.Contracts.Lookup.YearResponse(2025, "2025/26"), new PTL.Contracts.Lookup.YearResponse(2026, "2026/27")],
            SystemSettings = new PTL.Contracts.Lookup.SystemSettingsResponse(string.Empty, new DateTime(2025, 4, 1))
        };
        var controller = CreateController(new FakeSchemeApiClient(), lookup);

        var result = await controller.CreateForCurrentYear(CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<PTL.InternalWeb.Features.Scheme.SchemeFormViewModel>(view.Model);
        Assert.Equal(2025, model.YearId);
        Assert.Equal("2025/26", model.YearLabel);

        // Both entry points share the one Create form, as both legacy menu items share Scheme.aspx.
        Assert.Equal("Create", view.ViewName);
    }

    [Fact]
    public async Task Edit_Get_KeepsSampleNoSequenceAndDerivesStartDate()
    {
        var schemeId = Guid.NewGuid();
        var scheduleId = Guid.NewGuid();
        var scheme = SampleScheme(schemeId, Guid.NewGuid()) with { YearId = 2026, ScheduleId = scheduleId, SampleNoSequence = 7 };
        var lookup = new FakeLookupApiClient
        {
            Years = [new PTL.Contracts.Lookup.YearResponse(2025, "2025/26"), new PTL.Contracts.Lookup.YearResponse(2026, "2026/27")],
            SystemSettings = new PTL.Contracts.Lookup.SystemSettingsResponse(string.Empty, new DateTime(2025, 4, 1)),
            Schedules = [new PTL.Contracts.Lookup.ScheduleResponse(scheduleId, "Routine")]
        };
        var controller = CreateController(new FakeSchemeApiClient { SchemeResponse = scheme }, lookup);

        var result = await controller.Edit(schemeId, CancellationToken.None);

        var model = Assert.IsType<PTL.InternalWeb.Features.Scheme.SchemeFormViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(7, model.SampleNoSequence);

        // SampleScheme selects April only, and April is on or after the contract start month.
        Assert.Equal(new DateTime(2026, 4, 1), model.StartDate);
    }

    [Fact]
    public async Task Create_Post_InvalidModelState_ReturnsViewWithModel()
    {
        var controller = CreateController(new FakeSchemeApiClient());
        controller.ModelState.AddModelError("Identifier", "Enter the scheme identifier.");
        var model = new PTL.InternalWeb.Features.Scheme.SchemeFormViewModel();

        var result = await controller.Create(model, cancellationToken: CancellationToken.None);

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

        var result = await controller.Create(model, cancellationToken: CancellationToken.None);

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

        var result = await controller.Create(model, cancellationToken: CancellationToken.None);

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

        var result = await controller.Edit(schemeId, model, cancellationToken: CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Details", redirect.ActionName);
        Assert.Equal(schemeId, redirect.RouteValues!["id"]);
    }

    [Fact]
    public void ManageSchemes_ReturnsView()
    {
        var controller = CreateController(new FakeSchemeApiClient());

        var result = controller.ManageSchemes();

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public async Task Create_Post_SuccessWithoutSchemePayload_ReturnsViewWithGenericError()
    {
        var apiClient = new FakeSchemeApiClient
        {
            SaveResult = new SchemeSaveResult(true, null, new Dictionary<string, string[]>())
        };
        var controller = CreateController(apiClient);
        var model = new PTL.InternalWeb.Features.Scheme.SchemeFormViewModel { YearId = 2027, Identifier = "PT1234", Name = "Test Scheme" };

        var result = await controller.Create(model, cancellationToken: CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(model, view.Model);
        Assert.Contains(
            controller.ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage),
            message => message == "The scheme could not be created.");
    }

    [Fact]
    public async Task Edit_Post_InvalidModelState_ReturnsViewWithModel()
    {
        var controller = CreateController(new FakeSchemeApiClient());
        controller.ModelState.AddModelError("Identifier", "Enter an identifier");
        var model = new PTL.InternalWeb.Features.Scheme.SchemeFormViewModel();

        var result = await controller.Edit(Guid.NewGuid(), model, cancellationToken: CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(model, view.Model);
    }

    [Fact]
    public async Task Edit_Post_ApiFailure_AddsErrorsAndReturnsView()
    {
        var apiClient = new FakeSchemeApiClient
        {
            SaveResult = new SchemeSaveResult(false, null, new Dictionary<string, string[]> { ["Identifier"] = ["Identifier is required"] })
        };
        var controller = CreateController(apiClient);
        var model = new PTL.InternalWeb.Features.Scheme.SchemeFormViewModel { YearId = 2027, Identifier = "PT1234", Name = "Test Scheme" };

        var result = await controller.Edit(Guid.NewGuid(), model, cancellationToken: CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(model, view.Model);
        Assert.False(controller.ModelState.IsValid);
    }
}
