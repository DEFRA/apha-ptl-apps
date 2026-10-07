using PTL.Core.InternalUser;
using CoreInternalUser = PTL.Core.InternalUser.InternalUser;

namespace PTL.Api.Tests.InternalUser;

public class InternalUserServiceTests
{
    private static InternalUserService CreateService(FakeInternalUserRepository? repository = null) =>
        new(repository ?? new FakeInternalUserRepository());

    [Fact]
    public async Task ResolveAsync_NoMatchBySsoIdIntOrUsername_ReturnsDenied()
    {
        var service = CreateService();

        var result = await service.ResolveAsync(Guid.NewGuid(), "DEFRA\\unknown");

        Assert.False(result.IsPermitted);
        Assert.Null(result.User);
    }

    [Fact]
    public async Task ResolveAsync_MatchesBySsoIdInt_ReturnsPermitted()
    {
        var ssoIdInt = Guid.NewGuid();
        var repository = new FakeInternalUserRepository();
        repository.Seed(new CoreInternalUser { UserId = Guid.NewGuid(), SsoIdInt = ssoIdInt, Username = "DEFRA\\auser", Roles = ["Admin"] });
        var service = CreateService(repository);

        var result = await service.ResolveAsync(ssoIdInt, "DEFRA\\auser");

        Assert.True(result.IsPermitted);
        Assert.Equal(["Admin"], result.User!.Roles);
        Assert.Empty(repository.UpdateCalls);
    }

    [Fact]
    public async Task ResolveAsync_NoSsoIdIntMatch_FallsBackToUsernameAndBackfillsSsoIdInt()
    {
        var userId = Guid.NewGuid();
        var repository = new FakeInternalUserRepository();
        repository.Seed(new CoreInternalUser { UserId = userId, Username = "DEFRA\\auser", Roles = [] });
        var service = CreateService(repository);
        var ssoIdInt = Guid.NewGuid();

        var result = await service.ResolveAsync(ssoIdInt, "DEFRA\\auser");

        Assert.True(result.IsPermitted);
        Assert.Equal(ssoIdInt, result.User!.SsoIdInt);
        Assert.Contains((userId, ssoIdInt), repository.UpdateCalls);
    }

    [Theory]
    [InlineData("Internal User")]
    [InlineData("Scheduling")]
    public async Task ResolveAsync_HoldsInternalUserOrSchedulingRole_AddsSyntheticDistributionsRole(string triggeringRole)
    {
        var ssoIdInt = Guid.NewGuid();
        var repository = new FakeInternalUserRepository();
        repository.Seed(new CoreInternalUser { UserId = Guid.NewGuid(), SsoIdInt = ssoIdInt, Roles = [triggeringRole] });
        var service = CreateService(repository);

        var result = await service.ResolveAsync(ssoIdInt, "DEFRA\\auser");

        Assert.Contains("Distributions", result.User!.Roles);
    }

    [Fact]
    public async Task ResolveAsync_NoInternalUserOrSchedulingRole_DoesNotAddDistributionsRole()
    {
        var ssoIdInt = Guid.NewGuid();
        var repository = new FakeInternalUserRepository();
        repository.Seed(new CoreInternalUser { UserId = Guid.NewGuid(), SsoIdInt = ssoIdInt, Roles = ["Admin"] });
        var service = CreateService(repository);

        var result = await service.ResolveAsync(ssoIdInt, "DEFRA\\auser");

        Assert.DoesNotContain("Distributions", result.User!.Roles);
    }

    [Fact]
    public async Task ResolveAsync_AlreadyHasDistributionsRole_DoesNotDuplicateIt()
    {
        var ssoIdInt = Guid.NewGuid();
        var repository = new FakeInternalUserRepository();
        repository.Seed(new CoreInternalUser { UserId = Guid.NewGuid(), SsoIdInt = ssoIdInt, Roles = ["Internal User", "Distributions"] });
        var service = CreateService(repository);

        var result = await service.ResolveAsync(ssoIdInt, "DEFRA\\auser");

        Assert.Equal(1, result.User!.Roles.Count(r => r == "Distributions"));
    }
}
