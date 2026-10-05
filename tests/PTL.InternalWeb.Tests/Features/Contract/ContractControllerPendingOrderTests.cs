using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using PTL.Contracts.Contract;
using PTL.InternalWeb.Notifications;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Features.Contract;

public class ContractControllerPendingOrderTests
{
    private static PTL.InternalWeb.Features.Contract.ContractController CreateController(FakeContractApiClient apiClient) =>
        new(apiClient, new FakeCustomerApiClient(), new FakeLookupApiClient(), new FakeImportPermitApiClient(),
            NullLogger<PTL.InternalWeb.Features.Contract.ContractController>.Instance,
            new FakeContractExportApiClient(), new FakeContractRenewalApiClient(),
            new FakeExportTemplateApiClient(), new FakeBulkExportApiClient(),
            new PTL.Core.Contract.Document.TemplateMergeService())
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), new FakeTempDataProvider())
        };

    private static PendingOrderDetailsResponse SampleOrder(Guid pendingContractId) => new(
        pendingContractId, Guid.NewGuid(), "QAL0001", "Sample Laboratories Ltd", 2026, "2026/27", "PO-1", "£",
        [new PendingOrderSchemeResponse(
            Guid.NewGuid(), Guid.NewGuid(), "001: Alpha Lab", Guid.NewGuid(), "S1", "Sample Scheme",
            [
                new PendingOrderSchemeMonthResponse("Apr", 4, Selected: true, Enabled: true, AlreadyParticipating: false),
                new PendingOrderSchemeMonthResponse("May", 5, Selected: false, Enabled: false, AlreadyParticipating: false),
                new PendingOrderSchemeMonthResponse("Jun", 6, Selected: true, Enabled: false, AlreadyParticipating: true)
            ],
            ImportExportLicenceRequired: true, IsRemoved: false, Price: 100m, PostagePrice: 10m, TotalPrice: 110m)],
        100m, 10m, 110m);

    private static PendingOrderSchemeUpdateRequest UpdateRequest(bool isRemoved = false) =>
        new(false, false, false, true, false, false, false, false, false, false, false, false, true, isRemoved);

    [Fact]
    public async Task ReviewPendingOrders_ReturnsViewWithBothGrids()
    {
        var apiClient = new FakeContractApiClient
        {
            PendingOrders = new PendingOrderListResponse(
                [new PendingOrderSummaryResponse(Guid.NewGuid(), Guid.NewGuid(), "QAL0001", "Sample Labs", 2026, "2026/27", DateTime.UtcNow)],
                [])
        };
        var controller = CreateController(apiClient);

        var result = await controller.ReviewPendingOrders(CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<PTL.InternalWeb.Features.Contract.PendingOrderListViewModel>(view.Model);
        Assert.Single(model.CurrentYearOrders);
        Assert.Empty(model.NextYearOrders);
    }

    [Fact]
    public async Task PendingOrderDetails_UnknownOrder_ReturnsNotFound()
    {
        var controller = CreateController(new FakeContractApiClient { PendingOrder = null });

        var result = await controller.PendingOrderDetails(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task PendingOrderDetails_KnownOrder_ReturnsViewWithOrder()
    {
        var pendingContractId = Guid.NewGuid();
        var controller = CreateController(new FakeContractApiClient { PendingOrder = SampleOrder(pendingContractId) });

        var result = await controller.PendingOrderDetails(pendingContractId, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<PTL.InternalWeb.Features.Contract.PendingOrderDetailsViewModel>(view.Model);
        Assert.Equal(110m, model.Order.Total);
        Assert.Single(model.Order.Schemes);
    }

    [Fact]
    public async Task UpdatePendingOrderScheme_UnknownScheme_ReturnsNotFound()
    {
        var controller = CreateController(new FakeContractApiClient { UpdatePendingOrderSchemeResult = false });

        var result = await controller.UpdatePendingOrderScheme(Guid.NewGuid(), Guid.NewGuid(), UpdateRequest(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task UpdatePendingOrderScheme_Success_RedirectsBackToDetails()
    {
        var apiClient = new FakeContractApiClient();
        var controller = CreateController(apiClient);
        var pendingContractId = Guid.NewGuid();

        var result = await controller.UpdatePendingOrderScheme(pendingContractId, Guid.NewGuid(), UpdateRequest(), CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(PTL.InternalWeb.Features.Contract.ContractController.PendingOrderDetails), redirect.ActionName);
        Assert.Equal(pendingContractId, redirect.RouteValues!["pendingContractId"]);
        Assert.False(apiClient.LastSchemeUpdate!.IsRemoved);
    }

    [Fact]
    public async Task RemovePendingOrderScheme_TogglesTheRemovedFlag()
    {
        var apiClient = new FakeContractApiClient();
        var controller = CreateController(apiClient);

        await controller.RemovePendingOrderScheme(Guid.NewGuid(), Guid.NewGuid(), UpdateRequest(isRemoved: false), CancellationToken.None);
        Assert.True(apiClient.LastSchemeUpdate!.IsRemoved);

        await controller.RemovePendingOrderScheme(Guid.NewGuid(), Guid.NewGuid(), UpdateRequest(isRemoved: true), CancellationToken.None);
        Assert.False(apiClient.LastSchemeUpdate!.IsRemoved);
    }

    [Fact]
    public async Task RemovePendingOrderScheme_UnknownScheme_ReturnsNotFound()
    {
        var controller = CreateController(new FakeContractApiClient { UpdatePendingOrderSchemeResult = false });

        var result = await controller.RemovePendingOrderScheme(Guid.NewGuid(), Guid.NewGuid(), UpdateRequest(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task ApprovePendingOrder_UnknownOrder_ReturnsNotFound()
    {
        var controller = CreateController(new FakeContractApiClient
        {
            ApprovePendingOrderResult = new PendingOrderDecisionResult(false, true, new Dictionary<string, string[]>())
        });

        var result = await controller.ApprovePendingOrder(Guid.NewGuid(), "PO-2", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task ApprovePendingOrder_ValidationFailure_RedirectsBackToDetails()
    {
        var controller = CreateController(new FakeContractApiClient
        {
            ApprovePendingOrderResult = new PendingOrderDecisionResult(false, false, new Dictionary<string, string[]> { ["PurchaseOrderNumber"] = ["Too long."] })
        });

        var result = await controller.ApprovePendingOrder(Guid.NewGuid(), "PO-2", CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(PTL.InternalWeb.Features.Contract.ContractController.PendingOrderDetails), redirect.ActionName);
        Assert.Equal(NotificationType.Error, controller.TempData.GetNotification()!.Type);
    }

    [Fact]
    public async Task ApprovePendingOrder_Success_RedirectsToReviewListWithNotification()
    {
        var apiClient = new FakeContractApiClient();
        var controller = CreateController(apiClient);

        var result = await controller.ApprovePendingOrder(Guid.NewGuid(), "PO-2", CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(PTL.InternalWeb.Features.Contract.ContractController.ReviewPendingOrders), redirect.ActionName);
        Assert.Equal("PO-2", apiClient.LastApproveRequest!.PurchaseOrderNumber);
        var notification = controller.TempData.GetNotification();
        Assert.Equal(NotificationType.Success, notification!.Type);
        Assert.Equal("Order approved successfully.", notification.Message);
    }

    [Fact]
    public async Task DeclinePendingOrder_UnknownOrder_ReturnsNotFound()
    {
        var controller = CreateController(new FakeContractApiClient { DeclinePendingOrderResult = false });

        var result = await controller.DeclinePendingOrder(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task DeclinePendingOrder_Success_RedirectsToReviewListWithNotification()
    {
        var controller = CreateController(new FakeContractApiClient { DeclinePendingOrderResult = true });

        var result = await controller.DeclinePendingOrder(Guid.NewGuid(), CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(PTL.InternalWeb.Features.Contract.ContractController.ReviewPendingOrders), redirect.ActionName);
        var notification = controller.TempData.GetNotification();
        Assert.Equal(NotificationType.Success, notification!.Type);
        Assert.Equal("Order declined successfully.", notification.Message);
    }
}
