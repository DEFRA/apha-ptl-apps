using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PTL.Api.Controllers;
using PTL.Api.Tests.Contract;
using PTL.Contracts.Contract;
using PTL.Core.Contract;
using PTL.Core.Contract.ImportPermit;
using PTL.Core.Contract.PendingOrder;
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
        IRenewContractsService? renewContractsService = null,
        IPendingOrderService? pendingOrderService = null)
    {
        var controller = new ContractController(
            new ContractService(contractRepository ?? new FakeContractRepository(), participantSchemeRepository ?? new FakeParticipantSchemeRepository(), NullLogger<ContractService>.Instance),
            new ImportPermitService(new FakeImportPermitRepository()),
            sampleAddressService ?? new StubSampleAddressService(),
            contractRenewalService ?? new StubContractRenewalService(),
            renewContractsService ?? new StubRenewContractsService(),
            pendingOrderService ?? new StubPendingOrderService(),
            new StubExportTemplateService(),
            new StubBulkExportService(),
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

    [Fact]
    public async Task GetContractItems_WithSchemesAndParticipants_MapsNestedResponses()
    {
        var contractId = Guid.NewGuid();
        var schemeId = Guid.NewGuid();
        var participantSchemeId = Guid.NewGuid();
        var items = new ContractItemsAggregate
        {
            ContractId = contractId,
            Suffix = "A",
            YearId = 2027,
            QalNumber = "QAL/00001",
            Symbol = "£",
            DiscountRate = 0.1m,
            AdministrationCharge = 12m,
            NumberCourier = 2,
            CourierPrice = 5m,
            NumberPostage = 1,
            PostagePrice = 3m,
            NumberSpecialDelivery = 1,
            SpecialDeliveryPrice = 7m,
            IsReadOnly = false,
            Schemes =
            [
                new ContractItemSchemeGroup
                {
                    SchemeId = schemeId,
                    SchemeIdentifier = "SCH-001",
                    SchemeName = "Scheme One",
                    Participants =
                    [
                        new ContractItemLine
                        {
                            ParticipantSchemeId = participantSchemeId,
                            ParticipantId = Guid.NewGuid(),
                            SchemeId = schemeId,
                            LabCode = "LAB-1",
                            LabName = "Alpha Lab",
                            NumberOfDistributions = 5,
                            Price = 25m,
                            NonFeePaying = false,
                            HasOverride = true
                        }
                    ]
                }
            ]
        };

        var controller = CreateController(contractRepository: new FakeContractRepository { ContractItems = items });

        var result = await controller.GetContractItems(contractId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ContractItemsResponse>(ok.Value);
        var scheme = Assert.Single(response.Schemes);
        var participant = Assert.Single(scheme.Participants);
        Assert.Equal("SCH-001", scheme.SchemeIdentifier);
        Assert.Equal("Scheme One", scheme.SchemeName);
        Assert.Equal("LAB-1", participant.LabCode);
        Assert.Equal("Alpha Lab", participant.LabName);
        Assert.Equal(participantSchemeId, participant.ParticipantSchemeId);
        Assert.False(participant.NonFeePaying);
        Assert.True(participant.HasOverride);
    }

    [Fact]
    public async Task GetSampleAddresses_WithFeePayingSchemes_MapsNestedSchemeResponses()
    {
        var contractId = Guid.NewGuid();
        var scheme = new SampleAddressSchemeEntity
        {
            ContractId = contractId,
            ParticipantId = Guid.NewGuid(),
            ParticipantSchemeId = Guid.NewGuid(),
            SchemeName = "Scheme One",
            SchemeIdentifier = "SCH-001",
            MonthsActive = "Jan, Feb",
            WeekNumber = 3
        };

        var address = new SampleAddressEntity
        {
            ContractId = contractId,
            ParticipantId = Guid.NewGuid(),
            QalNumber = "QAL/00001",
            Organisation = "Alpha Labs",
            FeePayingSchemes = [scheme]
        };

        var controller = CreateController(sampleAddressService: new FixedSampleAddressService([address]));

        var result = await controller.GetSampleAddresses(contractId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<List<SampleAddressResponse>>(ok.Value);
        var feePaying = Assert.Single(response[0].FeePayingSchemes);
        Assert.Equal("Scheme One", feePaying.SchemeName);
        Assert.Equal("SCH-001", feePaying.SchemeIdentifier);
        Assert.Equal("Jan, Feb", feePaying.MonthsActive);
        Assert.Equal(3, feePaying.WeekNumber);
    }

    [Fact]
    public async Task GetRenewableContracts_AndRenewableItems_MapContractAndItemDtos()
    {
        var customerId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        var participantSchemeId = Guid.NewGuid();
        var contract = new RenewableContractEntity
        {
            ContractId = contractId,
            Suffix = "A",
            ContractSignatory = "Alice Example",
            RenewalInformation = "Renewal note",
            ActionsRequired = "Review action",
            IsActive = true,
            NoOfItems = 1
        };
        var item = new RenewableContractItemEntity
        {
            ContractId = contractId,
            Suffix = "A",
            ParticipantSchemeId = participantSchemeId,
            LabCode = "LAB-1",
            LabName = "Alpha Lab",
            OldSchemeIdentifier = "OLD-001",
            OldSchemeName = "Old Scheme",
            NewSchemeIdentifier = "NEW-001",
            NewSchemeName = "New Scheme"
        };

        var controller = CreateController(
            renewContractsService: new FixedRenewContractsService(
                new RenewableContractsResult(new RenewEligibility(true, null), ["Alice Example"], [contract]),
                [item]));

        var contractsResult = await controller.GetRenewableContracts(customerId, CancellationToken.None);
        var itemsResult = await controller.GetRenewableItems(customerId, CancellationToken.None);

        var contractsOk = Assert.IsType<OkObjectResult>(contractsResult.Result);
        var contracts = Assert.IsType<RenewableContractsResponse>(contractsOk.Value);
        var contractDto = Assert.Single(contracts.Contracts);
        Assert.Equal("A", contractDto.Suffix);
        Assert.Equal("Alice Example", contractDto.ContractSignatory);

        var itemsOk = Assert.IsType<OkObjectResult>(itemsResult.Result);
        var items = Assert.IsType<RenewableContractItemsResponse>(itemsOk.Value);
        var itemDto = Assert.Single(items.Items);
        Assert.Equal("LAB-1", itemDto.LabCode);
        Assert.Equal("OLD-001", itemDto.OldSchemeIdentifier);
        Assert.Equal("NEW-001", itemDto.NewSchemeIdentifier);
        Assert.True(itemDto.IsRenewable);
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

    private sealed class FixedRenewContractsService(
        RenewableContractsResult renewableContractsResult,
        IReadOnlyList<RenewableContractItemEntity> renewableItems) : IRenewContractsService
    {
        public Task<RenewableContractsResult> GetRenewableContractsAsync(Guid customerId, CancellationToken cancellationToken = default) =>
            Task.FromResult(renewableContractsResult);

        public Task<IReadOnlyList<RenewableContractItemEntity>> GetRenewableItemsAsync(Guid customerId, CancellationToken cancellationToken = default) =>
            Task.FromResult(renewableItems);

        public Task<RenewContractsResult> RenewContractsAsync(
            Guid customerId,
            IReadOnlyList<Guid> contractIds,
            IReadOnlyList<Guid> participantSchemeIds,
            string? newContractSignatory,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new RenewContractsResult(true, Guid.NewGuid(), null));
    }
}
