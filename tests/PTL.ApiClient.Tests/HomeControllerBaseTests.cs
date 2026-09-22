using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace PTL.ApiClient.Tests;

public class HomeControllerBaseTests
{
    [Fact]
    public void Index_ReturnsView()
    {
        var controller = new TestHomeController(new FakeApiClient());

        var result = controller.Index();

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public void Privacy_ReturnsView()
    {
        var controller = new TestHomeController(new FakeApiClient());

        var result = controller.Privacy();

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public async Task ApiStatus_ReturnsJsonFromApiClient()
    {
        var expected = new ApiHealthResponse("Healthy", 42, DateTime.UtcNow);
        var controller = new TestHomeController(new FakeApiClient(expected));

        var result = await controller.ApiStatus(CancellationToken.None);

        var jsonResult = Assert.IsType<JsonResult>(result);
        Assert.Equal(expected, jsonResult.Value);
    }

    [Fact]
    public void Error_ReturnsViewWithRequestId()
    {
        var controller = new TestHomeController(new FakeApiClient())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = controller.Error();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ErrorViewModel>(viewResult.Model);
        Assert.True(model.ShowRequestId);
    }

    private sealed class TestHomeController(IApiClient apiClient) : HomeControllerBase(apiClient);

    private sealed class FakeApiClient(ApiHealthResponse? response = null) : IApiClient
    {
        public Task<ApiHealthResponse?> GetHealthAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(response);
    }
}
