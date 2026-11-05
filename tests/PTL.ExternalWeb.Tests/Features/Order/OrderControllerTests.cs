using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Options;
using PTL.ApiClient;
using PTL.Contracts.CreateOrder;
using PTL.ExternalWeb.Features.Order;
using PTL.ExternalWeb.Tests.TestSupport;
using PTL.SharedUI.Notifications;

namespace PTL.ExternalWeb.Tests.Features.Order;

public class OrderControllerTests
{
    private static readonly Guid ParticipantId = Guid.NewGuid();

    private static OrderController CreateController(FakeCreateOrderApiClient? api = null, bool signedInAsParticipant = true, string? termsUrl = "https://example.test/terms.pdf")
    {
        var controller = new OrderController(
            api ?? new FakeCreateOrderApiClient(),
            Options.Create(new OrderPageOptions { TermsAndConditionsUrl = termsUrl }));
        if (signedInAsParticipant)
        {
            AuthenticatedParticipant.Attach(controller, ParticipantId);
        }
        else
        {
            controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        }

        controller.TempData = new TempDataDictionary(controller.HttpContext, new FakeTempDataProvider());
        return controller;
    }

    private static CreateOrderApiResult Status(CreateOrderApiStatus status) => new(status);

    [Fact]
    public void Index_RedirectsHome()
    {
        var result = CreateController().Index();

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Home", redirect.ControllerName);
    }

    [Fact]
    public async Task CurrentYear_Allowed_ReturnsIndexViewForCurrentYear()
    {
        var api = new FakeCreateOrderApiClient();
        var controller = CreateController(api);

        var result = await controller.CurrentYear("alpha", CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("Index", view.ViewName);
        var model = Assert.IsType<CreateOrderViewModel>(view.Model);
        Assert.Equal("current", model.YearSlug);
        Assert.Equal("alpha", model.Search);
        Assert.True(model.IsCurrentYear);
        Assert.Equal(nameof(OrderController.CurrentYear), model.PageAction);
        Assert.Equal("current", api.LastGetYear);
        Assert.Equal(ParticipantId, api.LastParticipantId);
    }

    [Fact]
    public async Task NextYear_Allowed_ReturnsIndexViewForNextYear()
    {
        var api = new FakeCreateOrderApiClient();
        var controller = CreateController(api);

        var result = await controller.NextYear(null, CancellationToken.None);

        var model = Assert.IsType<CreateOrderViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal("next", model.YearSlug);
        Assert.False(model.IsCurrentYear);
        Assert.Equal(nameof(OrderController.NextYear), model.PageAction);
        Assert.Equal("next", api.LastGetYear);
    }

    [Fact]
    public async Task CurrentYear_NoParticipantClaim_ReturnsNotFound()
    {
        var result = await CreateController(signedInAsParticipant: false).CurrentYear(null, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task CurrentYear_NotAllowed_ReturnsForbiddenExplanationView()
    {
        var api = new FakeCreateOrderApiClient { GetResult = Status(CreateOrderApiStatus.NotAllowed) };
        var controller = CreateController(api);

        var result = await controller.CurrentYear(null, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("NotAvailable", view.ViewName);
        Assert.Equal("current", view.Model);
        Assert.Equal(StatusCodes.Status403Forbidden, controller.Response.StatusCode);
    }

    [Fact]
    public async Task CurrentYear_ApiNotFound_ReturnsNotFound()
    {
        var api = new FakeCreateOrderApiClient { GetResult = Status(CreateOrderApiStatus.NotFound) };

        Assert.IsType<NotFoundResult>(await CreateController(api).CurrentYear(null, CancellationToken.None));
    }

    [Fact]
    public async Task Change_Allowed_ReturnsOrderBodyPartialAndForwardsTheChange()
    {
        var api = new FakeCreateOrderApiClient();
        var controller = CreateController(api);
        var schemeId = Guid.NewGuid();

        var result = await controller.Change(
            new CreateOrderChangeModel { Year = "Next", SchemeId = schemeId, Field = " APR ", Checked = true, Search = "alpha" },
            CancellationToken.None);

        var partial = Assert.IsType<PartialViewResult>(result);
        Assert.Equal("_OrderBody", partial.ViewName);
        var model = Assert.IsType<CreateOrderViewModel>(partial.Model);
        Assert.Equal("next", model.YearSlug);
        Assert.Equal("alpha", model.Search);
        Assert.Equal("next", api.LastChangeYear);
        Assert.Equal(new CreateOrderLineChangeRequest(schemeId, "apr", true), api.LastChange);
    }

    [Theory]
    [InlineData("current", "bogus")]
    [InlineData("current", null)]
    [InlineData("someday", "select")]
    [InlineData(null, "select")]
    public async Task Change_InvalidYearOrField_ReturnsBadRequest(string? year, string? field)
    {
        var api = new FakeCreateOrderApiClient();

        var result = await CreateController(api).Change(new CreateOrderChangeModel { Year = year, Field = field }, CancellationToken.None);

        Assert.IsType<BadRequestResult>(result);
        Assert.Null(api.LastChange);
    }

    [Theory]
    [InlineData("select")]
    [InlineData("importexport")]
    [InlineData("mar")]
    public async Task Change_EveryAllowedField_IsAccepted(string field)
    {
        var api = new FakeCreateOrderApiClient();

        var result = await CreateController(api).Change(new CreateOrderChangeModel { Year = "current", Field = field, Checked = true }, CancellationToken.None);

        Assert.IsType<PartialViewResult>(result);
        Assert.Equal(field, api.LastChange!.Field);
    }

    [Fact]
    public async Task Change_NoParticipantClaim_ReturnsNotFound()
    {
        var result = await CreateController(signedInAsParticipant: false)
            .Change(new CreateOrderChangeModel { Year = "current", Field = "select" }, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Change_NotAllowed_ReturnsForbidden()
    {
        var api = new FakeCreateOrderApiClient { ChangeResult = Status(CreateOrderApiStatus.NotAllowed) };

        var result = await CreateController(api).Change(new CreateOrderChangeModel { Year = "current", Field = "select" }, CancellationToken.None);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeResult>(result).StatusCode);
    }

    [Fact]
    public async Task Change_ApiNotFound_ReturnsNotFound()
    {
        var api = new FakeCreateOrderApiClient { ChangeResult = Status(CreateOrderApiStatus.NotFound) };

        var result = await CreateController(api).Change(new CreateOrderChangeModel { Year = "current", Field = "select" }, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void ConfirmClear_ValidYear_ReturnsViewModel()
    {
        var result = CreateController().ConfirmClear("next");

        var model = Assert.IsType<CreateOrderConfirmClearViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal("next", model.YearSlug);
    }

    [Fact]
    public void ConfirmClear_InvalidYear_ReturnsNotFound() =>
        Assert.IsType<NotFoundResult>(CreateController().ConfirmClear("bogus"));

    [Theory]
    [InlineData("current", nameof(OrderController.CurrentYear))]
    [InlineData("next", nameof(OrderController.NextYear))]
    public async Task Clear_Ok_RedirectsBackToTheYearPage(string year, string expectedAction)
    {
        var api = new FakeCreateOrderApiClient();

        var result = await CreateController(api).Clear(year, CancellationToken.None);

        Assert.Equal(expectedAction, Assert.IsType<RedirectToActionResult>(result).ActionName);
        Assert.Equal(year, api.LastClearYear);
    }

    [Fact]
    public async Task Clear_NotAllowed_ReturnsForbiddenExplanationView()
    {
        var api = new FakeCreateOrderApiClient { ClearStatus = CreateOrderApiStatus.NotAllowed };

        var result = await CreateController(api).Clear("current", CancellationToken.None);

        Assert.Equal("NotAvailable", Assert.IsType<ViewResult>(result).ViewName);
    }

    [Fact]
    public async Task Clear_ApiNotFound_ReturnsNotFound()
    {
        var api = new FakeCreateOrderApiClient { ClearStatus = CreateOrderApiStatus.NotFound };

        Assert.IsType<NotFoundResult>(await CreateController(api).Clear("current", CancellationToken.None));
    }

    [Fact]
    public async Task Clear_InvalidYearOrNoParticipant_ReturnsNotFound()
    {
        Assert.IsType<NotFoundResult>(await CreateController().Clear("bogus", CancellationToken.None));
        Assert.IsType<NotFoundResult>(await CreateController(signedInAsParticipant: false).Clear("current", CancellationToken.None));
    }

    [Fact]
    public async Task Review_OrderCanProceed_ReturnsSummaryOfSelectedSchemes()
    {
        var api = new FakeCreateOrderApiClient { GetResult = new(CreateOrderApiStatus.Ok, FakeCreateOrderApiClient.SampleOrder(selected: true)) };

        var result = await CreateController(api).Review("current", CancellationToken.None);

        var model = Assert.IsType<CreateOrderReviewViewModel>(Assert.IsType<ViewResult>(result).Model);
        var scheme = Assert.Single(model.Schemes);
        Assert.Equal(FakeCreateOrderApiClient.AvailableSchemeId, scheme.SchemeId);
        Assert.Equal(nameof(OrderController.CurrentYear), model.BackAction);
        Assert.Equal("£30.00", model.FormatMoney(30m));
    }

    [Fact]
    public async Task Review_NextYear_BackLinkTargetsNextYearPage()
    {
        var api = new FakeCreateOrderApiClient { GetResult = new(CreateOrderApiStatus.Ok, FakeCreateOrderApiClient.SampleOrder(selected: true)) };

        var result = await CreateController(api).Review("next", CancellationToken.None);

        Assert.Equal(nameof(OrderController.NextYear), Assert.IsType<CreateOrderReviewViewModel>(Assert.IsType<ViewResult>(result).Model).BackAction);
    }

    [Fact]
    public async Task Review_NothingSelectedYet_RedirectsBackToTheYearPageWithAMessage()
    {
        var controller = CreateController();

        var result = await controller.Review("current", CancellationToken.None);

        Assert.Equal(nameof(OrderController.CurrentYear), Assert.IsType<RedirectToActionResult>(result).ActionName);
        var notification = controller.TempData.GetNotification();
        Assert.Equal(NotificationType.Error, notification!.Type);
        Assert.Contains("at least one scheme", notification.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Review_AsksForTheConsentsAndCarriesTheSavedPurchaseOrderAndTermsLink()
    {
        var order = FakeCreateOrderApiClient.SampleOrder(selected: true) with { PurchaseOrderNumber = "PO-1" };
        var api = new FakeCreateOrderApiClient { GetResult = new(CreateOrderApiStatus.Ok, order) };

        var result = await CreateController(api).Review("current", CancellationToken.None);

        var model = Assert.IsType<CreateOrderReviewViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.True(api.LastGetReview);
        Assert.Equal("PO-1", model.PurchaseOrderNumber);
        Assert.Equal("https://example.test/terms.pdf", model.TermsUrl);
        Assert.False(model.TermsAccepted);
        Assert.Empty(model.Errors);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("javascript:alert(1)")]
    [InlineData("not a url")]
    public async Task Review_WithoutAUsableTermsAddress_HasNoTermsLink(string? configured)
    {
        var api = new FakeCreateOrderApiClient { GetResult = new(CreateOrderApiStatus.Ok, FakeCreateOrderApiClient.SampleOrder(selected: true)) };

        var result = await CreateController(api, termsUrl: configured).Review("current", CancellationToken.None);

        Assert.Null(Assert.IsType<CreateOrderReviewViewModel>(Assert.IsType<ViewResult>(result).Model).TermsUrl);
    }

    private static FakeCreateOrderApiClient ApiWithOrder(CreateOrderResponse? order = null) =>
        new() { GetResult = new(CreateOrderApiStatus.Ok, order ?? FakeCreateOrderApiClient.SampleOrder(selected: true)) };

    [Fact]
    public async Task Save_Ok_StoresThePurchaseOrderAndReturnsToTheReviewWithAMessage()
    {
        var api = ApiWithOrder();
        var controller = CreateController(api);

        var result = await controller.Save(new CreateOrderReviewForm { Year = "next", PurchaseOrderNumber = "PO-5" }, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(OrderController.Review), redirect.ActionName);
        Assert.Equal("next", redirect.RouteValues!["year"]);
        Assert.Equal(new CreateOrderSaveRequest("PO-5"), api.LastSave);
        Assert.Equal("next", api.LastSaveYear);
        Assert.Equal(NotificationType.Success, controller.TempData.GetNotification()!.Type);
    }

    [Fact]
    public async Task Save_WithErrors_ShowsThePageAgainWithThePostedValues()
    {
        var api = ApiWithOrder();
        api.SaveResult = new(CreateOrderApiStatus.Ok, [new CreateOrderValidationErrorResponse("PurchaseOrderNumber", "Too long")]);
        var form = new CreateOrderReviewForm { Year = "current", PurchaseOrderNumber = "PO-LONG", TermsAccepted = true };

        var result = await CreateController(api).Save(form, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("Review", view.ViewName);
        var model = Assert.IsType<CreateOrderReviewViewModel>(view.Model);
        Assert.Equal("PO-LONG", model.PurchaseOrderNumber);
        Assert.True(model.TermsAccepted);
        Assert.Equal("Too long", model.ErrorFor("PurchaseOrderNumber"));
    }

    [Fact]
    public async Task Submit_Ok_SubmitsTheOrderAndGoesHomeWithAMessage()
    {
        var api = ApiWithOrder();
        var controller = CreateController(api);
        var consent = Guid.NewGuid();
        var form = new CreateOrderReviewForm { Year = "current", PurchaseOrderNumber = "PO-7", TermsAccepted = true, ConsentedSchemeIds = [consent] };

        var result = await controller.Submit(form, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Home", redirect.ControllerName);
        Assert.Equal("PO-7", api.LastSubmit!.PurchaseOrderNumber);
        Assert.True(api.LastSubmit.TermsAccepted);
        Assert.Equal([consent], api.LastSubmit.ConsentedSchemeIds);
        Assert.Equal("current", api.LastSubmitYear);
        Assert.Equal(ParticipantId, api.LastParticipantId);
        Assert.Equal(NotificationType.Success, controller.TempData.GetNotification()!.Type);
    }

    [Fact]
    public async Task Submit_WithErrors_ShowsThePageAgainWithTheErrorsAndThePostedValues()
    {
        var consent = Guid.NewGuid();
        var api = ApiWithOrder();
        api.SubmitResult = new(CreateOrderApiStatus.Ok,
        [
            new CreateOrderValidationErrorResponse("TermsAccepted", "Accept the terms"),
            new CreateOrderValidationErrorResponse("Schemes", "Pick a month for Beta")
        ]);
        var form = new CreateOrderReviewForm { Year = "current", PurchaseOrderNumber = "PO-8", ConsentedSchemeIds = [consent] };

        var result = await CreateController(api).Submit(form, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("Review", view.ViewName);
        var model = Assert.IsType<CreateOrderReviewViewModel>(view.Model);
        Assert.Equal("PO-8", model.PurchaseOrderNumber);
        Assert.False(model.TermsAccepted);
        Assert.True(model.IsConsented(consent));
        Assert.Equal(["Schemes", "TermsAccepted"], model.ErrorSummary.Select(e => e.Field));
    }

    [Fact]
    public async Task Submit_WithErrorsAndNothingSelectedAnyMore_ReturnsToTheOrderPage()
    {
        var api = new FakeCreateOrderApiClient
        {
            SubmitResult = new(CreateOrderApiStatus.Ok, [new CreateOrderValidationErrorResponse("Schemes", "Pick a scheme")])
        };

        var result = await CreateController(api).Submit(new CreateOrderReviewForm { Year = "current" }, CancellationToken.None);

        Assert.Equal(nameof(OrderController.CurrentYear), Assert.IsType<RedirectToActionResult>(result).ActionName);
    }

    [Fact]
    public async Task SaveAndSubmit_NotAllowedOrNotFoundOrInvalid_AreHandled()
    {
        var forbidden = new FakeCreateOrderApiClient
        {
            SaveResult = new(CreateOrderApiStatus.NotAllowed, []),
            SubmitResult = new(CreateOrderApiStatus.NotAllowed, [])
        };
        var missing = new FakeCreateOrderApiClient
        {
            SaveResult = new(CreateOrderApiStatus.NotFound, []),
            SubmitResult = new(CreateOrderApiStatus.NotFound, [])
        };
        var form = new CreateOrderReviewForm { Year = "current" };

        Assert.Equal("NotAvailable", Assert.IsType<ViewResult>(await CreateController(forbidden).Save(form, CancellationToken.None)).ViewName);
        Assert.Equal("NotAvailable", Assert.IsType<ViewResult>(await CreateController(forbidden).Submit(form, CancellationToken.None)).ViewName);
        Assert.IsType<NotFoundResult>(await CreateController(missing).Save(form, CancellationToken.None));
        Assert.IsType<NotFoundResult>(await CreateController(missing).Submit(form, CancellationToken.None));
        Assert.IsType<NotFoundResult>(await CreateController().Save(new CreateOrderReviewForm { Year = "bogus" }, CancellationToken.None));
        Assert.IsType<NotFoundResult>(await CreateController().Submit(new CreateOrderReviewForm { Year = "bogus" }, CancellationToken.None));
        Assert.IsType<NotFoundResult>(await CreateController(signedInAsParticipant: false).Save(form, CancellationToken.None));
        Assert.IsType<NotFoundResult>(await CreateController(signedInAsParticipant: false).Submit(form, CancellationToken.None));
    }

    [Theory]
    [InlineData(nameof(OrderController.Save))]
    [InlineData(nameof(OrderController.Submit))]
    public void SaveAndSubmit_ArePostOnlyAndRequireAnAntiforgeryToken(string action)
    {
        var method = typeof(OrderController).GetMethod(action)!;

        Assert.NotEmpty(method.GetCustomAttributes(typeof(HttpPostAttribute), inherit: true));
        Assert.NotEmpty(method.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), inherit: true));
    }

    [Fact]
    public async Task Review_NotAllowedOrNotFoundOrInvalid_AreHandled()
    {
        var forbidden = new FakeCreateOrderApiClient { GetResult = Status(CreateOrderApiStatus.NotAllowed) };
        var missing = new FakeCreateOrderApiClient { GetResult = Status(CreateOrderApiStatus.NotFound) };

        Assert.Equal("NotAvailable", Assert.IsType<ViewResult>(await CreateController(forbidden).Review("current", CancellationToken.None)).ViewName);
        Assert.IsType<NotFoundResult>(await CreateController(missing).Review("current", CancellationToken.None));
        Assert.IsType<NotFoundResult>(await CreateController().Review("bogus", CancellationToken.None));
        Assert.IsType<NotFoundResult>(await CreateController(signedInAsParticipant: false).Review("current", CancellationToken.None));
    }

    [Fact]
    public async Task Print_OrderCanProceed_ReturnsPrintableSummaryView()
    {
        var api = new FakeCreateOrderApiClient { GetResult = new(CreateOrderApiStatus.Ok, FakeCreateOrderApiClient.SampleOrder(selected: true)) };

        var result = await CreateController(api).Print("current", CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("Print", view.ViewName);
        var model = Assert.IsType<CreateOrderReviewViewModel>(view.Model);
        Assert.Equal("current", model.YearSlug);
    }

    [Fact]
    public async Task Print_NotAllowedOrNotFoundOrInvalid_AreHandled()
    {
        var forbidden = new FakeCreateOrderApiClient { GetResult = Status(CreateOrderApiStatus.NotAllowed) };
        var missing = new FakeCreateOrderApiClient { GetResult = Status(CreateOrderApiStatus.NotFound) };

        Assert.Equal("NotAvailable", Assert.IsType<ViewResult>(await CreateController(forbidden).Print("current", CancellationToken.None)).ViewName);
        Assert.IsType<NotFoundResult>(await CreateController(missing).Print("current", CancellationToken.None));
        Assert.IsType<NotFoundResult>(await CreateController().Print("bogus", CancellationToken.None));
        Assert.IsType<NotFoundResult>(await CreateController(signedInAsParticipant: false).Print("current", CancellationToken.None));
    }

    [Fact]
    public async Task Print_NothingSelectedYet_RedirectsBackToTheYearPage()
    {
        var result = await CreateController().Print("next", CancellationToken.None);

        Assert.Equal(nameof(OrderController.NextYear), Assert.IsType<RedirectToActionResult>(result).ActionName);
    }

    [Fact]
    public void OrderController_IsRestrictedToParticipants()
    {
        var authorize = typeof(OrderController).GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), inherit: true)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()
            .Single();

        Assert.Equal(PTL.ExternalWeb.Authorization.ExternalPolicies.Participant, authorize.Policy);
    }

    [Fact]
    public void Change_RequiresAntiforgeryToken()
    {
        var method = typeof(OrderController).GetMethod(nameof(OrderController.Change))!;

        Assert.NotEmpty(method.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), inherit: true));
    }
}
