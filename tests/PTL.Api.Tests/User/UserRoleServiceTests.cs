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
        var users = new FakeUserRepository { Users = [new PTL.Core.User.User { UserId = userId, Username = "m100001" }] };
        var userRoles = new FakeUserRoleRepository
        {
            AssignmentsByUserId = new() { [userId] = [new UserRoleAssignment { UserRoleId = existingAssignmentId, UserId = userId, RoleId = AdminRoleId }] }
        };
        var service = CreateService(users: users, userRoles: userRoles);

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
        var users = new FakeUserRepository { Users = [new PTL.Core.User.User { UserId = userId, Username = "m100001" }] };
        var userRoles = new FakeUserRoleRepository
        {
            AssignmentsByUserId = new() { [userId] = [new UserRoleAssignment { UserRoleId = assignmentId, UserId = userId, RoleId = AdminRoleId }] }
        };
        var service = CreateService(users: users, userRoles: userRoles);

        var result = await service.SetUserRolesAsync(userId, [AdminRoleId], actingUserId: null);

        Assert.True(result.Success);
        Assert.Empty(userRoles.Added);
        Assert.Empty(userRoles.Removed);
    }

    [Fact]
    public async Task SetUserRolesAsync_RemovingOwnAdminRole_IsBlocked()
    {
        var userId = Guid.NewGuid();
        var users = new FakeUserRepository { Users = [new PTL.Core.User.User { UserId = userId, Username = "m100001" }] };
        var userRoles = new FakeUserRoleRepository
        {
            AssignmentsByUserId = new() { [userId] = [new UserRoleAssignment { UserRoleId = Guid.NewGuid(), UserId = userId, RoleId = AdminRoleId }] }
        };
        var service = CreateService(users: users, userRoles: userRoles);

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
        var users = new FakeUserRepository { Users = [new PTL.Core.User.User { UserId = targetUserId, Username = "m100001" }] };
        var userRoles = new FakeUserRoleRepository
        {
            AssignmentsByUserId = new() { [targetUserId] = [new UserRoleAssignment { UserRoleId = assignmentId, UserId = targetUserId, RoleId = AdminRoleId }] }
        };
        var service = CreateService(users: users, userRoles: userRoles);

        var result = await service.SetUserRolesAsync(targetUserId, [], actingUserId: currentUserId);

        Assert.True(result.Success);
        Assert.Contains(assignmentId, userRoles.Removed);
    }

    // System Administration business rule: a directory user with no PT-LIMS account must have
    // role assignment blocked until an account is created.
    [Fact]
    public async Task SetUserRolesAsync_UserHasNoPtlimsAccount_IsBlocked()
    {
        var userId = Guid.NewGuid();
        var userRoles = new FakeUserRoleRepository();
        var service = CreateService(users: new FakeUserRepository(), userRoles: userRoles);

        var result = await service.SetUserRolesAsync(userId, [AdminRoleId], actingUserId: null);

        Assert.False(result.Success);
        Assert.Equal("This user does not have a PT-LIMS account yet. Create the account before assigning roles.", result.Message);
        Assert.Empty(userRoles.Added);
    }

    // System Administration business rule: if the audit trail write fails, the whole change is
    // rolled back - no unaudited role change is ever persisted.
    [Fact]
    public async Task SetUserRolesAsync_AuditWriteFails_RollsBackAndDoesNotPersistRoleChanges()
    {
        var userId = Guid.NewGuid();
        var users = new FakeUserRepository { Users = [new PTL.Core.User.User { UserId = userId, Username = "m100001" }] };
        var userRoles = new FakeUserRoleRepository { ThrowOnAudit = true };
        var service = CreateService(users: users, userRoles: userRoles);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SetUserRolesAsync(userId, [SchemeAdminRoleId], actingUserId: null));

        Assert.Empty(userRoles.Added);
        Assert.Empty(userRoles.Removed);
    }

    [Fact]
    public async Task RemoveUserAsync_RevokesAllRolesAndDeletesTheUser_AuditedInOneCall()
    {
        var userId = Guid.NewGuid();
        var actingUserId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        var users = new FakeUserRepository { Users = [new PTL.Core.User.User { UserId = userId, Username = "m100001", FriendlyName = "Jane Smith" }] };
        var userRoles = new FakeUserRoleRepository
        {
            AssignmentsByUserId = new() { [userId] = [new UserRoleAssignment { UserRoleId = assignmentId, UserId = userId, RoleId = AdminRoleId }] }
        };
        var service = CreateService(users: users, userRoles: userRoles);

        var result = await service.RemoveUserAsync(userId, actingUserId);

        Assert.True(result.Success);
        Assert.Contains(assignmentId, users.RemovedUserRoleIds);
        Assert.Contains(userId, users.Deleted);
        Assert.Equal("RemoveUser", users.LastAuditEntry?.Action);
        Assert.Equal(actingUserId, users.LastAuditEntry?.ActingUserId);
        Assert.Equal(userId.ToString(), users.LastAuditEntry?.EntityId);
    }

    [Fact]
    public async Task RemoveUserAsync_AuditWriteFails_UserIsNotRemoved()
    {
        var userId = Guid.NewGuid();
        var users = new FakeUserRepository
        {
            Users = [new PTL.Core.User.User { UserId = userId, Username = "m100001", FriendlyName = "Jane Smith" }],
            ThrowOnAudit = true
        };
        var service = CreateService(users: users);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RemoveUserAsync(userId, actingUserId: Guid.NewGuid()));

        Assert.Empty(users.Deleted);
        Assert.Single(users.Users);
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

    [Fact]
    public async Task SetUserRolesBatchAsync_SavesEveryRowInOneRepositoryCall()
    {
        var userOneId = Guid.NewGuid();
        var userTwoId = Guid.NewGuid();
        var users = new FakeUserRepository
        {
            Users =
            [
                new PTL.Core.User.User { UserId = userOneId, Username = "m100001" },
                new PTL.Core.User.User { UserId = userTwoId, Username = "m100002" }
            ]
        };
        var userRoles = new FakeUserRoleRepository();
        var service = CreateService(users: users, userRoles: userRoles);

        var results = await service.SetUserRolesBatchAsync(
            [(userOneId, [AdminRoleId]), (userTwoId, [SchemeAdminRoleId])],
            actingUserId: null);

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.True(r.Success));
        Assert.Contains(userRoles.Added, a => a.UserId == userOneId && a.RoleId == AdminRoleId);
        Assert.Contains(userRoles.Added, a => a.UserId == userTwoId && a.RoleId == SchemeAdminRoleId);
    }

    [Fact]
    public async Task SetUserRolesBatchAsync_RemovingOwnAdminRole_IsBlockedForThatRowOnlyAndOtherRowsStillSave()
    {
        var actingUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        var users = new FakeUserRepository
        {
            Users =
            [
                new PTL.Core.User.User { UserId = actingUserId, Username = "m100001" },
                new PTL.Core.User.User { UserId = otherUserId, Username = "m100002" }
            ]
        };
        var userRoles = new FakeUserRoleRepository
        {
            AssignmentsByUserId = new() { [actingUserId] = [new UserRoleAssignment { UserRoleId = assignmentId, UserId = actingUserId, RoleId = AdminRoleId }] }
        };
        var service = CreateService(users: users, userRoles: userRoles);

        var results = await service.SetUserRolesBatchAsync(
            [(actingUserId, []), (otherUserId, [SchemeAdminRoleId])],
            actingUserId);

        var blockedResult = Assert.Single(results, r => r.UserId == actingUserId);
        Assert.False(blockedResult.Success);
        Assert.Equal("You cannot remove your own Admin access.", blockedResult.Message);
        var allowedResult = Assert.Single(results, r => r.UserId == otherUserId);
        Assert.True(allowedResult.Success);
        Assert.Contains(userRoles.Added, a => a.UserId == otherUserId && a.RoleId == SchemeAdminRoleId);
        Assert.DoesNotContain(assignmentId, userRoles.Removed);
    }

    [Fact]
    public async Task SetUserRolesBatchAsync_UserHasNoPtlimsAccount_IsBlockedForThatRowOnly()
    {
        var existingUserId = Guid.NewGuid();
        var missingUserId = Guid.NewGuid();
        var users = new FakeUserRepository { Users = [new PTL.Core.User.User { UserId = existingUserId, Username = "m100001" }] };
        var userRoles = new FakeUserRoleRepository();
        var service = CreateService(users: users, userRoles: userRoles);

        var results = await service.SetUserRolesBatchAsync(
            [(existingUserId, [AdminRoleId]), (missingUserId, [AdminRoleId])],
            actingUserId: null);

        var blockedResult = Assert.Single(results, r => r.UserId == missingUserId);
        Assert.False(blockedResult.Success);
        Assert.Equal("This user does not have a PT-LIMS account yet. Create the account before assigning roles.", blockedResult.Message);
        Assert.Contains(userRoles.Added, a => a.UserId == existingUserId && a.RoleId == AdminRoleId);
    }

    [Fact]
    public async Task SetUserRolesBatchAsync_NoChangesAcrossAnyRow_DoesNotCallTheRepository()
    {
        var userId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        var users = new FakeUserRepository { Users = [new PTL.Core.User.User { UserId = userId, Username = "m100001" }] };
        var userRoles = new FakeUserRoleRepository
        {
            AssignmentsByUserId = new() { [userId] = [new UserRoleAssignment { UserRoleId = assignmentId, UserId = userId, RoleId = AdminRoleId }] }
        };
        var service = CreateService(users: users, userRoles: userRoles);

        var results = await service.SetUserRolesBatchAsync([(userId, [AdminRoleId])], actingUserId: null);

        Assert.True(Assert.Single(results).Success);
        Assert.Empty(userRoles.Added);
        Assert.Empty(userRoles.Removed);
        Assert.Empty(userRoles.BatchAuditEntries);
    }
}

