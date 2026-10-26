using Microsoft.AspNetCore.Mvc;
using PTL.ExternalWeb.Features.Comments;

namespace PTL.ExternalWeb.Tests.Features.Comments;

public class CommentsControllerTests
{
    [Fact]
    public void Index_ReturnsComingSoonViewForEnterComments()
    {
        var controller = new CommentsController();

        var result = controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("_ComingSoonBody", viewResult.ViewName);
        Assert.Equal("Enter comments", viewResult.Model);
    }
}
