using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PTL.Api.Controllers;
using PTL.Api.Tests.Contract;
using PTL.Contracts.Participant;
using PTL.Core.Participant;
using CoreContract = PTL.Core.Contract.Contract;

namespace PTL.Api.Tests.Endpoints;

public class ParticipantSchemeControllerTests
{
    private static (ParticipantSchemeController Controller, FakeParticipantSchemeRepository Repository, FakeContractRepository Contracts) CreateController()
    {
        var repository = new FakeParticipantSchemeRepository();
        var contracts = new FakeContractRepository();
        var service = new ParticipantSchemeService(repository, contracts, NullLogger<ParticipantSchemeService>.Instance);
        var controller = new ParticipantSchemeController(service, NullLogger<ParticipantSchemeController>.Instance);

        var services = new ServiceCollection().AddMvc().Services.BuildServiceProvider();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { RequestServices = services }
        };

        return (controller, repository, contracts);
    }

    private static CreateParticipantSchemeRequest ValidCreateRequest(Guid contractId, Guid participantId, Guid schemeId) => new(
        contractId, participantId, schemeId,
        true, false, false, false, false, false, false, false, false, false, false, false,
        1, "EXT-1", "Contact", false, false, false, "Instructions", true, false,
        false, false, false, false, false, false, false, false, false, false, false, false);

    private static UpdateParticipantSchemeRequest ValidUpdateRequest() => new(
        true, false, false, false, false, false, false, false, false, false, false, false,
        2, "EXT-2", "Contact updated", false, false, false, "Instructions updated", true, false,
        false, false, false, false, false, false, false, false, false, false, false, false);

    [Fact]
    public async Task GetParticipantScheme_UnknownId_ReturnsNotFound()
    {
        var (controller, _, _) = CreateController();

        var result = await controller.GetParticipantScheme(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task CreateParticipantScheme_ValidRequest_ReturnsCreated()
    {
        var (controller, _, contracts) = CreateController();
        var contractId = Guid.NewGuid();
        contracts.Seed(new CoreContract { ContractId = contractId, CustomerId = Guid.NewGuid(), YearId = DateTime.UtcNow.Year });

        var result = await controller.CreateParticipantScheme(ValidCreateRequest(contractId, Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.IsType<ParticipantSchemeResponse>(created.Value);
    }

    [Fact]
    public async Task CreateParticipantScheme_ReadOnlyContract_ReturnsValidationProblem()
    {
        var (controller, _, contracts) = CreateController();
        var contractId = Guid.NewGuid();
        contracts.Seed(new CoreContract { ContractId = contractId, CustomerId = Guid.NewGuid(), YearId = DateTime.UtcNow.Year, IsReadOnly = true });

        var result = await controller.CreateParticipantScheme(ValidCreateRequest(contractId, Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task UpdateParticipantScheme_UnknownId_ReturnsNotFound()
    {
        var (controller, _, _) = CreateController();

        var result = await controller.UpdateParticipantScheme(Guid.NewGuid(), ValidUpdateRequest(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task UpdateParticipantScheme_ExistingRecord_ReturnsUpdatedResponse()
    {
        var (controller, repository, contracts) = CreateController();
        var contractId = Guid.NewGuid();
        contracts.Seed(new CoreContract { ContractId = contractId, CustomerId = Guid.NewGuid(), YearId = DateTime.UtcNow.Year });
        var existing = new ParticipantSchemeRecord { ParticipantSchemeId = Guid.NewGuid(), ContractId = contractId, ParticipantId = Guid.NewGuid(), SchemeId = Guid.NewGuid(), NumberOfSetsRequired = 1 };
        repository.Add(existing);

        var result = await controller.UpdateParticipantScheme(existing.ParticipantSchemeId, ValidUpdateRequest(), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ParticipantSchemeResponse>(ok.Value);
        Assert.Equal(2, response.NumberOfSetsRequired);
    }

    [Fact]
    public async Task DeleteParticipantScheme_ExistingRecord_ReturnsNoContent()
    {
        var (controller, repository, contracts) = CreateController();
        var contractId = Guid.NewGuid();
        contracts.Seed(new CoreContract { ContractId = contractId, CustomerId = Guid.NewGuid(), YearId = DateTime.UtcNow.Year });
        var existing = new ParticipantSchemeRecord { ParticipantSchemeId = Guid.NewGuid(), ContractId = contractId, ParticipantId = Guid.NewGuid(), SchemeId = Guid.NewGuid(), NumberOfSetsRequired = 1 };
        repository.Add(existing);

        var result = await controller.DeleteParticipantScheme(existing.ParticipantSchemeId, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task DeleteParticipantScheme_UnknownId_ReturnsNotFound()
    {
        var (controller, _, _) = CreateController();

        var result = await controller.DeleteParticipantScheme(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }
}
