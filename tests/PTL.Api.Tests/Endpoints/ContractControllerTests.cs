using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PTL.Api.Controllers;
using PTL.Api.Tests.Contract;
using PTL.Contracts.Contract;
using PTL.Core.Contract;

namespace PTL.Api.Tests.Endpoints;

public class ContractControllerTests
{
    private static ContractController CreateController(FakeContractRepository repository)
    {
        var controller = new ContractController(new ContractService(repository, NullLogger<ContractService>.Instance), NullLogger<ContractController>.Instance);

        // ValidationProblem() resolves ProblemDetailsFactory from HttpContext.RequestServices,
        // which a bare controller instance does not have without this wiring.
        var services = new ServiceCollection().AddMvc().Services.BuildServiceProvider();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { RequestServices = services }
        };

        return controller;
    }

    private static ContractRequest ValidCreateRequest(int? yearId = null) => new(
        YearId: yearId ?? DateTime.UtcNow.Year + 1,
        UTNumber: "UT12345",
        FTNumber: string.Empty,
        ContractSignatory: "Alice Example",
        ActionsRequired: string.Empty,
        RenewalInformation: string.Empty,
        DiscountRate: 0,
        AdministrationCharge: 0,
        NumberCourier: 0,
        CourierPrice: 0,
        NumberPostage: 0,
        PostagePrice: 0,
        NumberSpecialDelivery: 0,
        SpecialDeliveryPrice: 0,
        AcknowledgementPostedDate: new DateTime(2026, 1, 1),
        AcknowledgementReturnedDate: new DateTime(2026, 1, 5),
        JobSheetPostedDate: new DateTime(2026, 1, 10),
        ReasonForClosure: string.Empty,
        DateOfLeaving: new DateTime(2026, 12, 31),
        IsActive: true,
        Suffix: "A",
        PurchaseOrderNumber: string.Empty,
        OptOutOfInvoiceGeneration: false,
        IsOnlineOrder: false);

    private static ContractRequest ToUpdateRequest(ContractRequest request) => request;

    [Fact]
    public async Task CreateContract_ValidRequest_ReturnsCreatedAtAction()
    {
        var controller = CreateController(new FakeContractRepository());

        var result = await controller.CreateContract(Guid.NewGuid(), ValidCreateRequest(), CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(ContractController.GetContract), created.ActionName);
        Assert.IsType<ContractResponse>(created.Value);
    }

    [Fact]
    public async Task CreateContract_BothUtAndFtNumbers_ReturnsValidationProblem()
    {
        var controller = CreateController(new FakeContractRepository());
        var request = ValidCreateRequest() with { FTNumber = "FT12345" };

        var result = await controller.CreateContract(Guid.NewGuid(), request, CancellationToken.None);

        var badRequest = Assert.IsType<ObjectResult>(result.Result, exactMatch: false);
        Assert.Equal(400, badRequest.StatusCode);
    }

    [Fact]
    public async Task GetContract_UnknownContract_ReturnsNotFound()
    {
        var controller = CreateController(new FakeContractRepository());

        var result = await controller.GetContract(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetContract_ExistingContract_ReturnsOk()
    {
        var repository = new FakeContractRepository();
        var controller = CreateController(repository);
        var customerId = Guid.NewGuid();
        var created = await controller.CreateContract(customerId, ValidCreateRequest(), CancellationToken.None);
        var contractId = ((ContractResponse)((CreatedAtActionResult)created.Result!).Value!).ContractId;

        var result = await controller.GetContract(contractId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(contractId, ((ContractResponse)ok.Value!).ContractId);
    }

    [Fact]
    public async Task GetContract_ExistingContract_ReturnsFullyMappedResponse()
    {
        var repository = new FakeContractRepository();
        var controller = CreateController(repository);
        var customerId = Guid.NewGuid();
        var request = ValidCreateRequest();
        var created = await controller.CreateContract(customerId, request, CancellationToken.None);
        var contractId = ((ContractResponse)((CreatedAtActionResult)created.Result!).Value!).ContractId;

        var result = await controller.GetContract(contractId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ContractResponse>(ok.Value);
        Assert.Equal(contractId, response.ContractId);
        Assert.Equal(customerId, response.CustomerId);
        Assert.Equal("Sample Laboratories Ltd", response.CustomerName);
        Assert.Equal("QAL/00001", response.QalNumber);
        Assert.Equal(request.YearId, response.YearId);
        Assert.Equal(request.UTNumber, response.UTNumber);
        Assert.Equal(request.FTNumber, response.FTNumber);
        Assert.Equal(request.ContractSignatory, response.ContractSignatory);
        Assert.Equal(request.ActionsRequired, response.ActionsRequired);
        Assert.Equal(request.RenewalInformation, response.RenewalInformation);
        Assert.Equal(request.DiscountRate, response.DiscountRate);
        Assert.Equal(request.AdministrationCharge, response.AdministrationCharge);
        Assert.Equal(request.NumberCourier, response.NumberCourier);
        Assert.Equal(request.CourierPrice, response.CourierPrice);
        Assert.Equal(request.NumberPostage, response.NumberPostage);
        Assert.Equal(request.PostagePrice, response.PostagePrice);
        Assert.Equal(request.NumberSpecialDelivery, response.NumberSpecialDelivery);
        Assert.Equal(request.SpecialDeliveryPrice, response.SpecialDeliveryPrice);
        Assert.Equal(request.AcknowledgementPostedDate, response.AcknowledgementPostedDate);
        Assert.Equal(request.AcknowledgementReturnedDate, response.AcknowledgementReturnedDate);
        Assert.Equal(request.JobSheetPostedDate, response.JobSheetPostedDate);
        Assert.Equal(request.ReasonForClosure, response.ReasonForClosure);
        Assert.Equal(request.DateOfLeaving, response.DateOfLeaving);
        Assert.Equal(request.IsActive, response.IsActive);
        Assert.False(response.IsReadOnly);
        Assert.Equal(request.Suffix, response.Suffix);
        Assert.NotNull(response.CommencementDate);
        Assert.Equal(request.PurchaseOrderNumber, response.PurchaseOrderNumber);
        Assert.Equal(request.OptOutOfInvoiceGeneration, response.OptOutOfInvoiceGeneration);
        Assert.False(response.IsInvoiceSent);
        Assert.Equal(request.IsOnlineOrder, response.IsOnlineOrder);
        Assert.Null(response.ApprovedBy);
        Assert.Null(response.ApprovedDate);
    }

    [Fact]
    public async Task GetContractsForCustomer_ByYear_ReturnsExactMatch()
    {
        var repository = new FakeContractRepository();
        var controller = CreateController(repository);
        var customerId = Guid.NewGuid();
        var year = DateTime.UtcNow.Year + 1;
        await controller.CreateContract(customerId, ValidCreateRequest(year), CancellationToken.None);

        var result = await controller.GetContractsForCustomer(customerId, new ContractSearchRequest(YearId: year), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ContractSearchResponse>(ok.Value);
        Assert.Single(response.Items);
        Assert.Equal(year, response.Items[0].YearId);
    }

    [Fact]
    public async Task GetContractsForCustomer_SearchBySuffix_FiltersResults()
    {
        var repository = new FakeContractRepository();
        var controller = CreateController(repository);
        var customerId = Guid.NewGuid();
        var year = DateTime.UtcNow.Year + 1;
        await controller.CreateContract(customerId, ValidCreateRequest(year) with { Suffix = "A" }, CancellationToken.None);
        await controller.CreateContract(customerId, ValidCreateRequest(year) with { Suffix = "B" }, CancellationToken.None);

        var result = await controller.GetContractsForCustomer(customerId, new ContractSearchRequest(Period: ContractPeriodFilter.All, SearchTerm: "B"), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ContractSearchResponse>(ok.Value);
        Assert.Single(response.Items);
        Assert.Equal("B", response.Items[0].Suffix);
    }

    [Fact]
    public async Task UpdateContract_UnknownContract_ReturnsNotFound()
    {
        var controller = CreateController(new FakeContractRepository());

        var result = await controller.UpdateContract(Guid.NewGuid(), ToUpdateRequest(ValidCreateRequest()), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task UpdateContract_ReadOnlyContract_ReturnsValidationProblem()
    {
        var repository = new FakeContractRepository();
        var controller = CreateController(repository);
        var customerId = Guid.NewGuid();
        var created = await controller.CreateContract(customerId, ValidCreateRequest(DateTime.UtcNow.Year - 1), CancellationToken.None);
        var contractId = ((ContractResponse)((CreatedAtActionResult)created.Result!).Value!).ContractId;

        var result = await controller.UpdateContract(contractId, ToUpdateRequest(ValidCreateRequest(DateTime.UtcNow.Year - 1)), CancellationToken.None);

        var badRequest = Assert.IsType<ObjectResult>(result.Result, exactMatch: false);
        Assert.Equal(400, badRequest.StatusCode);
    }
}
