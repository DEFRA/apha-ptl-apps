using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PTL.Api.Controllers;
using PTL.Api.Tests.Contract;
using PTL.Contracts.Contract;
using PTL.Core.Contract;
using PTL.Core.Contract.ImportPermit;
using PTL.Core.Contract.Renew;
using PTL.Core.Contract.Renewal;
using PTL.Core.Contract.SampleAddress;
using PTL.Core.Participant;

namespace PTL.Api.Tests.Endpoints;

// Covers the ContractController actions the other Endpoints/ContractController* test files don't
// touch at all: ContractItems/RemoveContractItem, SampleAddresses, Renewal, and the three
// Renew-Contracts endpoints.
public class ContractControllerAdditionalEndpointsTests
{
    private static ContractController CreateController(
        FakeContractRepository? contractRepository = null,
        FakeParticipantSchemeRepository? participantSchemeRepository = null,
        ISampleAddressService? sampleAddressService = null,
        IContractRenewalService? contractRenewalService = null,
        IRenewContractsService? renewContractsService = null)
    {
        var controller = new ContractController(
            new ContractService(contractRepository ?? new FakeContractRepository(), participantSchemeRepository ?? new FakeParticipantSchemeRepository(), NullLogger<ContractService>.Instance),
            new ImportPermitService(new FakeImportPermitRepository()),
            sampleAddressService ?? new StubSampleAddressService(),
            contractRenewalService ?? new StubContractRenewalService(),
            renewContractsService ?? new StubRenewContractsService(),
            NullLogger<ContractController>.Instance);

        var services = new ServiceCollection().AddMvc().Services.BuildServiceProvider();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { RequestServices = services }
        };

        return controller;
    }

    private static ContractItemsAggregate ValidItems(Guid contractId) => new()
    {
        ContractId = contractId,
        Suffix = "A",
        YearId = DateTime.UtcNow.Year,
        QalNumber = "QAL/00001",
        Symbol = "£",
        DiscountRate = 0,
        AdministrationCharge = 10m,
        NumberCourier = 1,
        CourierPrice = 5m,
        NumberPostage = 0,
        PostagePrice = 0m,
        NumberSpecialDelivery = 0,
        SpecialDeliveryPrice = 0m,
        IsReadOnly = false
    };

    [Fact]
    public async Task GetContractItems_ExistingContract_ReturnsOkWithMappedResponse()
    {
        var repository = new FakeContractRepository { ContractItems = ValidItems(Guid.NewGuid()) };
        var controller = CreateController(repository);

        var result = await controller.GetContractItems(repository.ContractItems!.ContractId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ContractItemsResponse>(ok.Value);
        Assert.Equal(repository.ContractItems.ContractId, response.ContractId);
    }

    [Fact]
    public async Task GetContractItems_UnknownContract_ReturnsNotFound()
    {
        var controller = CreateController();

        var result = await controller.GetContractItems(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task RemoveContractItem_ValidItem_ReturnsNoContent()
    {
        var contractRepository = new FakeContractRepository();
        var contractId = Guid.NewGuid();
        contractRepository.Seed(new PTL.Core.Contract.Contract { ContractId = contractId, IsReadOnly = false });
        var participantSchemeRepository = new FakeParticipantSchemeRepository();
        var participantSchemeId = Guid.NewGuid();
        participantSchemeRepository.Add(new ParticipantSchemeRecord { ParticipantSchemeId = participantSchemeId, ContractId = contractId });
        var controller = CreateController(contractRepository, participantSchemeRepository);

        var result = await controller.RemoveContractItem(contractId, participantSchemeId, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task RemoveContractItem_UnknownItem_ReturnsNotFound()
    {
        var controller = CreateController();

        var result = await controller.RemoveContractItem(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task RemoveContractItem_ReadOnlyContract_ReturnsValidationProblem()
    {
        var contractRepository = new FakeContractRepository();
        var contractId = Guid.NewGuid();
        contractRepository.Seed(new PTL.Core.Contract.Contract { ContractId = contractId, IsReadOnly = true });
        var controller = CreateController(contractRepository);

        var result = await controller.RemoveContractItem(contractId, Guid.NewGuid(), CancellationToken.None);

        var problem = Assert.IsType<BadRequestObjectResult>(result);
        Assert.IsType<Microsoft.AspNetCore.Mvc.ValidationProblemDetails>(problem.Value, exactMatch: false);
    }

    [Fact]
    public async Task GetSampleAddresses_ReturnsOkWithMappedResponses()
    {
        var address = new SampleAddressEntity { ContractId = Guid.NewGuid(), LabCode = "1000", Organisation = "Sample Labs" };
        var controller = CreateController(sampleAddressService: new FixedSampleAddressService([address]));

        var result = await controller.GetSampleAddresses(address.ContractId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<List<SampleAddressResponse>>(ok.Value);
        Assert.Single(response);
        Assert.Equal("Sample Labs", response[0].Organisation);
    }

    [Fact]
    public async Task GetRenewal_ExistingContract_ReturnsOkWithMappedResponse()
    {
        var renewal = new ContractRenewalEntity { ContractId = Guid.NewGuid(), OrganisationName = "Sample Labs" };
        var controller = CreateController(contractRenewalService: new FixedContractRenewalService(renewal));

        var result = await controller.GetRenewal(renewal.ContractId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ContractRenewalResponse>(ok.Value);
        Assert.Equal("Sample Labs", response.OrganisationName);
    }

    [Fact]
    public async Task GetRenewal_UnknownContract_ReturnsNotFound()
    {
        var controller = CreateController(contractRenewalService: new FixedContractRenewalService(null));

        var result = await controller.GetRenewal(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetRenewableContracts_ReturnsOkWithEligibilityAndContracts()
    {
        var controller = CreateController();

        var result = await controller.GetRenewableContracts(Guid.NewGuid(), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.IsType<RenewableContractsResponse>(ok.Value);
    }

    [Fact]
    public async Task GetRenewableItems_ReturnsOkWithItems()
    {
        var controller = CreateController();

        var result = await controller.GetRenewableItems(Guid.NewGuid(), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.IsType<RenewableContractItemsResponse>(ok.Value);
    }

    [Fact]
    public async Task RenewContracts_ReturnsOkWithResult()
    {
        var controller = CreateController();
        var request = new RenewContractRequest([Guid.NewGuid()], [Guid.NewGuid()], "Alice Example");

        var result = await controller.RenewContracts(Guid.NewGuid(), request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<RenewContractResponse>(ok.Value);
        Assert.True(response.Success);
    }

    private sealed class FixedSampleAddressService(IReadOnlyList<SampleAddressEntity> addresses) : ISampleAddressService
    {
        public Task<IReadOnlyList<SampleAddressEntity>> GetByContractIdAsync(Guid contractId, CancellationToken cancellationToken = default) =>
            Task.FromResult(addresses);
    }

    private sealed class FixedContractRenewalService(ContractRenewalEntity? renewal) : IContractRenewalService
    {
        public Task<ContractRenewalEntity?> GetByContractIdAsync(Guid contractId, CancellationToken cancellationToken = default) =>
            Task.FromResult(renewal);
    }
}
