using Microsoft.AspNetCore.Mvc;
using PTL.Api.Controllers;
using PTL.Api.Tests.User;
using PTL.Contracts.User;
using PTL.Core.User;

namespace PTL.Api.Tests.Endpoints;

public class RoleControllerTests
{
    [Fact]
    public async Task GetRoles_ReturnsMappedRoles()
    {
        var roleId = Guid.NewGuid();
        var userRoleService = new FakeUserRoleService { Roles = [new Role { RoleId = roleId, Name = "Admin" }] };
        var controller = new RoleController(userRoleService);

        var result = await controller.GetRoles(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var roles = Assert.IsAssignableFrom<IReadOnlyList<RoleResponse>>(ok.Value);
        Assert.Equal("Admin", Assert.Single(roles).Name);
    }
}
