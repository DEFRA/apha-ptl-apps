using PTL.Api.Tests.Customer;
using PTL.Api.Tests.ExternalUser;
using PTL.Api.Tests.Participant;
using PTL.Core.ExternalUser;
using PTL.Core.TestConsultant;
using CoreTestConsultant = PTL.Core.TestConsultant.TestConsultant;

namespace PTL.Api.Tests.Audit;

public class TestConsultantAuditTests
{
    private readonly FakeTestConsultantRepository _repository = new();
    private readonly FakeActingUserAccessor _actingUser = new() { ActingUserId = Guid.NewGuid() };

    private TestConsultantService CreateService() =>
        new(_repository, _actingUser);

    private Guid SeedConsultant()
    {
        var id = Guid.NewGuid();
        _repository.Seed(new CoreTestConsultant { ExternalTestConsultantId = id, Name = "Jane Smith", Email = "jane@example.com" });
        return id;
    }

    [Fact]
    public async Task CreateAsync_PassesAuditEntryToRepository()
    {
        var created = await CreateService().CreateAsync("Jane Smith", "Science", "jane@example.com");

        var entry = Assert.IsType<PTL.Core.Audit.AuditEntry>(_repository.LastAuditEntry);
        Assert.Equal("CreateExternalTestConsultant", entry.Action);
        Assert.Equal("ExternalTestConsultant", entry.EntityType);
        Assert.Equal(created.ExternalTestConsultantId.ToString(), entry.EntityId);
        Assert.Equal(_actingUser.ActingUserId, entry.ActingUserId);
    }

    [Fact]
    public async Task UpdateAsync_PassesAuditEntryToRepository()
    {
        var id = SeedConsultant();

        await CreateService().UpdateAsync(id, "Jane Smith", "Science", "jane@example.com");

        var entry = Assert.IsType<PTL.Core.Audit.AuditEntry>(_repository.LastAuditEntry);
        Assert.Equal("UpdateExternalTestConsultant", entry.Action);
        Assert.Equal(id.ToString(), entry.EntityId);
        Assert.Equal(_actingUser.ActingUserId, entry.ActingUserId);
    }

    [Fact]
    public async Task UpdateAsync_UnknownConsultant_WritesNothing()
    {
        await CreateService().UpdateAsync(Guid.NewGuid(), "Jane Smith", "Science", "jane@example.com");

        Assert.Empty(_repository.UpdateCalls);
        Assert.Null(_repository.LastAuditEntry);
    }

    [Fact]
    public async Task SetStatusAsync_PassesAuditEntryWithStatusDetails()
    {
        var id = SeedConsultant();

        await CreateService().SetStatusAsync(id, true);

        var entry = Assert.IsType<PTL.Core.Audit.AuditEntry>(_repository.LastAuditEntry);
        Assert.Equal("SetExternalTestConsultantStatus", entry.Action);
        Assert.Equal("ExternalTestConsultant", entry.EntityType);
        Assert.Equal(id.ToString(), entry.EntityId);
        Assert.Equal("Inactive: True", entry.Details);
        Assert.Equal(_actingUser.ActingUserId, entry.ActingUserId);
    }

    [Fact]
    public async Task DeleteAsync_PassesAuditEntryToRepository()
    {
        var id = SeedConsultant();

        await CreateService().DeleteAsync(id);

        var entry = Assert.IsType<PTL.Core.Audit.AuditEntry>(_repository.LastAuditEntry);
        Assert.Equal("DeleteExternalTestConsultant", entry.Action);
        Assert.Equal("ExternalTestConsultant", entry.EntityType);
        Assert.Equal(id.ToString(), entry.EntityId);
        Assert.Equal(_actingUser.ActingUserId, entry.ActingUserId);
    }

    [Fact]
    public async Task DeleteAsync_BlockedByAllocation_WritesNothing()
    {
        var id = SeedConsultant();
        _repository.Seed(new CoreTestConsultant { ExternalTestConsultantId = id, AllocationCount = 1 });

        await CreateService().DeleteAsync(id);

        Assert.Empty(_repository.DeleteCalls);
        Assert.Null(_repository.LastAuditEntry);
    }

    // The SSO link made at external sign-in is system-initiated, not a System Administration change.
    [Fact]
    public async Task ExternalUserSignInLink_UpdatesTestConsultantWithoutAuditEntry()
    {
        var id = Guid.NewGuid();
        _repository.Seed(new CoreTestConsultant { ExternalTestConsultantId = id, Email = "jane@example.com" });
        var service = new ExternalUserService(new FakeParticipantRepository(), new FakeViewerRepository(), _repository, new FakeCustomerRepository());

        var result = await service.ResolveAsync(Guid.NewGuid(), "jane@example.com", "Jane", ["Test Consultant"]);

        Assert.Equal(id, result.TestConsultantId);
        Assert.Single(_repository.UpdateCalls);
        Assert.Null(_repository.LastAuditEntry);
    }
}
