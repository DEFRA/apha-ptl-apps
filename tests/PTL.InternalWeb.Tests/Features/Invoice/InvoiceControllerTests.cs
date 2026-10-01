using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using PTL.Contracts.Invoice;
using PTL.InternalWeb.Features.Invoice;
using PTL.InternalWeb.Notifications;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Features.Invoice;

public class InvoiceControllerTests
{
    private static InvoiceController CreateController(
        FakeInvoiceApiClient? apiClient = null,
        string environmentName = "Development") =>
        new(apiClient ?? new FakeInvoiceApiClient(), new FakeHostEnvironment(environmentName), NullLogger<InvoiceController>.Instance)
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), new FakeTempDataProvider())
        };

    [Fact]
    public async Task Index_BuildsViewModelFromPendingSummary()
    {
        var apiClient = new FakeInvoiceApiClient
        {
            Pending = new PendingInvoiceSummaryResponse(2026, "2025/26", 10, 2, 3, ["ops@example.com"])
        };
        var controller = CreateController(apiClient);

        var result = await controller.Index(CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<InvoiceGenerationViewModel>(view.Model);
        Assert.Equal("2025/26", model.FinancialYear);
        Assert.Equal(10, model.EligibleContractCount);
        Assert.Equal(2, model.OptOutContractCount);
        Assert.Equal(3, model.NonFeePayingItemCount);
        Assert.Equal(["ops@example.com"], model.NotificationRecipients);
    }

    [Fact]
    public async Task Index_DevelopmentEnvironment_AllowsReset()
    {
        var controller = CreateController(environmentName: "Development");

        var view = Assert.IsType<ViewResult>(await controller.Index(CancellationToken.None));
        var model = Assert.IsType<InvoiceGenerationViewModel>(view.Model);

        Assert.True(model.CanReset);
    }

    [Fact]
    public async Task Index_ProductionEnvironment_DisallowsReset()
    {
        var controller = CreateController(environmentName: "Production");

        var view = Assert.IsType<ViewResult>(await controller.Index(CancellationToken.None));
        var model = Assert.IsType<InvoiceGenerationViewModel>(view.Model);

        Assert.False(model.CanReset);
    }

    [Fact]
    public async Task Generate_Success_SetsSuccessNotificationAndRedirects()
    {
        var apiClient = new FakeInvoiceApiClient
        {
            GenerateResult = new InvoiceGenerationResponse(true, 5, "invoices/PT_Invoices_2026-01-01-00-00-00.csv", null)
        };
        var controller = CreateController(apiClient);

        var result = await controller.Generate(CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        var notification = controller.TempData.GetNotification();
        Assert.NotNull(notification);
        Assert.Equal(NotificationType.Success, notification!.Type);
    }

    [Fact]
    public async Task Generate_Failure_SetsErrorNotificationWithLegacyMessage()
    {
        var apiClient = new FakeInvoiceApiClient
        {
            GenerateResult = new InvoiceGenerationResponse(false, 0, null, "Invoices could not be generated. Please try again later.")
        };
        var controller = CreateController(apiClient);

        var result = await controller.Generate(CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        var notification = controller.TempData.GetNotification();
        Assert.NotNull(notification);
        Assert.Equal(NotificationType.Error, notification!.Type);
        Assert.Equal("Invoices could not be generated. Please try again later.", notification.Message);
    }

    [Fact]
    public async Task Reset_DevelopmentEnvironment_CallsApiClientAndRedirects()
    {
        var apiClient = new FakeInvoiceApiClient();
        var controller = CreateController(apiClient, environmentName: "Development");

        var result = await controller.Reset(CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.True(apiClient.ResetCalled);
    }

    [Fact]
    public async Task Reset_ProductionEnvironment_ReturnsNotFoundAndDoesNotCallApiClient()
    {
        var apiClient = new FakeInvoiceApiClient();
        var controller = CreateController(apiClient, environmentName: "Production");

        var result = await controller.Reset(CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        Assert.False(apiClient.ResetCalled);
    }

    [Fact]
    public async Task AuditHistory_ReturnsViewWithHistory()
    {
        var apiClient = new FakeInvoiceApiClient
        {
            AuditHistory = [new InvoiceAuditRecordResponse(Guid.NewGuid(), "test.user", DateTime.UtcNow)]
        };
        var controller = CreateController(apiClient);

        var result = await controller.AuditHistory(CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<IReadOnlyList<InvoiceAuditRecordResponse>>(view.Model);
        Assert.Single(model);
    }
}
