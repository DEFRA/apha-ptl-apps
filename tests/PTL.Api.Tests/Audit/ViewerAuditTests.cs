using PTL.Api.Tests.Customer;
using PTL.Api.Tests.ExternalUser;
using PTL.Api.Tests.Participant;
using PTL.Core.Audit;
using PTL.Core.ExternalUser;
using PTL.Core.Viewer;

namespace PTL.Api.Tests.Audit;

public class ViewerAuditTests
{
    private readonly FakeViewerRepository _repository = new();
    private readonly FakeActingUserAccessor _actingUser = new() { ActingUserId = Guid.NewGuid() };

    private ViewerService CreateService() =>
        new(_repository, _actingUser);

    private ViewerEntity SeedViewer()
    {
        var viewer = new ViewerEntity { ViewerId = Guid.NewGuid(), Name = "Jane Smith", Email = "jane@example.com" };
        _repository.Viewers.Add(viewer);
        return viewer;
    }

    [Fact]
    public async Task CreateAsync_PassesAuditEntryToRepository()
    {
        var created = await CreateService().CreateAsync("Jane Smith", "jane@example.com");

        var entry = Assert.IsType<AuditEntry>(_repository.LastAuditEntry);
        Assert.Equal("CreateViewer", entry.Action);
        Assert.Equal("Viewer", entry.EntityType);
        Assert.Equal(created.ViewerId.ToString(), entry.EntityId);
        Assert.Equal(_actingUser.ActingUserId, entry.ActingUserId);
    }

    [Fact]
    public async Task UpdateAsync_PassesAuditEntryToRepository()
    {
        var viewer = SeedViewer();

        await CreateService().UpdateAsync(viewer.ViewerId, "Jane Smith", "jane@example.com");

        var entry = Assert.IsType<AuditEntry>(_repository.LastAuditEntry);
        Assert.Equal("UpdateViewer", entry.Action);
        Assert.Equal(viewer.ViewerId.ToString(), entry.EntityId);
        Assert.Equal(_actingUser.ActingUserId, entry.ActingUserId);
    }

    [Fact]
    public async Task UpdateAsync_UnknownViewer_WritesNothing()
    {
        await CreateService().UpdateAsync(Guid.NewGuid(), "Jane Smith", "jane@example.com");

        Assert.Null(_repository.LastAuditEntry);
    }

    [Fact]
    public async Task DeleteAsync_PassesAuditEntryToRepository()
    {
        var viewer = SeedViewer();

        var (success, _) = await CreateService().DeleteAsync(viewer.ViewerId);

        Assert.True(success);
        var entry = Assert.IsType<AuditEntry>(_repository.LastAuditEntry);
        Assert.Equal("DeleteViewer", entry.Action);
        Assert.Equal("Viewer", entry.EntityType);
        Assert.Equal(viewer.ViewerId.ToString(), entry.EntityId);
        Assert.Equal(_actingUser.ActingUserId, entry.ActingUserId);
    }

    [Fact]
    public async Task DeleteAsync_BlockedByAssignments_WritesNothing()
    {
        var viewer = SeedViewer();
        viewer.Schemes = [new ViewerSchemeEntity { ViewerId = viewer.ViewerId, Identifier = "SFW1234", Name = "Heavy Metals" }];

        var (success, _) = await CreateService().DeleteAsync(viewer.ViewerId);

        Assert.False(success);
        Assert.Single(_repository.Viewers);
        Assert.Null(_repository.LastAuditEntry);
    }

    // The SSO link made at external sign-in is system-initiated, not a System Administration change.
    [Fact]
    public async Task ExternalUserSignInLink_UpdatesViewerWithoutAuditEntry()
    {
        var viewer = SeedViewer();
        var service = new ExternalUserService(new FakeParticipantRepository(), _repository, new FakeTestConsultantRepository(), new FakeCustomerRepository());

        var result = await service.ResolveAsync(Guid.NewGuid(), "jane@example.com", "Jane", ["Viewer"]);

        Assert.Equal(viewer.ViewerId, result.ViewerId);
        Assert.NotNull(viewer.SsoIdExt);
        Assert.Null(_repository.LastAuditEntry);
    }
}
