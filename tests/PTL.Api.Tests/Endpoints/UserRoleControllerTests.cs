using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using PTL.Api.Controllers;
using PTL.Api.Tests.User;
using PTL.Contracts.User;

namespace PTL.Api.Tests.Endpoints;

public class UserRoleControllerTests
{
    private static UserRoleController CreateController(FakeUserRoleService? userRoleService = null)
    {
        var controller = new UserRoleController(userRoleService ?? new FakeUserRoleService());

        var services = new ServiceCollection().AddMvc().Services.BuildServiceProvider();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { RequestServices = services }
        };

        return controller;
    }

    [Fact]
    public async Task GetUserRoles_ReturnsGrid()
    {
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var userRoleService = new FakeUserRoleService
        {
            Grid = [new PTL.Core.User.UserRoleRow(userId, "m100001", "Jane Smith", [roleId])]
        };
        var controller = CreateController(userRoleService);

        var result = await controller.GetUserRoles(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var rows = Assert.IsAssignableFrom<IReadOnlyList<UserRoleRowResponse>>(ok.Value);
        Assert.Contains(roleId, Assert.Single(rows).RoleIds);
    }

    [Fact]
    public async Task SetUserRoles_Blocked_ReturnsOkWithFailureMessage()
    {
        var userId = Guid.NewGuid();
        var userRoleService = new FakeUserRoleService { SetResult = new PTL.Core.User.SetUserRolesResult(false, "You cannot remove your own Admin access.") };
        var controller = CreateController(userRoleService);

        var result = await controller.SetUserRoles(userId, new SetUserRolesRequest([]), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<SetUserRolesResponse>(ok.Value);
        Assert.False(response.Success);
        Assert.Equal("You cannot remove your own Admin access.", response.Message);
    }

    [Fact]
    public async Task SetUserRoles_Valid_ReturnsOkWithSuccess()
    {
        var userId = Guid.NewGuid();
        var userRoleService = new FakeUserRoleService();
        var controller = CreateController(userRoleService);

        var result = await controller.SetUserRoles(userId, new SetUserRolesRequest([Guid.NewGuid()]), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<SetUserRolesResponse>(ok.Value);
        Assert.True(response.Success);
        Assert.Single(userRoleService.SetCalls);
    }

    [Fact]
    public async Task RemoveUser_Blocked_ReturnsOkWithFailureMessage()
    {
        var userId = Guid.NewGuid();
        var userRoleService = new FakeUserRoleService { RemoveResult = new PTL.Core.User.UserRemoveResult(false, "You cannot remove your own account.") };
        var controller = CreateController(userRoleService);

        var result = await controller.RemoveUser(userId, actingUserId: null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<UserRemoveResponse>(ok.Value);
        Assert.False(response.Success);
        Assert.Equal("You cannot remove your own account.", response.Message);
    }

    [Fact]
    public async Task RemoveUser_Valid_ReturnsOkWithSuccess()
    {
        var userId = Guid.NewGuid();
        var userRoleService = new FakeUserRoleService();
        var controller = CreateController(userRoleService);

        var result = await controller.RemoveUser(userId, actingUserId: null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<UserRemoveResponse>(ok.Value);
        Assert.True(response.Success);
        Assert.Single(userRoleService.RemoveCalls);
    }
}
