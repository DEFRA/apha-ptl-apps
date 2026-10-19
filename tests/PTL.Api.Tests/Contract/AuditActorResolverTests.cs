using PTL.Api.Tests.Audit;
using PTL.Api.Tests.User;
using PTL.Core.Audit;

namespace PTL.Api.Tests.Contract;

public class AuditActorResolverTests
{
    private static PTL.Core.User.User UserNamed(Guid id, string username) => new() { UserId = id, Username = username };

    [Fact]
    public async Task GetAuditNameAsync_KnownUser_ReturnsTheUsername()
    {
        var id = Guid.NewGuid();
        var resolver = new AuditActorResolver(new FakeActingUserAccessor { ActingUserId = id }, new FakeUserRepository { Users = [UserNamed(id, "jsmith")] });

        Assert.Equal("jsmith", await resolver.GetAuditNameAsync());
    }

    [Fact]
    public async Task GetAuditNameAsync_LongUsername_IsTruncatedToTheAuditColumnWidth()
    {
        var id = Guid.NewGuid();
        var resolver = new AuditActorResolver(new FakeActingUserAccessor { ActingUserId = id }, new FakeUserRepository { Users = [UserNamed(id, new string('x', 80))] });

        Assert.Equal(50, (await resolver.GetAuditNameAsync()).Length);
    }

    [Fact]
    public async Task GetAuditNameAsync_UnknownUser_ReturnsGuest()
    {
        var resolver = new AuditActorResolver(new FakeActingUserAccessor { ActingUserId = Guid.NewGuid() }, new FakeUserRepository());

        Assert.Equal("Guest", await resolver.GetAuditNameAsync());
    }

    [Fact]
    public async Task GetAuditNameAsync_NobodySignedIn_ReturnsGuest()
    {
        var resolver = new AuditActorResolver(new FakeActingUserAccessor(), new FakeUserRepository());

        Assert.Equal("Guest", await resolver.GetAuditNameAsync());
    }
}
