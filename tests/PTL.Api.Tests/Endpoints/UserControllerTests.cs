using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using PTL.Api.Controllers;
using PTL.Api.Tests.User;
using PTL.Contracts.User;
using PTL.Core.User;

namespace PTL.Api.Tests.Endpoints;

public class UserControllerTests
{
    private static UserController CreateController(FakeUserRepository repository, FakeStaffDirectoryService? directory = null, FakeUserRoleService? userRoleService = null)
    {
        var controller = new UserController(new UserService(repository, directory ?? new FakeStaffDirectoryService()), userRoleService ?? new FakeUserRoleService());

        var services = new ServiceCollection().AddMvc().Services.BuildServiceProvider();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { RequestServices = services }
        };

        return controller;
    }

    [Fact]
    public async Task SearchDirectory_BlankTerm_ReturnsEmptyWithoutCallingDirectory()
    {
        var directory = new FakeStaffDirectoryService { Results = [new StaffDirectoryUser { Username = "m1" }] };
        var controller = CreateController(new FakeUserRepository(), directory);

        var result = await controller.SearchDirectory(string.Empty, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<StaffDirectoryUserResponse>>(ok.Value));
        Assert.Null(directory.LastSearchTerm);
    }

    [Fact]
    public async Task SearchDirectory_ReturnsMappedResults()
    {
        var directory = new FakeStaffDirectoryService
        {
            Results = [new StaffDirectoryUser { Username = "m100001", Email = "jane@apha.gov.uk", FriendlyName = "Jane Smith", FirstName = "Jane", LastName = "Smith" }]
        };
        var controller = CreateController(new FakeUserRepository(), directory);

        var result = await controller.SearchDirectory("jane", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var results = Assert.IsAssignableFrom<IReadOnlyList<StaffDirectoryUserResponse>>(ok.Value);
        Assert.Equal("m100001", Assert.Single(results).Username);
    }

    [Fact]
    public async Task CreateUser_Valid_ReturnsOk()
    {
        var controller = CreateController(new FakeUserRepository());

        var result = await controller.CreateUser(
            new CreateUserRequest("m100001", "jane@apha.gov.uk", "Jane Smith", "Jane", "Smith", "Science"),
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task CreateUser_NoEmail_ReturnsBadRequest()
    {
        var controller = CreateController(new FakeUserRepository());

        var result = await controller.CreateUser(
            new CreateUserRequest("m100001", string.Empty, "Jane Smith", "Jane", "Smith", "Science"),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task CreateUser_DuplicateUsername_ReturnsBadRequest()
    {
        var repository = new FakeUserRepository { Users = [new PTL.Core.User.User { Username = "m100001" }] };
        var controller = CreateController(repository);

        var result = await controller.CreateUser(
            new CreateUserRequest("m100001", "jane@apha.gov.uk", "Jane Smith", "Jane", "Smith", "Science"),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetUsers_ReturnsMappedUsers()
    {
        var repository = new FakeUserRepository { Users = [new PTL.Core.User.User { Username = "m100001", FriendlyName = "Jane Smith" }] };
        var controller = CreateController(repository);

        var result = await controller.GetUsers(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var users = Assert.IsAssignableFrom<IReadOnlyList<UserResponse>>(ok.Value);
        Assert.Equal("m100001", Assert.Single(users).Username);
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
        var controller = CreateController(new FakeUserRepository(), userRoleService: userRoleService);

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
        var controller = CreateController(new FakeUserRepository(), userRoleService: userRoleService);

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
        var controller = CreateController(new FakeUserRepository(), userRoleService: userRoleService);

        var result = await controller.SetUserRoles(userId, new SetUserRolesRequest([Guid.NewGuid()]), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<SetUserRolesResponse>(ok.Value);
        Assert.True(response.Success);
        Assert.Single(userRoleService.SetCalls);
    }
}
