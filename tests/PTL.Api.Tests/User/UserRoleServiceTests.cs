using PTL.Core.User;

namespace PTL.Api.Tests.User;

public class UserRoleServiceTests
{
    private static readonly Guid AdminRoleId = Guid.NewGuid();
    private static readonly Guid SchemeAdminRoleId = Guid.NewGuid();

    private static UserRoleService CreateService(
        FakeUserRepository? users = null,
        FakeRoleRepository? roles = null,
        FakeUserRoleRepository? userRoles = null) =>
        new(
            users ?? new FakeUserRepository(),
            roles ?? new FakeRoleRepository { Roles = [new Role { RoleId = AdminRoleId, Name = "Admin" }, new Role { RoleId = SchemeAdminRoleId, Name = "Scheme Admin" }] },
            userRoles ?? new FakeUserRoleRepository());

    [Fact]
    public async Task GetRolesAsync_ReturnsAllRoles()
    {
        var service = CreateService();

        var roles = await service.GetRolesAsync();

        Assert.Equal(2, roles.Count);
    }

    [Fact]
    public async Task GetUserRoleGridAsync_BuildsOneRowPerUserWithTheirRoleIds()
    {
        var userId = Guid.NewGuid();
        var users = new FakeUserRepository { Users = [new PTL.Core.User.User { UserId = userId, Username = "m100001", FriendlyName = "Jane Smith" }] };
        var userRoles = new FakeUserRoleRepository
        {
            AssignmentsByUserId = new() { [userId] = [new UserRoleAssignment { UserRoleId = Guid.NewGuid(), UserId = userId, RoleId = AdminRoleId }] }
        };
        var service = CreateService(users: users, userRoles: userRoles);

        var grid = await service.GetUserRoleGridAsync();

        var row = Assert.Single(grid);
        Assert.Equal("Jane Smith", row.FriendlyName);
        Assert.Contains(AdminRoleId, row.RoleIds);
    }

    [Fact]
    public async Task SetUserRolesAsync_AddsAndRemovesToMatchDesiredSet()
    {
        var userId = Guid.NewGuid();
        var existingAssignmentId = Guid.NewGuid();
        var userRoles = new FakeUserRoleRepository
        {
            AssignmentsByUserId = new() { [userId] = [new UserRoleAssignment { UserRoleId = existingAssignmentId, UserId = userId, RoleId = AdminRoleId }] }
        };
        var service = CreateService(userRoles: userRoles);

        var result = await service.SetUserRolesAsync(userId, [SchemeAdminRoleId], actingUserId: null);

        Assert.True(result.Success);
        Assert.Contains(userRoles.Removed, id => id == existingAssignmentId);
        Assert.Contains(userRoles.Added, a => a.UserId == userId && a.RoleId == SchemeAdminRoleId);
    }

    [Fact]
    public async Task SetUserRolesAsync_NoChange_DoesNotAddOrRemoveAnything()
    {
        var userId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        var userRoles = new FakeUserRoleRepository
        {
            AssignmentsByUserId = new() { [userId] = [new UserRoleAssignment { UserRoleId = assignmentId, UserId = userId, RoleId = AdminRoleId }] }
        };
        var service = CreateService(userRoles: userRoles);

        var result = await service.SetUserRolesAsync(userId, [AdminRoleId], actingUserId: null);

        Assert.True(result.Success);
        Assert.Empty(userRoles.Added);
        Assert.Empty(userRoles.Removed);
    }

    [Fact]
    public async Task SetUserRolesAsync_RemovingOwnAdminRole_IsBlocked()
    {
        var userId = Guid.NewGuid();
        var userRoles = new FakeUserRoleRepository
        {
            AssignmentsByUserId = new() { [userId] = [new UserRoleAssignment { UserRoleId = Guid.NewGuid(), UserId = userId, RoleId = AdminRoleId }] }
        };
        var service = CreateService(userRoles: userRoles);

        var result = await service.SetUserRolesAsync(userId, [], actingUserId: userId);

        Assert.False(result.Success);
        Assert.Equal("You cannot remove your own Admin access.", result.Message);
        Assert.Empty(userRoles.Removed);
    }

    [Fact]
    public async Task SetUserRolesAsync_RemovingSomeoneElsesAdminRole_IsAllowed()
    {
        var targetUserId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        var userRoles = new FakeUserRoleRepository
        {
            AssignmentsByUserId = new() { [targetUserId] = [new UserRoleAssignment { UserRoleId = assignmentId, UserId = targetUserId, RoleId = AdminRoleId }] }
        };
        var service = CreateService(userRoles: userRoles);

        var result = await service.SetUserRolesAsync(targetUserId, [], actingUserId: currentUserId);

        Assert.True(result.Success);
        Assert.Contains(assignmentId, userRoles.Removed);
    }

    [Fact]
    public async Task RemoveUserAsync_RevokesAllRolesAndDeletesTheUser()
    {
        var userId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        var users = new FakeUserRepository { Users = [new PTL.Core.User.User { UserId = userId, Username = "m100001", FriendlyName = "Jane Smith" }] };
        var userRoles = new FakeUserRoleRepository
        {
            AssignmentsByUserId = new() { [userId] = [new UserRoleAssignment { UserRoleId = assignmentId, UserId = userId, RoleId = AdminRoleId }] }
        };
        var service = CreateService(users: users, userRoles: userRoles);

        var result = await service.RemoveUserAsync(userId, actingUserId: null);

        Assert.True(result.Success);
        Assert.Contains(assignmentId, userRoles.Removed);
        Assert.Contains(userId, users.Deleted);
    }

    [Fact]
    public async Task RemoveUserAsync_RemovingYourself_IsBlocked()
    {
        var userId = Guid.NewGuid();
        var users = new FakeUserRepository { Users = [new PTL.Core.User.User { UserId = userId, Username = "m100001", FriendlyName = "Jane Smith" }] };
        var service = CreateService(users: users);

        var result = await service.RemoveUserAsync(userId, actingUserId: userId);

        Assert.False(result.Success);
        Assert.Equal("You cannot remove your own account.", result.Message);
        Assert.Empty(users.Deleted);
    }
}
