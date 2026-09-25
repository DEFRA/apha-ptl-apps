using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using PTL.Api.Controllers;
using PTL.Api.Tests.Contract;
using PTL.Contracts.Contract;
using PTL.Core.Contract.ImportPermit;

namespace PTL.Api.Tests.Endpoints;

public class ImportPermitControllerTests
{
    private static (ImportPermitController Controller, FakeImportPermitRepository Repository) CreateController()
    {
        var repository = new FakeImportPermitRepository();
        var service = new ImportPermitService(repository);
        var controller = new ImportPermitController(service);

        var services = new ServiceCollection().AddMvc().Services.BuildServiceProvider();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { RequestServices = services }
        };

        return (controller, repository);
    }

    [Fact]
    public async Task GetImportPermits_ReturnsMappedResponses()
    {
        var (controller, repository) = CreateController();
        var participantSchemeId = Guid.NewGuid();
        repository.Permits =
        [
            new ImportPermitEntity
            {
                ParticipantSchemeId = participantSchemeId,
                SchemeNumber = "PT0001",
                SchemeName = "African Horse Sickness (AHS)",
                LabId = "1476",
                ImportPermitRequired = true,
                ImportPermitReceived = false,
                ImportPermitExpiry = null
            }
        ];

        var result = await controller.GetImportPermits(Guid.NewGuid(), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var permits = Assert.IsAssignableFrom<IReadOnlyList<ImportPermitResponse>>(ok.Value);
        Assert.Single(permits);
        Assert.Equal(participantSchemeId, permits[0].ParticipantSchemeId);
        Assert.True(permits[0].ImportPermitRequired);
    }

    [Fact]
    public async Task UpdateImportPermit_Valid_ReturnsNoContent()
    {
        var (controller, repository) = CreateController();
        var participantSchemeId = Guid.NewGuid();

        var result = await controller.UpdateImportPermit(
            participantSchemeId,
            new UpdateImportPermitRequest(true, new DateTime(2027, 1, 1)),
            CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.Single(repository.UpdateCalls);
    }

    [Fact]
    public async Task UpdateImportPermit_ReceivedWithoutExpiry_ReturnsValidationProblem()
    {
        var (controller, repository) = CreateController();

        var result = await controller.UpdateImportPermit(
            Guid.NewGuid(),
            new UpdateImportPermitRequest(true, null),
            CancellationToken.None);

        Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Empty(repository.UpdateCalls);
    }
}
