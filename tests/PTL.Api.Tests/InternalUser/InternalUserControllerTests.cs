using Microsoft.AspNetCore.Mvc;
using PTL.Api.Controllers;
using PTL.Contracts.InternalUser;
using PTL.Core.InternalUser;
using CoreInternalUser = PTL.Core.InternalUser.InternalUser;

namespace PTL.Api.Tests.InternalUser;

public class InternalUserControllerTests
{
    private static InternalUserController CreateController(FakeInternalUserRepository? repository = null) =>
        new(new InternalUserService(repository ?? new FakeInternalUserRepository()));

    [Fact]
    public async Task ResolveInternalUser_NoMatch_ReturnsOkWithNotPermitted()
    {
        var controller = CreateController();

        var response = await controller.ResolveInternalUser(
            new ResolveInternalUserRequest(Guid.NewGuid(), "DEFRA\\unknown"),
            CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(response.Result);
        var body = Assert.IsType<ResolveInternalUserResponse>(okResult.Value);
        Assert.False(body.IsPermitted);
        Assert.Null(body.UserId);
        Assert.Empty(body.Roles);
    }

    [Fact]
    public async Task ResolveInternalUser_Match_ReturnsOkWithFriendlyNameAsFullName()
    {
        var ssoIdInt = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var repository = new FakeInternalUserRepository();
        repository.Seed(new CoreInternalUser { UserId = userId, SsoIdInt = ssoIdInt, FriendlyName = "Alice User", Department = "QAU", Roles = ["Admin"] });
        var controller = CreateController(repository);

        var response = await controller.ResolveInternalUser(
            new ResolveInternalUserRequest(ssoIdInt, "DEFRA\\auser"),
            CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(response.Result);
        var body = Assert.IsType<ResolveInternalUserResponse>(okResult.Value);
        Assert.True(body.IsPermitted);
        Assert.Equal(userId, body.UserId);
        Assert.Equal("Alice User", body.FullName);
        Assert.Equal("QAU", body.Department);
        Assert.Equal(["Admin"], body.Roles);
    }

    [Fact]
    public async Task ResolveInternalUser_MatchWithNoFriendlyName_FallsBackToFirstLastName()
    {
        var ssoIdInt = Guid.NewGuid();
        var repository = new FakeInternalUserRepository();
        repository.Seed(new CoreInternalUser { UserId = Guid.NewGuid(), SsoIdInt = ssoIdInt, FirstName = "Alice", LastName = "User" });
        var controller = CreateController(repository);

        var response = await controller.ResolveInternalUser(
            new ResolveInternalUserRequest(ssoIdInt, "DEFRA\\auser"),
            CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(response.Result);
        var body = Assert.IsType<ResolveInternalUserResponse>(okResult.Value);
        Assert.Equal("Alice User", body.FullName);
    }
}
