using PTL.Api.Tests.ExternalUser;
using PTL.Core.TestConsultant;

namespace PTL.Api.Tests.TestConsultant;

public class TestConsultantServiceTests
{
    private static TestConsultantService CreateService(FakeTestConsultantRepository? repository = null, FakeExternalLoginService? loginService = null) =>
        new(repository ?? new FakeTestConsultantRepository(), loginService ?? new FakeExternalLoginService());

    [Fact]
    public async Task CreateAsync_ValidInput_PersistsAndReturnsConsultant()
    {
        var repository = new FakeTestConsultantRepository();
        var service = CreateService(repository);

        var created = await service.CreateAsync("Jane Smith", "Science", "jane@example.com");

        Assert.Equal("Jane Smith", created.Name);
        Assert.False(created.IsInactive);
        Assert.Equal(Guid.Empty, created.SsoId);
    }

    [Fact]
    public async Task CreateAsync_MissingName_ThrowsValidationException()
    {
        var service = CreateService();

        var ex = await Assert.ThrowsAsync<ExternalTestConsultantValidationException>(
            () => service.CreateAsync(string.Empty, "Science", "jane@example.com"));

        Assert.Contains(ex.Errors, e => e.Message == ExternalTestConsultantValidator.NameRequiredMessage);
    }

    [Fact]
    public async Task CreateAsync_MissingEmail_ThrowsValidationException()
    {
        var service = CreateService();

        var ex = await Assert.ThrowsAsync<ExternalTestConsultantValidationException>(
            () => service.CreateAsync("Jane Smith", "Science", string.Empty));

        Assert.Contains(ex.Errors, e => e.Message == ExternalTestConsultantValidator.EmailRequiredMessage);
    }

    [Fact]
    public async Task CreateAsync_InvalidEmailFormat_ThrowsValidationException()
    {
        var service = CreateService();

        var ex = await Assert.ThrowsAsync<ExternalTestConsultantValidationException>(
            () => service.CreateAsync("Jane Smith", "Science", "not-an-email"));

        Assert.Contains(ex.Errors, e => e.Message == ExternalTestConsultantValidator.EmailInvalidMessage);
    }

    [Fact]
    public async Task UpdateAsync_ExistingConsultant_PreservesSsoIdAndStatus()
    {
        var id = Guid.NewGuid();
        var repository = new FakeTestConsultantRepository();
        repository.Seed(new PTL.Core.TestConsultant.TestConsultant { ExternalTestConsultantId = id, Name = "Old Name", SsoId = Guid.NewGuid(), IsInactive = true, InactiveDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) });
        var service = CreateService(repository);

        var updated = await service.UpdateAsync(id, "New Name", "Science", "new@example.com");

        Assert.NotNull(updated);
        Assert.Equal("New Name", updated.Name);
        Assert.NotEqual(Guid.Empty, updated.SsoId);
        Assert.True(updated.IsInactive);
    }

    [Fact]
    public async Task UpdateAsync_UnknownConsultant_ReturnsNull()
    {
        var service = CreateService();

        var updated = await service.UpdateAsync(Guid.NewGuid(), "New Name", "Science", "new@example.com");

        Assert.Null(updated);
    }

    [Fact]
    public async Task SetStatusAsync_BecomingInactive_StampsInactiveDate()
    {
        var id = Guid.NewGuid();
        var repository = new FakeTestConsultantRepository();
        repository.Seed(new PTL.Core.TestConsultant.TestConsultant { ExternalTestConsultantId = id, IsInactive = false });
        var service = CreateService(repository);

        var updated = await service.SetStatusAsync(id, isInactive: true);

        Assert.NotNull(updated);
        Assert.True(updated.IsInactive);
        Assert.NotNull(updated.InactiveDate);
    }

    [Fact]
    public async Task SetStatusAsync_BecomingActive_ClearsInactiveDate()
    {
        var id = Guid.NewGuid();
        var repository = new FakeTestConsultantRepository();
        repository.Seed(new PTL.Core.TestConsultant.TestConsultant { ExternalTestConsultantId = id, IsInactive = true, InactiveDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) });
        var service = CreateService(repository);

        var updated = await service.SetStatusAsync(id, isInactive: false);

        Assert.NotNull(updated);
        Assert.False(updated.IsInactive);
        Assert.Null(updated.InactiveDate);
    }

    [Fact]
    public async Task GenerateLoginAsync_Success_SetsSsoId()
    {
        var id = Guid.NewGuid();
        var repository = new FakeTestConsultantRepository();
        repository.Seed(new PTL.Core.TestConsultant.TestConsultant { ExternalTestConsultantId = id, Name = "Jane Smith", Email = "jane@example.com", SsoId = Guid.Empty });
        var loginService = new FakeExternalLoginService();
        var service = CreateService(repository, loginService);

        var (success, message) = await service.GenerateLoginAsync(id);

        Assert.True(success);
        Assert.Null(message);
        Assert.Contains(id, loginService.Calls);
        Assert.NotEqual(Guid.Empty, Assert.Single(repository.UpdateCalls).SsoId);
    }

    [Fact]
    public async Task GenerateLoginAsync_UnknownConsultant_ReturnsFailure()
    {
        var service = CreateService();

        var (success, message) = await service.GenerateLoginAsync(Guid.NewGuid());

        Assert.False(success);
        Assert.Equal("Test consultant was not found.", message);
    }

    [Fact]
    public async Task GenerateLoginAsync_StubReportsFailure_ReturnsFailureWithoutUpdating()
    {
        var id = Guid.NewGuid();
        var repository = new FakeTestConsultantRepository();
        repository.Seed(new PTL.Core.TestConsultant.TestConsultant { ExternalTestConsultantId = id });
        var loginService = new FakeExternalLoginService { Success = false };
        var service = CreateService(repository, loginService);

        var (success, message) = await service.GenerateLoginAsync(id);

        Assert.False(success);
        Assert.Equal("The login could not be generated.", message);
        Assert.Empty(repository.UpdateCalls);
    }
}
