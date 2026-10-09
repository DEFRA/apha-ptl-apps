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
    private static UserController CreateController(FakeUserRepository repository, FakeStaffDirectoryService? directory = null)
    {
        var controller = new UserController(new UserService(repository, directory ?? new FakeStaffDirectoryService()));

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
    public async Task GetTestConsultants_ReturnsMappedUsers()
    {
        var repository = new FakeUserRepository
        {
            TestConsultants = [new PTL.Core.User.User { Username = "m100001", FriendlyName = "Jane Smith", Department = "Science", IsInactive = true, InactiveDate = new DateTime(2026, 10, 8, 0, 0, 0, DateTimeKind.Utc) }]
        };
        var controller = CreateController(repository);

        var result = await controller.GetTestConsultants(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var users = Assert.IsAssignableFrom<IReadOnlyList<UserResponse>>(ok.Value);
        var user = Assert.Single(users);
        Assert.Equal("Science", user.Department);
        Assert.True(user.IsInactive);
    }

    [Fact]
    public async Task UpdateTestConsultant_PersistsRequestValues()
    {
        var userId = Guid.NewGuid();
        var repository = new FakeUserRepository();
        var controller = CreateController(repository);

        var result = await controller.UpdateTestConsultant(userId, new UpdateTestConsultantRequest("Science", true, new DateTime(2026, 10, 8, 0, 0, 0, DateTimeKind.Utc)), CancellationToken.None);

        Assert.IsType<OkResult>(result);
        var update = Assert.Single(repository.DepartmentUpdates);
        Assert.Equal(userId, update.UserId);
        Assert.Equal("Science", update.Department);
        Assert.True(update.IsInactive);
    }
}
