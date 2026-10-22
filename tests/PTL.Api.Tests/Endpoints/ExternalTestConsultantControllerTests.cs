using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using PTL.Api.Controllers;
using PTL.Api.Tests.Audit;
using PTL.Api.Tests.ExternalUser;
using PTL.Contracts.TestConsultant;
using PTL.Core.TestConsultant;

namespace PTL.Api.Tests.Endpoints;

public class ExternalTestConsultantControllerTests
{
    private static ExternalTestConsultantController CreateController(FakeTestConsultantRepository? repository = null)
    {
        var controller = new ExternalTestConsultantController(new TestConsultantService(repository ?? new FakeTestConsultantRepository(), new FakeActingUserAccessor()));

        var services = new ServiceCollection().AddMvc().Services.BuildServiceProvider();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { RequestServices = services }
        };

        return controller;
    }

    [Fact]
    public async Task GetAll_ReturnsMappedConsultants()
    {
        var repository = new FakeTestConsultantRepository();
        repository.Seed(new PTL.Core.TestConsultant.TestConsultant { ExternalTestConsultantId = Guid.NewGuid(), Name = "Jane Smith", Email = "jane@example.com", SsoId = Guid.NewGuid() });
        var controller = CreateController(repository);

        var result = await controller.GetAll(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var consultants = Assert.IsAssignableFrom<IReadOnlyList<ExternalTestConsultantResponse>>(ok.Value);
        var consultant = Assert.Single(consultants);
        Assert.Equal("Jane Smith", consultant.Name);
    }

    [Fact]
    public async Task Create_Valid_ReturnsOk()
    {
        var controller = CreateController();

        var result = await controller.Create(new ExternalTestConsultantSaveRequest("Jane Smith", "Science", "jane@example.com"), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var consultant = Assert.IsType<ExternalTestConsultantResponse>(ok.Value);
        Assert.Equal("Jane Smith", consultant.Name);
    }

    [Fact]
    public async Task Create_MissingEmail_ReturnsValidationProblem()
    {
        var controller = CreateController();

        var result = await controller.Create(new ExternalTestConsultantSaveRequest("Jane Smith", "Science", string.Empty), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.False(controller.ModelState.IsValid);
    }

    [Fact]
    public async Task Update_UnknownConsultant_ReturnsNotFound()
    {
        var controller = CreateController();

        var result = await controller.Update(Guid.NewGuid(), new ExternalTestConsultantSaveRequest("Jane Smith", "Science", "jane@example.com"), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task SetStatus_ExistingConsultant_ReturnsUpdatedConsultant()
    {
        var id = Guid.NewGuid();
        var repository = new FakeTestConsultantRepository();
        repository.Seed(new PTL.Core.TestConsultant.TestConsultant { ExternalTestConsultantId = id });
        var controller = CreateController(repository);

        var result = await controller.SetStatus(id, new ExternalTestConsultantStatusRequest(true), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var consultant = Assert.IsType<ExternalTestConsultantResponse>(ok.Value);
        Assert.True(consultant.IsInactive);
    }

    [Fact]
    public async Task SetStatus_DeactivatingAnAllocatedConsultant_ReturnsValidationProblem()
    {
        var id = Guid.NewGuid();
        var repository = new FakeTestConsultantRepository();
        repository.Seed(new PTL.Core.TestConsultant.TestConsultant { ExternalTestConsultantId = id, AllocationCount = 1, AllocationString = "S1" });
        var controller = CreateController(repository);

        var result = await controller.SetStatus(id, new ExternalTestConsultantStatusRequest(true), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.False(controller.ModelState.IsValid);
    }

    [Fact]
    public async Task Delete_ExistingConsultant_ReturnsSuccess()
    {
        var id = Guid.NewGuid();
        var repository = new FakeTestConsultantRepository();
        repository.Seed(new PTL.Core.TestConsultant.TestConsultant { ExternalTestConsultantId = id });
        var controller = CreateController(repository);

        var result = await controller.Delete(id, CancellationToken.None);

        var response = Assert.IsType<ExternalTestConsultantDeleteResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.True(response.Success);
    }

    [Fact]
    public async Task Delete_AllocatedConsultant_ReturnsOkWithFailure()
    {
        var id = Guid.NewGuid();
        var repository = new FakeTestConsultantRepository();
        repository.Seed(new PTL.Core.TestConsultant.TestConsultant { ExternalTestConsultantId = id, AllocationCount = 1, AllocationString = "S1" });
        var controller = CreateController(repository);

        var result = await controller.Delete(id, CancellationToken.None);

        var response = Assert.IsType<ExternalTestConsultantDeleteResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.False(response.Success);
    }
}
