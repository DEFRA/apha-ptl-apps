using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using PTL.Api.Controllers;
using PTL.Api.Tests.ExternalUser;
using PTL.Api.Tests.Participant;
using PTL.Contracts.Viewer;
using PTL.Core.Viewer;

namespace PTL.Api.Tests.Endpoints;

public class ViewerControllerTests
{
    private static ViewerController CreateController(FakeViewerRepository? repository = null, FakeExternalLoginService? loginService = null)
    {
        var controller = new ViewerController(new ViewerService(repository ?? new FakeViewerRepository(), loginService ?? new FakeExternalLoginService()));

        var services = new ServiceCollection().AddMvc().Services.BuildServiceProvider();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { RequestServices = services }
        };

        return controller;
    }

    [Fact]
    public async Task GetAll_ReturnsMappedViewers()
    {
        var repository = new FakeViewerRepository();
        repository.Viewers.Add(new ViewerEntity { ViewerId = Guid.NewGuid(), Name = "Jane Smith", Email = "jane@example.com", SsoId = Guid.NewGuid() });
        var controller = CreateController(repository);

        var result = await controller.GetAll(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var viewers = Assert.IsAssignableFrom<IReadOnlyList<ViewerResponse>>(ok.Value);
        var viewer = Assert.Single(viewers);
        Assert.Equal("Jane Smith", viewer.Name);
        Assert.True(viewer.HasLogin);
    }

    [Fact]
    public async Task Create_Valid_ReturnsOk()
    {
        var controller = CreateController();

        var result = await controller.Create(new ViewerSaveRequest("Jane Smith", "jane@example.com"), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var viewer = Assert.IsType<ViewerResponse>(ok.Value);
        Assert.Equal("Jane Smith", viewer.Name);
    }

    [Fact]
    public async Task Create_MissingEmail_ReturnsBadRequest()
    {
        var controller = CreateController();

        var result = await controller.Create(new ViewerSaveRequest("Jane Smith", string.Empty), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.False(controller.ModelState.IsValid);
    }

    [Fact]
    public async Task Update_UnknownViewer_ReturnsNotFound()
    {
        var controller = CreateController();

        var result = await controller.Update(Guid.NewGuid(), new ViewerSaveRequest("Jane Smith", "jane@example.com"), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Delete_ExistingViewer_ReturnsSuccess()
    {
        var id = Guid.NewGuid();
        var repository = new FakeViewerRepository();
        repository.Viewers.Add(new ViewerEntity { ViewerId = id });
        var controller = CreateController(repository);

        var result = await controller.Delete(id, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ViewerDeleteResponse>(ok.Value);
        Assert.True(response.Success);
    }

    [Fact]
    public async Task Delete_UnknownViewer_ReturnsOkWithFailure()
    {
        var controller = CreateController();

        var result = await controller.Delete(Guid.NewGuid(), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ViewerDeleteResponse>(ok.Value);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task GenerateLogin_Success_ReturnsOkWithSuccess()
    {
        var id = Guid.NewGuid();
        var repository = new FakeViewerRepository();
        repository.Viewers.Add(new ViewerEntity { ViewerId = id, Name = "Jane Smith", Email = "jane@example.com" });
        var controller = CreateController(repository);

        var result = await controller.GenerateLogin(id, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<PTL.Contracts.TestConsultant.GenerateLoginResponse>(ok.Value);
        Assert.True(response.Success);
    }
}
