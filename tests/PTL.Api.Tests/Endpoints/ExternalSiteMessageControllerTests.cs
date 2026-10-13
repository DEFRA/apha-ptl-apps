using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using PTL.Api.Controllers;
using PTL.Api.Tests.ExternalSiteMessage;
using PTL.Contracts.ExternalSiteMessage;
using PTL.Core.ExternalSiteMessage;

namespace PTL.Api.Tests.Endpoints;

public class ExternalSiteMessageControllerTests
{
    private static ExternalSiteMessageController CreateController(FakeExternalSiteMessageRepository repository)
    {
        var controller = new ExternalSiteMessageController(new ExternalSiteMessageService(repository));

        var services = new ServiceCollection().AddMvc().Services.BuildServiceProvider();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { RequestServices = services }
        };

        return controller;
    }

    [Fact]
    public async Task GetExternalSiteMessage_ReturnsMappedResponse()
    {
        var repository = new FakeExternalSiteMessageRepository
        {
            Message = new PTL.Core.ExternalSiteMessage.ExternalSiteMessage { Message = "Body", ImportantMessage = "Notice", SupportEmailAddress = "vetqas@apha.gov.uk" }
        };
        var controller = CreateController(repository);

        var result = await controller.GetExternalSiteMessage(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ExternalSiteMessageResponse>(ok.Value);
        Assert.Equal("Body", response.Message);
    }

    [Fact]
    public async Task UpdateExternalSiteMessage_Valid_ReturnsOk()
    {
        var controller = CreateController(new FakeExternalSiteMessageRepository());

        var result = await controller.UpdateExternalSiteMessage(
            new ExternalSiteMessageSaveRequest("Body", "Notice", "vetqas@apha.gov.uk"), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task UpdateExternalSiteMessage_ImportantMessageTooLong_ReturnsBadRequest()
    {
        var controller = CreateController(new FakeExternalSiteMessageRepository());

        var result = await controller.UpdateExternalSiteMessage(
            new ExternalSiteMessageSaveRequest("Body", new string('a', 501), "vetqas@apha.gov.uk"), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task UpdateExternalSiteMessage_InvalidEmail_ReturnsBadRequest()
    {
        var controller = CreateController(new FakeExternalSiteMessageRepository());

        var result = await controller.UpdateExternalSiteMessage(
            new ExternalSiteMessageSaveRequest("Body", "Notice", "not-an-email"), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }
}
