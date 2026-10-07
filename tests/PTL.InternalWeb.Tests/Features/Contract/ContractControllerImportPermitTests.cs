using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using PTL.Contracts.Contract;
using PTL.InternalWeb.Features.Contract;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Features.Contract;

public class ContractControllerImportPermitTests
{
    private static ContractController CreateController(FakeImportPermitApiClient importPermitApiClient) =>
        new(new FakeContractApiClient(), new FakeCustomerApiClient(), new FakeLookupApiClient(), importPermitApiClient, NullLogger<ContractController>.Instance, new FakeContractExportApiClient(), new FakeContractRenewalApiClient(), new FakeExportTemplateApiClient(), new FakeBulkExportApiClient(), new PTL.Core.Contract.Document.TemplateMergeService());

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

        var result = await controller.ImportPermits(contractId, null, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ImportPermitsViewModel>(view.Model);
        Assert.Equal(contractId, model.ContractId);
        Assert.Single(model.Permits);
        Assert.Equal("PT0001", model.Permits[0].SchemeNumber);
        Assert.Null(model.EditParticipantSchemeId);
    }

    [Fact]
    public async Task ImportPermits_Get_WithEditParticipantSchemeId_SetsEditState()
    {
        var contractId = Guid.NewGuid();
        var participantSchemeId = Guid.NewGuid();
        var importPermitApiClient = new FakeImportPermitApiClient
        {
            Permits = [new ImportPermitResponse(participantSchemeId, "PT0001", "AHS", "1476", true, false, null)]
        };
        var controller = CreateController(importPermitApiClient);

        var result = await controller.ImportPermits(contractId, participantSchemeId, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ImportPermitsViewModel>(view.Model);
        Assert.Equal(participantSchemeId, model.EditParticipantSchemeId);
    }

    [Fact]
    public async Task UpdateImportPermit_ReceivedWithoutExpiry_ReturnsViewWithModelErrorAndRowStillEditing()
    {
        var contractId = Guid.NewGuid();
        var participantSchemeId = Guid.NewGuid();
        var importPermitApiClient = new FakeImportPermitApiClient
        {
            Permits = [new ImportPermitResponse(participantSchemeId, "PT0001", "AHS", "1476", true, false, null)]
        };
        var controller = CreateController(importPermitApiClient);

        var result = await controller.UpdateImportPermit(contractId, participantSchemeId, true, null, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("ImportPermits", view.ViewName);
        var model = Assert.IsType<ImportPermitsViewModel>(view.Model);
        Assert.Equal(participantSchemeId, model.EditParticipantSchemeId);
        Assert.False(controller.ModelState.IsValid);
        Assert.Empty(importPermitApiClient.UpdateCalls);
    }

    [Fact]
    public async Task UpdateImportPermit_ReceivedWithUnparsableExpiryText_ReturnsInvalidDateMessage()
    {
        var contractId = Guid.NewGuid();
        var participantSchemeId = Guid.NewGuid();
        var importPermitApiClient = new FakeImportPermitApiClient
        {
            Permits = [new ImportPermitResponse(participantSchemeId, "PT0001", "AHS", "1476", true, false, null)]
        };
        var controller = CreateController(importPermitApiClient);

        var result = await controller.UpdateImportPermit(contractId, participantSchemeId, true, "not-a-date", CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        var error = Assert.Single(controller.ModelState["importPermitExpiry"]!.Errors);
        Assert.Equal("Invalid date. Please enter a valid date in dd/mm/yyyy format.", error.ErrorMessage);
        Assert.Empty(importPermitApiClient.UpdateCalls);
    }

    [Fact]
    public async Task UpdateImportPermit_Valid_UpdatesAndRedirectsToViewMode()
    {
        var contractId = Guid.NewGuid();
        var participantSchemeId = Guid.NewGuid();
        var importPermitApiClient = new FakeImportPermitApiClient();
        var controller = CreateController(importPermitApiClient);

        var result = await controller.UpdateImportPermit(contractId, participantSchemeId, true, "01/01/2027", CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(ContractController.ImportPermits), redirect.ActionName);
        Assert.Single(importPermitApiClient.UpdateCalls);
        Assert.Equal(new DateTime(2027, 1, 1), importPermitApiClient.UpdateCalls[0].Request.ImportPermitExpiry);
        Assert.True(importPermitApiClient.UpdateCalls[0].Request.ImportPermitReceived);
    }

    [Fact]
    public async Task UpdateImportPermit_NotReceived_DoesNotRequireExpiry()
    {
        var contractId = Guid.NewGuid();
        var participantSchemeId = Guid.NewGuid();
        var importPermitApiClient = new FakeImportPermitApiClient();
        var controller = CreateController(importPermitApiClient);

        var result = await controller.UpdateImportPermit(contractId, participantSchemeId, false, null, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Single(importPermitApiClient.UpdateCalls);
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

        var result = await controller.ImportPermits(contractId, null, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ImportPermitsViewModel>(view.Model);
        Assert.Equal("01/01/2027", model.Permits[0].ImportPermitExpiry);
    }
}
