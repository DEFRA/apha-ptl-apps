using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using PTL.Contracts.Contract;
using PTL.InternalWeb.Features.Contract;
using PTL.InternalWeb.Tests.TestSupport;
using PTL.SharedUI.Notifications;

namespace PTL.InternalWeb.Tests.Features.Contract;

public class ContractControllerImportPermitTests
{
    private static readonly Guid CustomerId = Guid.NewGuid();

    private static ContractController CreateController(FakeImportPermitApiClient importPermitApiClient)
    {
        var contractId = Guid.NewGuid();
        var contractApiClient = new FakeContractApiClient
        {
            ContractResponse = new ContractResponse(
                contractId, CustomerId, "Sample Laboratories Ltd", "QAL/00001", DateTime.UtcNow.Year, "UT3/306",
                string.Empty, "Alice Example", string.Empty, string.Empty, 0.1m, 25m, 2, 5m, 3, 4m, 1, 6m,
                null, null, null, string.Empty, null, true, false, "A", DateTime.UtcNow, string.Empty, false, false, false, null, null)
        };

        return new(contractApiClient, new FakeCustomerApiClient(), new FakeLookupApiClient(), importPermitApiClient, NullLogger<ContractController>.Instance, new FakeContractExportApiClient(), new FakeContractRenewalApiClient(), new FakeExportTemplateApiClient(), new FakeBulkExportApiClient(), new PTL.Core.Contract.Document.TemplateMergeService())
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), new FakeTempDataProvider())
        };
    }

    private static ImportPermitResponse Permit(Guid id, bool required = true, bool received = false, DateTime? expiry = null) =>
        new(id, "PT0001", "AHS", "1476", required, received, expiry);

    private static ImportPermitPendingRow Pending(Guid id, bool received, string? expiry) =>
        new() { ParticipantSchemeId = id, ImportPermitReceived = received, ImportPermitExpiry = expiry };

    [Fact]
    public async Task ImportPermits_Get_ReturnsViewWithPermits()
    {
        var contractId = Guid.NewGuid();
        var participantSchemeId = Guid.NewGuid();
        var importPermitApiClient = new FakeImportPermitApiClient
        {
            Permits = [new ImportPermitResponse(participantSchemeId, "PT0001", "AHS", "1476", true, false, null)]
        };
        var controller = CreateController(importPermitApiClient);

        var result = await controller.ImportPermits(contractId, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ImportPermitsViewModel>(view.Model);
        Assert.Equal(contractId, model.ContractId);
        Assert.Single(model.Permits);
        Assert.Equal("PT0001", model.Permits[0].SchemeNumber);
        Assert.Null(model.EditParticipantSchemeId);
    }

    [Fact]
    public async Task Edit_PutsRowInEditModeAndKeepsOtherPendingChanges_WithoutPersisting()
    {
        var contractId = Guid.NewGuid();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var importPermitApiClient = new FakeImportPermitApiClient { Permits = [Permit(first), Permit(second)] };
        var controller = CreateController(importPermitApiClient);

        var result = await controller.ImportPermits(
            contractId, [Pending(first, true, "01/01/2027"), Pending(second, false, null)], ImportPermitCommands.Edit, second, null, null, CancellationToken.None);

        var model = Assert.IsType<ImportPermitsViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(second, model.EditParticipantSchemeId);
        Assert.True(model.Permits[0].ImportPermitReceived);
        Assert.Equal("01/01/2027", model.Permits[0].ImportPermitExpiry);
        Assert.Empty(importPermitApiClient.UpdateCalls);
        Assert.Null(controller.TempData.GetNotification());
    }

    [Fact]
    public async Task Update_AppliesRowToPendingListOnly_NoDatabaseWriteAndNoNotification()
    {
        var contractId = Guid.NewGuid();
        var participantSchemeId = Guid.NewGuid();
        var importPermitApiClient = new FakeImportPermitApiClient { Permits = [Permit(participantSchemeId)] };
        var controller = CreateController(importPermitApiClient);

        var result = await controller.ImportPermits(
            contractId, [Pending(participantSchemeId, false, null)], ImportPermitCommands.Update, participantSchemeId, true, "01/01/2027", CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ImportPermitsViewModel>(view.Model);
        Assert.Null(model.EditParticipantSchemeId);
        Assert.True(model.Permits[0].ImportPermitReceived);
        Assert.Equal("01/01/2027", model.Permits[0].ImportPermitExpiry);
        Assert.Empty(importPermitApiClient.UpdateCalls);
        Assert.Null(controller.TempData.GetNotification());
    }

    [Fact]
    public async Task Update_NotReceived_ClearsExpiryAndDoesNotRequireIt()
    {
        var contractId = Guid.NewGuid();
        var participantSchemeId = Guid.NewGuid();
        var importPermitApiClient = new FakeImportPermitApiClient { Permits = [Permit(participantSchemeId, received: true, expiry: new DateTime(2027, 1, 1))] };
        var controller = CreateController(importPermitApiClient);

        var result = await controller.ImportPermits(
            contractId, [Pending(participantSchemeId, true, "01/01/2027")], ImportPermitCommands.Update, participantSchemeId, false, "01/01/2027", CancellationToken.None);

        var model = Assert.IsType<ImportPermitsViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.True(controller.ModelState.IsValid);
        Assert.False(model.Permits[0].ImportPermitReceived);
        Assert.Null(model.Permits[0].ImportPermitExpiry);
        Assert.Empty(importPermitApiClient.UpdateCalls);
    }

    [Fact]
    public async Task Update_ReceivedWithoutExpiry_RedisplaysRowInEditModeWithError()
    {
        var contractId = Guid.NewGuid();
        var participantSchemeId = Guid.NewGuid();
        var importPermitApiClient = new FakeImportPermitApiClient { Permits = [Permit(participantSchemeId)] };
        var controller = CreateController(importPermitApiClient);

        var result = await controller.ImportPermits(
            contractId, [Pending(participantSchemeId, false, null)], ImportPermitCommands.Update, participantSchemeId, true, null, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("ImportPermits", view.ViewName);
        var model = Assert.IsType<ImportPermitsViewModel>(view.Model);
        Assert.Equal(participantSchemeId, model.EditParticipantSchemeId);
        Assert.True(model.EditReceived);
        Assert.False(model.Permits[0].ImportPermitReceived);
        Assert.False(controller.ModelState.IsValid);
        Assert.Equal("Expiry date required", Assert.Single(controller.ModelState["importPermitExpiry"]!.Errors).ErrorMessage);
        Assert.Empty(importPermitApiClient.UpdateCalls);
    }

    [Fact]
    public async Task Update_ReceivedWithUnparsableExpiryText_ReturnsInvalidDateMessage()
    {
        var contractId = Guid.NewGuid();
        var participantSchemeId = Guid.NewGuid();
        var importPermitApiClient = new FakeImportPermitApiClient { Permits = [Permit(participantSchemeId)] };
        var controller = CreateController(importPermitApiClient);

        var result = await controller.ImportPermits(
            contractId, [Pending(participantSchemeId, false, null)], ImportPermitCommands.Update, participantSchemeId, true, "not-a-date", CancellationToken.None);

        var model = Assert.IsType<ImportPermitsViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal("not-a-date", model.EditExpiry);
        var error = Assert.Single(controller.ModelState["importPermitExpiry"]!.Errors);
        Assert.Equal("Invalid date. Please enter a valid date in dd/mm/yyyy format.", error.ErrorMessage);
        Assert.Empty(importPermitApiClient.UpdateCalls);
    }

    [Fact]
    public async Task CancelEdit_DiscardsTheRowEditButKeepsEarlierPendingChanges()
    {
        var contractId = Guid.NewGuid();
        var participantSchemeId = Guid.NewGuid();
        var importPermitApiClient = new FakeImportPermitApiClient { Permits = [Permit(participantSchemeId)] };
        var controller = CreateController(importPermitApiClient);

        var result = await controller.ImportPermits(
            contractId, [Pending(participantSchemeId, true, "01/01/2027")], ImportPermitCommands.CancelEdit, participantSchemeId, false, null, CancellationToken.None);

        var model = Assert.IsType<ImportPermitsViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Null(model.EditParticipantSchemeId);
        Assert.True(model.Permits[0].ImportPermitReceived);
        Assert.Equal("01/01/2027", model.Permits[0].ImportPermitExpiry);
        Assert.Empty(importPermitApiClient.UpdateCalls);
        Assert.Null(controller.TempData.GetNotification());
    }

    [Fact]
    public async Task Save_PersistsEveryPermit_ShowsSuccessBannerAndReturnsToContractList()
    {
        var contractId = Guid.NewGuid();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var importPermitApiClient = new FakeImportPermitApiClient { Permits = [Permit(first), Permit(second)] };
        var controller = CreateController(importPermitApiClient);

        var result = await controller.ImportPermits(
            contractId, [Pending(first, true, "01/01/2027"), Pending(second, false, null)], ImportPermitCommands.Save, null, null, null, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(ContractController.Index), redirect.ActionName);
        Assert.Equal(CustomerId, redirect.RouteValues!["customerId"]);
        Assert.Equal(2, importPermitApiClient.UpdateCalls.Count);
        Assert.Equal(first, importPermitApiClient.UpdateCalls[0].ParticipantSchemeId);
        Assert.True(importPermitApiClient.UpdateCalls[0].Request.ImportPermitReceived);
        Assert.Equal(new DateTime(2027, 1, 1), importPermitApiClient.UpdateCalls[0].Request.ImportPermitExpiry);
        Assert.False(importPermitApiClient.UpdateCalls[1].Request.ImportPermitReceived);
        var notification = controller.TempData.GetNotification();
        Assert.NotNull(notification);
        Assert.Equal(NotificationType.Success, notification.Type);
        Assert.Equal("Import Permit(s) saved successfully.", notification.Message);
    }

    [Fact]
    public async Task Apply_PersistsAndStaysOnPage()
    {
        var contractId = Guid.NewGuid();
        var participantSchemeId = Guid.NewGuid();
        var importPermitApiClient = new FakeImportPermitApiClient { Permits = [Permit(participantSchemeId)] };
        var controller = CreateController(importPermitApiClient);

        var result = await controller.ImportPermits(
            contractId, [Pending(participantSchemeId, true, "01/01/2027")], ImportPermitCommands.Apply, null, null, null, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("ImportPermits", view.ViewName);
        Assert.Single(importPermitApiClient.UpdateCalls);
        Assert.Equal(NotificationType.Success, controller.TempData.GetNotification()!.Type);
    }

    [Fact]
    public async Task Save_WhenAWriteIsRejected_StaysOnPageWithErrorBanner()
    {
        var contractId = Guid.NewGuid();
        var participantSchemeId = Guid.NewGuid();
        var importPermitApiClient = new FakeImportPermitApiClient { Permits = [Permit(participantSchemeId)], UpdateResult = false };
        var controller = CreateController(importPermitApiClient);

        var result = await controller.ImportPermits(
            contractId, [Pending(participantSchemeId, true, "01/01/2027")], ImportPermitCommands.Save, null, null, null, CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.Equal(NotificationType.Error, controller.TempData.GetNotification()!.Type);
    }

    [Fact]
    public async Task Cancel_DiscardsEverything_NoWriteNoBannerAndReturnsToContractList()
    {
        var contractId = Guid.NewGuid();
        var participantSchemeId = Guid.NewGuid();
        var importPermitApiClient = new FakeImportPermitApiClient { Permits = [Permit(participantSchemeId)] };
        var controller = CreateController(importPermitApiClient);

        var result = await controller.ImportPermits(
            contractId, [Pending(participantSchemeId, true, "01/01/2027")], ImportPermitCommands.Cancel, null, null, null, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(ContractController.Index), redirect.ActionName);
        Assert.Empty(importPermitApiClient.UpdateCalls);
        Assert.Null(controller.TempData.GetNotification());
    }

    [Fact]
    public async Task ImportPermits_Get_WithExpiryDate_FormatsExpiryString()
    {
        var contractId = Guid.NewGuid();
        var participantSchemeId = Guid.NewGuid();
        var importPermitApiClient = new FakeImportPermitApiClient
        {
            Permits = [new ImportPermitResponse(participantSchemeId, "PT0001", "AHS", "1476", true, true, new DateTime(2027, 1, 1))]
        };
        var controller = CreateController(importPermitApiClient);

        var result = await controller.ImportPermits(contractId, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ImportPermitsViewModel>(view.Model);
        Assert.Equal("01/01/2027", model.Permits[0].ImportPermitExpiry);
    }
}
