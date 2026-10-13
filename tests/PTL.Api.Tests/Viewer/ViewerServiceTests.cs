using PTL.Api.Tests.ExternalUser;
using PTL.Api.Tests.Participant;
using PTL.Core.Viewer;

namespace PTL.Api.Tests.Viewer;

public class ViewerServiceTests
{
    private static ViewerService CreateService(FakeViewerRepository? repository = null, FakeExternalLoginService? loginService = null) =>
        new(repository ?? new FakeViewerRepository(), loginService ?? new FakeExternalLoginService());

    [Fact]
    public async Task CreateAsync_ValidInput_PersistsAndReturnsViewer()
    {
        var repository = new FakeViewerRepository();
        var service = CreateService(repository);

        var created = await service.CreateAsync("Jane Smith", "jane@example.com");

        Assert.Equal("Jane Smith", created.Name);
        Assert.Equal(Guid.Empty, created.SsoId);
        Assert.Single(repository.Viewers);
    }

    [Fact]
    public async Task CreateAsync_MissingName_ThrowsValidationException()
    {
        var service = CreateService();

        var ex = await Assert.ThrowsAsync<ViewerValidationException>(
            () => service.CreateAsync(string.Empty, "jane@example.com"));

        Assert.Contains(ex.Errors, e => e.Message == ViewerValidator.NameRequiredMessage);
    }

    [Fact]
    public async Task CreateAsync_MissingEmail_ThrowsValidationException()
    {
        var service = CreateService();

        var ex = await Assert.ThrowsAsync<ViewerValidationException>(
            () => service.CreateAsync("Jane Smith", string.Empty));

        Assert.Contains(ex.Errors, e => e.Message == ViewerValidator.EmailRequiredMessage);
    }

    [Fact]
    public async Task CreateAsync_InvalidEmailFormat_ThrowsValidationException()
    {
        var service = CreateService();

        var ex = await Assert.ThrowsAsync<ViewerValidationException>(
            () => service.CreateAsync("Jane Smith", "not-an-email"));

        Assert.Contains(ex.Errors, e => e.Message == ViewerValidator.EmailInvalidMessage);
    }

    [Fact]
    public async Task UpdateAsync_ExistingViewer_PreservesSsoId()
    {
        var id = Guid.NewGuid();
        var repository = new FakeViewerRepository();
        repository.Viewers.Add(new ViewerEntity { ViewerId = id, Name = "Old Name", SsoId = Guid.NewGuid() });
        var service = CreateService(repository);

        var updated = await service.UpdateAsync(id, "New Name", "new@example.com");

        Assert.NotNull(updated);
        Assert.Equal("New Name", updated.Name);
        Assert.NotEqual(Guid.Empty, updated.SsoId);
    }

    [Fact]
    public async Task UpdateAsync_UnknownViewer_ReturnsNull()
    {
        var service = CreateService();

        var updated = await service.UpdateAsync(Guid.NewGuid(), "New Name", "new@example.com");

        Assert.Null(updated);
    }

    [Fact]
    public async Task DeleteAsync_ExistingViewer_ReturnsSuccess()
    {
        var id = Guid.NewGuid();
        var repository = new FakeViewerRepository();
        repository.Viewers.Add(new ViewerEntity { ViewerId = id });
        var service = CreateService(repository);

        var (success, message) = await service.DeleteAsync(id);

        Assert.True(success);
        Assert.Null(message);
        Assert.Empty(repository.Viewers);
    }

    [Fact]
    public async Task DeleteAsync_UnknownViewer_ReturnsFailure()
    {
        var service = CreateService();

        var (success, message) = await service.DeleteAsync(Guid.NewGuid());

        Assert.False(success);
        Assert.Equal("Viewer was not found.", message);
    }

    [Fact]
    public async Task DeleteAsync_ViewerWithAssignedSchemes_ReturnsFailureAndDoesNotDelete()
    {
        var id = Guid.NewGuid();
        var repository = new FakeViewerRepository();
        repository.Viewers.Add(new ViewerEntity
        {
            ViewerId = id,
            Schemes = [new ViewerSchemeEntity { ViewerId = id, Identifier = "S1", Name = "Scheme 1" }]
        });
        var service = CreateService(repository);

        var (success, message) = await service.DeleteAsync(id);

        Assert.False(success);
        Assert.Equal("This viewer is assigned to one or more schemes or participants. Remove those assignments before removing the viewer.", message);
        Assert.Single(repository.Viewers);
    }

    [Fact]
    public async Task DeleteAsync_ViewerWithAssignedParticipants_ReturnsFailureAndDoesNotDelete()
    {
        var id = Guid.NewGuid();
        var repository = new FakeViewerRepository();
        repository.Viewers.Add(new ViewerEntity
        {
            ViewerId = id,
            Participants = [new ViewerParticipantLinkEntity { ViewerId = id, LabCode = "L1", LabName = "Lab 1" }]
        });
        var service = CreateService(repository);

        var (success, _) = await service.DeleteAsync(id);

        Assert.False(success);
        Assert.Single(repository.Viewers);
    }

    [Fact]
    public async Task GenerateLoginAsync_Success_SetsSsoId()
    {
        var id = Guid.NewGuid();
        var repository = new FakeViewerRepository();
        repository.Viewers.Add(new ViewerEntity { ViewerId = id, Name = "Jane Smith", Email = "jane@example.com", SsoId = Guid.Empty });
        var loginService = new FakeExternalLoginService();
        var service = CreateService(repository, loginService);

        var (success, message) = await service.GenerateLoginAsync(id);

        Assert.True(success);
        Assert.Null(message);
        Assert.Contains(id, loginService.Calls);
        Assert.NotEqual(Guid.Empty, repository.Viewers.Single().SsoId);
    }

    [Fact]
    public async Task GenerateLoginAsync_UnknownViewer_ReturnsFailure()
    {
        var service = CreateService();

        var (success, message) = await service.GenerateLoginAsync(Guid.NewGuid());

        Assert.False(success);
        Assert.Equal("Viewer was not found.", message);
    }

    [Fact]
    public async Task GenerateLoginAsync_StubReportsFailure_ReturnsFailureWithoutUpdating()
    {
        var id = Guid.NewGuid();
        var repository = new FakeViewerRepository();
        repository.Viewers.Add(new ViewerEntity { ViewerId = id });
        var loginService = new FakeExternalLoginService { Success = false };
        var service = CreateService(repository, loginService);

        var (success, message) = await service.GenerateLoginAsync(id);

        Assert.False(success);
        Assert.Equal("The login could not be generated.", message);
        Assert.Equal(Guid.Empty, repository.Viewers.Single().SsoId);
    }
}
