using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using PTL.ApiClient;
using PTL.Contracts.Lookup;
using PTL.Contracts.Scheme;
using PTL.InternalWeb.AuditReason;
using PTL.InternalWeb.Confirmation;
using PTL.InternalWeb.Features.Scheme;
using PTL.InternalWeb.Tests.TestSupport;
using PTL.SharedUI.Notifications;

namespace PTL.InternalWeb.Tests.Features.Scheme;

// Previous Year Scheme Edit - Legacy Parity. A scheme from a closed year opens in the same form as any other, mostly
// locked, and its Apply button saves and stays on the scheme where Save would return to the list.
public partial class SchemeControllerTests
{
    private const string PreviousYearViewName = "PreviousYear";
    private const string UpdatedMessage = "Scheme updated successfully.";
    private const int ClosedYearId = 2025;

    // FakeSchemeApiClient keeps neither the route id nor the token of an update, so these tests drive the controller through this spy.
    private sealed class UpdateSpySchemeApiClient : ISchemeApiClient
    {
        public SchemeSaveResult SaveResult { get; set; } = new(true, null, new Dictionary<string, string[]>());

        public List<(Guid SchemeId, SchemeRequest Request, CancellationToken Token)> Updates { get; } = [];

        public Task<SchemeSaveResult> UpdateSchemeAsync(Guid schemeId, SchemeRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Updates.Add((schemeId, request, cancellationToken));
            return Task.FromResult(SaveResult);
        }

        public Task<SchemeResponse?> GetSchemeAsync(Guid schemeId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<SchemeSearchResponse> GetSchemesForYearAsync(SchemeSearchRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<SchemeSearchResponse> GetSchemeFamiliesAsync(int page, int pageSize, string? searchTerm = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<SchemeHistoryResponse>> GetSchemeHistoryAsync(Guid sharedId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<ParticipantSchemeListResponse> GetSchemesForParticipantAsync(Guid participantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<SchemeResponse?> RenewSchemeAsync(Guid schemeId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<SchemeSaveResult> CreateSchemeAsync(SchemeRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private static SchemeController SpyPostingController(UpdateSpySchemeApiClient apiClient, FakeLookupApiClient? lookupApiClient = null, (string Key, string Value)[]? fields = null)
    {
        var controller = new SchemeController(apiClient, lookupApiClient ?? new FakeLookupApiClient(), NullLogger<SchemeController>.Instance);
        var context = FormContext(fields ?? []);
        context.HttpContext.Request.PathBase = "/internal";
        context.HttpContext.Request.Path = "/Scheme/Apply/1";
        controller.ControllerContext = context;
        controller.Url = new FakeUrlHelper();
        controller.TempData = new TempDataDictionary(context.HttpContext, new FakeTempDataProvider());
        return controller;
    }

    // The form never posts IsReadOnly, so it arrives null.
    private static SchemeFormViewModel PreviousYearModel(bool? isReadOnly = null) => new()
    {
        SchemeId = Guid.NewGuid(),
        SharedId = Guid.NewGuid(),
        IsReadOnly = isReadOnly,
        YearId = ClosedYearId,
        Identifier = "PT1234",
        Name = "Test Scheme",
        CustomsVolume = "20 ml",
        CustomsDescription = "Biological samples",
        Instructions = "<p>Follow the packing instructions.</p>",
    };

    private static FakeLookupApiClient LookupsWithOneOfEach() => new()
    {
        AllYears = [new YearResponse(ClosedYearId, "2025/26")],
        Schedules = [new ScheduleResponse(Guid.NewGuid(), "Routine")],
        ScheduleCodes = [new ScheduleCodeResponse(Guid.NewGuid(), "Code A")],
        Days = [new DayResponse(Guid.NewGuid(), "Monday")],
        SchemeItemTypes = [new SchemeItemTypeResponse(Guid.NewGuid(), "Chemical", false)],
        Currencies = [new CurrencyResponse(Guid.NewGuid(), "British Pound", "GBP", "GBP - British Pound")],
    };

    // A redisplay that skipped these lists would come back with empty dropdowns and no pricing grid.
    // A closed-year scheme lists only the currencies it already has a price for, and these fixtures hold none.
    private static void AssertOptionListsPopulated(SchemeFormViewModel model)
    {
        Assert.Equal("2025/26", model.YearLabel);
        Assert.Equal("Routine", Assert.Single(model.ScheduleOptions).Text);
        Assert.Equal("Code A", Assert.Single(model.ScheduleCodeOptions).Text);
        Assert.Equal("Monday", Assert.Single(model.DayOfWeekOptions).Text);
        Assert.Equal("Chemical", Assert.Single(model.TestTypeOptions).Text);
        if (model.IsPreviousYear)
        {
            Assert.Empty(model.Prices);
        }
        else
        {
            Assert.Equal("British Pound", Assert.Single(model.Prices).CurrencyName);
        }
    }

    [Fact]
    public async Task Details_PreviousYearScheme_OpensTheReadOnlyViewPageLikeAnyOtherScheme()
    {
        var schemeId = Guid.NewGuid();
        var scheme = SampleScheme(schemeId, Guid.NewGuid(), isReadOnly: true) with { YearId = ClosedYearId };
        var controller = CreateController(new FakeSchemeApiClient { SchemeResponse = scheme }, LookupsWithOneOfEach());

        var result = await controller.Details(schemeId, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Null(view.ViewName);
        var model = Assert.IsType<SchemeFormViewModel>(view.Model);
        Assert.Equal(schemeId, model.SchemeId);
        Assert.True(model.IsViewMode);
        Assert.True(model.IsReadOnly);
        Assert.False(model.IsPreviousYear);
        AssertOptionListsPopulated(model);
    }

    [Fact]
    public async Task Details_CurrentYearScheme_StillOpensTheViewPageInTheDefaultView()
    {
        var schemeId = Guid.NewGuid();
        var scheme = SampleScheme(schemeId, Guid.NewGuid(), isReadOnly: false) with { YearId = ClosedYearId };
        var controller = CreateController(new FakeSchemeApiClient { SchemeResponse = scheme }, LookupsWithOneOfEach());

        var result = await controller.Details(schemeId, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Null(view.ViewName);
        var model = Assert.IsType<SchemeFormViewModel>(view.Model);
        Assert.True(model.IsViewMode);
        Assert.False(model.IsReadOnly);
        Assert.False(model.IsPreviousYear);
        AssertOptionListsPopulated(model);
    }

    [Fact]
    public async Task Edit_Get_PreviousYearScheme_OpensTheLimitedEditFormEvenFromTheEditUrl()
    {
        var schemeId = Guid.NewGuid();
        var scheme = SampleScheme(schemeId, Guid.NewGuid(), isReadOnly: true) with { YearId = ClosedYearId };
        var controller = CreateController(new FakeSchemeApiClient { SchemeResponse = scheme }, LookupsWithOneOfEach());

        var result = await controller.Edit(schemeId, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal(PreviousYearViewName, view.ViewName);
        var model = Assert.IsType<SchemeFormViewModel>(view.Model);
        Assert.Equal(schemeId, model.SchemeId);
        Assert.False(model.IsViewMode);
        Assert.True(model.IsPreviousYear);
        AssertOptionListsPopulated(model);
    }

    [Fact]
    public async Task Edit_Get_CurrentYearScheme_StillOpensTheFullyEditableFormInTheDefaultView()
    {
        var schemeId = Guid.NewGuid();
        var scheme = SampleScheme(schemeId, Guid.NewGuid(), isReadOnly: false) with { YearId = ClosedYearId };
        var controller = CreateController(new FakeSchemeApiClient { SchemeResponse = scheme }, LookupsWithOneOfEach());

        var result = await controller.Edit(schemeId, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Null(view.ViewName);
        var model = Assert.IsType<SchemeFormViewModel>(view.Model);
        Assert.False(model.IsViewMode);
        Assert.False(model.IsPreviousYear);
        AssertOptionListsPopulated(model);
    }

    [Fact]
    public async Task Edit_Get_PreviousYearSchemeWithAPriceForOnlyOneOfTwoCurrencies_ListsOnlyThatPriceRow()
    {
        var (scheme, lookups, held) = SchemeWithAPriceForOneOfTwoCurrencies(isReadOnly: true);
        var controller = CreateController(new FakeSchemeApiClient { SchemeResponse = scheme }, lookups);

        var result = await controller.Edit(scheme.SchemeId, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal(PreviousYearViewName, view.ViewName);
        var price = Assert.Single(Assert.IsType<SchemeFormViewModel>(view.Model).Prices);
        Assert.Equal(held.CurrencyId, price.CurrencyId);
        Assert.Equal(held.Name, price.CurrencyName);
        Assert.Equal(12.5m, price.Price);
        Assert.NotEqual(Guid.Empty, price.SchemeCurrencyId);
    }

    [Fact]
    public async Task Edit_Get_NonClosedYearSchemeWithAPriceForOnlyOneOfTwoCurrencies_ListsBothCurrenciesWithTheMissingPriceBlank()
    {
        var (scheme, lookups, held) = SchemeWithAPriceForOneOfTwoCurrencies(isReadOnly: false);
        var controller = CreateController(new FakeSchemeApiClient { SchemeResponse = scheme }, lookups);

        var result = await controller.Edit(scheme.SchemeId, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Null(view.ViewName);
        var prices = Assert.IsType<SchemeFormViewModel>(view.Model).Prices;
        Assert.Equal(2, prices.Count);
        Assert.Equal(12.5m, Assert.Single(prices, p => p.CurrencyId == held.CurrencyId).Price);
        var missing = Assert.Single(prices, p => p.CurrencyId != held.CurrencyId);
        Assert.Null(missing.Price);
        Assert.Equal(Guid.Empty, missing.SchemeCurrencyId);
    }

    private static (SchemeResponse Scheme, FakeLookupApiClient Lookups, CurrencyResponse Held) SchemeWithAPriceForOneOfTwoCurrencies(bool isReadOnly)
    {
        var held = new CurrencyResponse(Guid.NewGuid(), "British Pound", "GBP", "GBP - British Pound");
        var other = new CurrencyResponse(Guid.NewGuid(), "Euro", "EUR", "EUR - Euro");
        var schemeId = Guid.NewGuid();
        var scheme = SampleScheme(schemeId, Guid.NewGuid(), isReadOnly) with
        {
            YearId = ClosedYearId,
            Prices = [new SchemeCurrencyPriceResponse(Guid.NewGuid(), held.CurrencyId, 12.5m, held.Name, held.Symbol)],
        };
        var lookups = LookupsWithOneOfEach();
        lookups.Currencies = [held, other];
        return (scheme, lookups, held);
    }

    // Only the first of two currencies is posted back, as it would be for a scheme that holds just that price.
    private static SchemeFormViewModel PreviousYearModelPostingOnePriceOfTwoCurrencies(out CurrencyResponse posted, FakeLookupApiClient lookups)
    {
        posted = new CurrencyResponse(Guid.NewGuid(), "British Pound", "GBP", "GBP - British Pound");
        lookups.Currencies = [posted, new CurrencyResponse(Guid.NewGuid(), "Euro", "EUR", "EUR - Euro")];
        var model = PreviousYearModel();
        model.Prices = [new SchemeCurrencyPriceViewModel { SchemeCurrencyId = Guid.NewGuid(), CurrencyId = posted.CurrencyId, Price = 12.5m }];
        return model;
    }

    [Fact]
    public async Task Apply_InvalidModelState_ListsOnlyTheCurrenciesTheFormPosted()
    {
        var lookups = LookupsWithOneOfEach();
        var model = PreviousYearModelPostingOnePriceOfTwoCurrencies(out var posted, lookups);
        var controller = SpyPostingController(new UpdateSpySchemeApiClient(), lookups);
        controller.ModelState.AddModelError(nameof(SchemeFormViewModel.CustomsDescription), "Enter the customs description");

        var result = await controller.Apply(Guid.NewGuid(), model, cancellationToken: CancellationToken.None);

        Assert.Equal(PreviousYearViewName, Assert.IsType<ViewResult>(result).ViewName);
        var price = Assert.Single(model.Prices);
        Assert.Equal(posted.CurrencyId, price.CurrencyId);
        Assert.Equal(12.5m, price.Price);
        Assert.Equal("British Pound", price.CurrencyName);
    }

    [Fact]
    public async Task Apply_ApiFailure_ListsOnlyTheCurrenciesTheFormPosted()
    {
        var lookups = LookupsWithOneOfEach();
        var model = PreviousYearModelPostingOnePriceOfTwoCurrencies(out var posted, lookups);
        var api = new UpdateSpySchemeApiClient
        {
            SaveResult = new SchemeSaveResult(false, null, new Dictionary<string, string[]> { [string.Empty] = ["The scheme could not be saved."] })
        };
        var controller = SpyPostingController(api, lookups);

        var result = await controller.Apply(Guid.NewGuid(), model, cancellationToken: CancellationToken.None);

        Assert.Equal(PreviousYearViewName, Assert.IsType<ViewResult>(result).ViewName);
        Assert.Single(api.Updates);
        Assert.Equal(posted.CurrencyId, Assert.Single(model.Prices).CurrencyId);
    }

    [Fact]
    public async Task Edit_Post_Success_SavesTheRouteSchemeSetsTheNotificationAndReturnsToTheList()
    {
        var schemeId = Guid.NewGuid();
        var api = new UpdateSpySchemeApiClient();
        var controller = SpyPostingController(api);
        var model = PreviousYearModel(isReadOnly: false);
        model.CustomsDescription = "Revised customs description";

        var result = await controller.Edit(schemeId, model, cancellationToken: CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(SchemeController.Index), redirect.ActionName);
        Assert.Null(redirect.RouteValues);
        var update = Assert.Single(api.Updates);
        Assert.Equal(schemeId, update.SchemeId);
        Assert.Equal("Revised customs description", update.Request.CustomsDescription);
        var notification = controller.TempData.GetNotification();
        Assert.NotNull(notification);
        Assert.Equal(NotificationType.Success, notification.Type);
        Assert.Equal(UpdatedMessage, notification.Message);
        Assert.False(model.IsReadOnly);
    }

    [Fact]
    public async Task Edit_Post_ApiFailure_RedisplaysTheEditFormInTheDefaultViewWithItsOptionLists()
    {
        var api = new UpdateSpySchemeApiClient
        {
            SaveResult = new SchemeSaveResult(false, null, new Dictionary<string, string[]> { [nameof(SchemeFormViewModel.CustomsDescription)] = ["Customs description must not exceed 500 characters"] })
        };
        var controller = SpyPostingController(api, LookupsWithOneOfEach());
        var model = PreviousYearModel(isReadOnly: false);

        var result = await controller.Edit(Guid.NewGuid(), model, cancellationToken: CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Null(view.ViewName);
        Assert.Same(model, view.Model);
        Assert.Single(api.Updates);
        Assert.Equal("Customs description must not exceed 500 characters", Assert.Single(controller.ModelState[nameof(SchemeFormViewModel.CustomsDescription)]!.Errors).ErrorMessage);
        AssertOptionListsPopulated(model);
        Assert.Null(controller.TempData.GetNotification());
    }

    [Fact]
    public async Task Apply_Success_SavesTheRouteSchemeAndStaysOnTheEditPageWithANotification()
    {
        var schemeId = Guid.NewGuid();
        var sharedId = Guid.NewGuid();
        var api = new UpdateSpySchemeApiClient();
        var controller = SpyPostingController(api);
        var model = PreviousYearModel();
        model.SharedId = sharedId;
        model.CustomsDescription = "Revised customs description";
        model.CustomsVolume = "50 ml";

        var result = await controller.Apply(schemeId, model, cancellationToken: CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(SchemeController.Edit), redirect.ActionName);
        Assert.Null(redirect.ControllerName);
        Assert.Equal(schemeId, Assert.IsType<Guid>(redirect.RouteValues!["id"]));
        var update = Assert.Single(api.Updates);
        Assert.Equal(schemeId, update.SchemeId);
        Assert.Equal("Revised customs description", update.Request.CustomsDescription);
        Assert.Equal("50 ml", update.Request.CustomsVolume);
        Assert.Equal(sharedId, update.Request.SharedId);
        Assert.Null(update.Request.AuditWhy);
        var notification = controller.TempData.GetNotification();
        Assert.NotNull(notification);
        Assert.Equal(NotificationType.Success, notification.Type);
        Assert.Equal(UpdatedMessage, notification.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Apply_TreatsTheSchemeAsPreviousYearWhateverTheFormPostedForIsReadOnly(bool? posted)
    {
        var controller = SpyPostingController(new UpdateSpySchemeApiClient());
        var model = PreviousYearModel(posted);

        await controller.Apply(Guid.NewGuid(), model, cancellationToken: CancellationToken.None);

        Assert.True(model.IsReadOnly);
        Assert.True(model.IsPreviousYear);
    }

    [Fact]
    public async Task Apply_AuditStepWithAReason_SendsTheReasonAndStaysOnTheEditPage()
    {
        var schemeId = Guid.NewGuid();
        var api = new UpdateSpySchemeApiClient();
        var controller = SpyPostingController(api);

        var result = await controller.Apply(schemeId, AuditModel(isAuditStep: true, auditWhy: "Corrected the customs description"), cancellationToken: CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(SchemeController.Edit), redirect.ActionName);
        Assert.Equal(schemeId, Assert.IsType<Guid>(redirect.RouteValues!["id"]));
        Assert.Equal("Corrected the customs description", Assert.Single(api.Updates).Request.AuditWhy);
        var notification = controller.TempData.GetNotification();
        Assert.NotNull(notification);
        Assert.Equal(UpdatedMessage, notification.Message);
    }

    [Fact]
    public async Task Apply_PassesTheRequestCancellationTokenToTheApi()
    {
        using var cancellation = new CancellationTokenSource();
        var api = new UpdateSpySchemeApiClient();
        var controller = SpyPostingController(api);

        await controller.Apply(Guid.NewGuid(), PreviousYearModel(), cancellationToken: cancellation.Token);

        Assert.Equal(cancellation.Token, Assert.Single(api.Updates).Token);
    }

    [Fact]
    public async Task Apply_CancelledRequest_SurfacesTheCancellationAndReportsNoSuccess()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var controller = SpyPostingController(new UpdateSpySchemeApiClient());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => controller.Apply(Guid.NewGuid(), PreviousYearModel(), cancellationToken: cancellation.Token));

        Assert.Null(controller.TempData.GetNotification());
    }

    [Fact]
    public async Task Apply_InvalidModelState_RedisplaysThePreviousYearFormWithoutCallingTheApi()
    {
        var api = new UpdateSpySchemeApiClient();
        var controller = SpyPostingController(api, LookupsWithOneOfEach());
        controller.ModelState.AddModelError(nameof(SchemeFormViewModel.CustomsDescription), "Enter the customs description");
        var model = PreviousYearModel();
        model.CustomsDescription = null;
        model.Instructions = "<p>Keep this</p><script>alert('x')</script>";

        var result = await controller.Apply(Guid.NewGuid(), model, cancellationToken: CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal(PreviousYearViewName, view.ViewName);
        Assert.Same(model, view.Model);
        Assert.True(model.IsPreviousYear);
        Assert.Empty(api.Updates);
        Assert.Equal("Enter the customs description", Assert.Single(controller.ModelState[nameof(SchemeFormViewModel.CustomsDescription)]!.Errors).ErrorMessage);
        Assert.Contains("<p>Keep this</p>", model.Instructions);
        Assert.DoesNotContain("<script", model.Instructions);
        AssertOptionListsPopulated(model);
        Assert.Null(controller.TempData.GetNotification());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Apply_AuditStepWithoutAReason_ShowsTheAuditPageAgainWithoutCallingTheApi(string? reason)
    {
        var api = new UpdateSpySchemeApiClient();
        var controller = SpyPostingController(api);
        controller.ModelState.AddModelError(nameof(SchemeFormViewModel.AuditWhy), ReasonRequiredMessage);
        var model = AuditModel(isAuditStep: true, auditWhy: reason);

        var result = await controller.Apply(Guid.NewGuid(), model, cancellationToken: CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal(AuditReasonFlow.ViewName, view.ViewName);
        var page = Assert.IsType<AuditReasonPageViewModel>(view.Model);
        Assert.Equal(AuditObjectName, page.AuditObjectName);
        Assert.Equal(reason, page.AuditWhy);
        Assert.Equal(ReasonRequiredMessage, Assert.Single(controller.ModelState[nameof(SchemeFormViewModel.AuditWhy)]!.Errors).ErrorMessage);
        Assert.True(model.IsAuditStep);
        Assert.Empty(api.Updates);
    }

    [Fact]
    public async Task Apply_AuditStepWithAnotherInvalidField_RedisplaysThePreviousYearFormAndLeavesTheAuditStep()
    {
        var api = new UpdateSpySchemeApiClient();
        var controller = SpyPostingController(api);
        controller.ModelState.AddModelError(nameof(SchemeFormViewModel.AuditWhy), ReasonRequiredMessage);
        controller.ModelState.AddModelError(nameof(SchemeFormViewModel.CustomsDescription), "Enter the customs description");
        var model = AuditModel(isAuditStep: true);

        var result = await controller.Apply(Guid.NewGuid(), model, cancellationToken: CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal(PreviousYearViewName, view.ViewName);
        Assert.Same(model, view.Model);
        Assert.False(model.IsAuditStep);
        Assert.Empty(api.Updates);
    }

    [Fact]
    public async Task Apply_ApiRejectsTheEdit_AddsTheErrorsAndRedisplaysThePreviousYearForm()
    {
        var api = new UpdateSpySchemeApiClient
        {
            SaveResult = new SchemeSaveResult(false, null, new Dictionary<string, string[]>
            {
                [nameof(SchemeFormViewModel.CustomsDescription)] = ["Customs description must not exceed 500 characters"],
                [string.Empty] = ["The scheme could not be saved."],
            })
        };
        var controller = SpyPostingController(api, LookupsWithOneOfEach());
        var model = PreviousYearModel();

        var result = await controller.Apply(Guid.NewGuid(), model, cancellationToken: CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal(PreviousYearViewName, view.ViewName);
        Assert.Same(model, view.Model);
        Assert.True(model.IsPreviousYear);
        Assert.Single(api.Updates);
        Assert.Equal("Customs description must not exceed 500 characters", Assert.Single(controller.ModelState[nameof(SchemeFormViewModel.CustomsDescription)]!.Errors).ErrorMessage);
        Assert.Equal("The scheme could not be saved.", Assert.Single(controller.ModelState[string.Empty]!.Errors).ErrorMessage);
        AssertOptionListsPopulated(model);
        Assert.Null(controller.TempData.GetNotification());
    }

    [Fact]
    public async Task Apply_ApiRequiresAnAuditReason_ShowsTheAuditPageWithoutRedirectingOrNotifying()
    {
        var api = new UpdateSpySchemeApiClient { SaveResult = AuditRequired() };
        var controller = SpyPostingController(api, fields:
        [
            ("__RequestVerificationToken", "token"),
            ("IsAuditStep", "false"),
            ("AuditWhy", string.Empty),
            ("CustomsDescription", "Revised customs description"),
        ]);

        var result = await controller.Apply(Guid.NewGuid(), PreviousYearModel(), cancellationToken: CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal(AuditReasonFlow.ViewName, view.ViewName);
        var page = Assert.IsType<AuditReasonPageViewModel>(view.Model);
        Assert.Equal(AuditObjectName, page.AuditObjectName);
        Assert.Null(page.AuditWhy);
        Assert.Equal([new("CustomsDescription", "Revised customs description")], page.Fields);
        Assert.Null(Assert.Single(api.Updates).Request.AuditWhy);
        Assert.Null(controller.TempData.GetNotification());
    }

    [Fact]
    public async Task Apply_AuditStepWhereTheApiRejectsOnlyTheReason_ShowsTheAuditPageAgain()
    {
        var api = new UpdateSpySchemeApiClient
        {
            SaveResult = new SchemeSaveResult(false, null, new Dictionary<string, string[]> { [nameof(SchemeFormViewModel.AuditWhy)] = [ReasonTooLongMessage] })
        };
        var controller = SpyPostingController(api);
        var model = AuditModel(isAuditStep: true, auditWhy: "kept reason");

        var result = await controller.Apply(Guid.NewGuid(), model, cancellationToken: CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal(AuditReasonFlow.ViewName, view.ViewName);
        var page = Assert.IsType<AuditReasonPageViewModel>(view.Model);
        Assert.Equal("kept reason", page.AuditWhy);
        Assert.Equal(AuditObjectName, page.AuditObjectName);
        Assert.Equal(ReasonTooLongMessage, Assert.Single(controller.ModelState[nameof(SchemeFormViewModel.AuditWhy)]!.Errors).ErrorMessage);
        Assert.True(model.IsAuditStep);
        Assert.Single(api.Updates);
    }

    [Fact]
    public async Task Apply_RemoveCategoryNotConfirmed_AsksFirstAndChangesNothing()
    {
        var api = new UpdateSpySchemeApiClient();
        var controller = SpyPostingController(api, fields:
        [
            ("__RequestVerificationToken", "token"),
            ("CustomsDescription", "Revised customs description"),
            ("testCommand", "remove-category:0:0"),
        ]);
        var model = ClosedYearCommandData.Tree();
        var before = ClosedYearCommandData.Describe(model);

        var result = await controller.Apply(Guid.NewGuid(), model, testCommand: "remove-category:0:0", cancellationToken: CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal(ConfirmationFlow.ViewName, view.ViewName);
        var page = Assert.IsType<ConfirmationPageViewModel>(view.Model);
        Assert.Equal("Are you sure you want to remove this Category?", page.Message);
        Assert.Null(page.CancelUrl);
        Assert.Contains(new KeyValuePair<string, string>("testCommand", "remove-category:0:0"), page.Fields);
        Assert.Contains(new KeyValuePair<string, string>("CustomsDescription", "Revised customs description"), page.Fields);
        Assert.Equal(before, ClosedYearCommandData.Describe(model));
        Assert.Empty(api.Updates);
    }

    [Fact]
    public async Task Apply_RemoveCategoryDeclined_RedisplaysThePreviousYearFormWithTheCategoryStillThere()
    {
        var api = new UpdateSpySchemeApiClient();
        var controller = SpyPostingController(api);
        var model = ClosedYearCommandData.Tree();
        var before = ClosedYearCommandData.Describe(model);

        var result = await controller.Apply(Guid.NewGuid(), model, testCommand: "remove-category:0:0", confirmation: ConfirmationFlow.Cancelled, cancellationToken: CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal(PreviousYearViewName, view.ViewName);
        Assert.Same(model, view.Model);
        Assert.Equal(before, ClosedYearCommandData.Describe(model));
        Assert.Empty(api.Updates);
    }

    [Fact]
    public async Task Apply_MoveResultUp_ReordersTheStagedTreeAndRedisplaysWithoutSavingOrValidating()
    {
        var api = new UpdateSpySchemeApiClient();
        var controller = SpyPostingController(api, LookupsWithOneOfEach());
        controller.ModelState.AddModelError(nameof(SchemeFormViewModel.CustomsDescription), "Enter the customs description");
        var model = ClosedYearCommandData.Tree();
        model.YearId = ClosedYearId;
        model.Instructions = "<p>Keep this</p><script>alert('x')</script>";

        var result = await controller.Apply(Guid.NewGuid(), model, testCommand: "move-result-up:0:1", cancellationToken: CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal(PreviousYearViewName, view.ViewName);
        Assert.Same(model, view.Model);
        Assert.True(model.IsPreviousYear);
        Assert.Equal(["Serology result 2", "Serology result 1"], model.Tests[0].ResultItems.Select(item => item.Name).ToArray());
        Assert.Equal(["Bacteriology result 1", "Bacteriology result 2"], model.Tests[1].ResultItems.Select(item => item.Name).ToArray());
        Assert.True(controller.ModelState.IsValid);
        Assert.Contains("<p>Keep this</p>", model.Instructions);
        Assert.DoesNotContain("<script", model.Instructions);
        AssertOptionListsPopulated(model);
        Assert.Empty(api.Updates);
        Assert.Null(controller.TempData.GetNotification());
    }

    [Fact]
    public async Task Apply_RemoveCategoryConfirmed_RemovesItFromTheStagedTreeWithoutSaving()
    {
        var api = new UpdateSpySchemeApiClient();
        var controller = SpyPostingController(api);
        var model = ClosedYearCommandData.Tree();

        var result = await controller.Apply(Guid.NewGuid(), model, testCommand: "remove-category:0:0", confirmation: ConfirmationFlow.Confirmed, cancellationToken: CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal(PreviousYearViewName, view.ViewName);
        Assert.Equal(["Serology category 2"], model.Tests[0].Categories.Select(category => category.Name).ToArray());
        Assert.Equal(2, model.Tests[1].Categories.Count);
        Assert.Empty(api.Updates);
    }

    [Fact]
    public async Task Apply_AddTabulation_StagesTheNamedTabulationWithoutSaving()
    {
        var api = new UpdateSpySchemeApiClient();
        var controller = SpyPostingController(api);
        var model = ClosedYearCommandData.Tree();

        var result = await controller.Apply(Guid.NewGuid(), model, testCommand: "add-tabulation", cancellationToken: CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal(PreviousYearViewName, view.ViewName);
        Assert.Equal(["Tab A", "Tab B", "Published"], model.Tabulations.Select(tabulation => tabulation.Name).ToArray());
        Assert.Null(model.NewTabulationName);
        Assert.Empty(api.Updates);
    }

    [Fact]
    public async Task Apply_RemoveTabulation_DropsItFromTheStagedTreeWithoutSaving()
    {
        var api = new UpdateSpySchemeApiClient();
        var controller = SpyPostingController(api);
        var model = ClosedYearCommandData.Tree();

        var result = await controller.Apply(Guid.NewGuid(), model, testCommand: "remove-tabulation:0", cancellationToken: CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal(PreviousYearViewName, view.ViewName);
        Assert.Equal(["Tab B"], model.Tabulations.Select(tabulation => tabulation.Name).ToArray());
        Assert.Empty(api.Updates);
    }

    [Theory]
    [MemberData(nameof(ClosedYearCommandData.Allowed), MemberType = typeof(ClosedYearCommandData))]
    public async Task Apply_CommandLegacyKeepsOnAPreviousYearScheme_ChangesTheStagedTreeAndRedisplaysWithoutSaving(string command)
    {
        var api = new UpdateSpySchemeApiClient();
        var controller = SpyPostingController(api);
        controller.ModelState.AddModelError(nameof(SchemeFormViewModel.CustomsDescription), "Enter the customs description");
        var model = ClosedYearCommandData.Tree();
        var before = ClosedYearCommandData.Describe(model);

        var result = await controller.Apply(Guid.NewGuid(), model, testCommand: command, confirmation: ConfirmationFlow.Confirmed, cancellationToken: CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal(PreviousYearViewName, view.ViewName);
        Assert.Same(model, view.Model);
        Assert.NotEqual(before, ClosedYearCommandData.Describe(model));
        Assert.True(controller.ModelState.IsValid);
        Assert.Empty(api.Updates);
    }

    [Theory]
    [MemberData(nameof(ClosedYearCommandData.Blocked), MemberType = typeof(ClosedYearCommandData))]
    public async Task Apply_CommandLegacyHidesOnAPreviousYearScheme_LeavesTheStagedTreeAloneAndRedisplaysWithoutSaving(string command)
    {
        var api = new UpdateSpySchemeApiClient();
        var controller = SpyPostingController(api, fields: [($"pick:{command}", Guid.NewGuid().ToString())]);
        controller.ModelState.AddModelError(nameof(SchemeFormViewModel.CustomsDescription), "Enter the customs description");
        var model = ClosedYearCommandData.Tree();
        var before = ClosedYearCommandData.Describe(model);

        var result = await controller.Apply(Guid.NewGuid(), model, testCommand: command, confirmation: ConfirmationFlow.Confirmed, cancellationToken: CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal(PreviousYearViewName, view.ViewName);
        Assert.Same(model, view.Model);
        Assert.True(model.IsPreviousYear);
        Assert.Equal(before, ClosedYearCommandData.Describe(model));
        Assert.True(controller.ModelState.IsValid);
        Assert.Empty(api.Updates);
    }

    // Control for the test above: the same posts do change the tree through Edit, which only redisplays the default view.
    [Theory]
    [MemberData(nameof(ClosedYearCommandData.Blocked), MemberType = typeof(ClosedYearCommandData))]
    public async Task Edit_Post_CommandLegacyHidesOnAPreviousYearScheme_StillChangesTheStagedTreeOnAnyOtherScheme(string command)
    {
        var api = new UpdateSpySchemeApiClient();
        var controller = SpyPostingController(api, fields: [($"pick:{command}", Guid.NewGuid().ToString())]);
        var model = ClosedYearCommandData.Tree();
        var before = ClosedYearCommandData.Describe(model);

        var result = await controller.Edit(Guid.NewGuid(), model, testCommand: command, confirmation: ConfirmationFlow.Confirmed, cancellationToken: CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Null(view.ViewName);
        Assert.NotEqual(before, ClosedYearCommandData.Describe(model));
        Assert.Empty(api.Updates);
    }

    [Fact]
    public void Apply_IsAPostOnlyActionProtectedByAnAntiForgeryToken()
    {
        var apply = typeof(SchemeController).GetMethod(nameof(SchemeController.Apply))!;

        Assert.NotNull(apply.GetCustomAttribute<HttpPostAttribute>());
        Assert.NotNull(apply.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
    }
}
