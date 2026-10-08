using PTL.Core.User;

namespace PTL.Api.Tests.User;

public class UserServiceTests
{
    private static UserService CreateService(FakeUserRepository repository, FakeStaffDirectoryService? directory = null) =>
        new(repository, directory ?? new FakeStaffDirectoryService());

    [Fact]
    public async Task SearchStaffDirectoryAsync_FiltersOutCandidatesWithExistingAccounts()
    {
        var repository = new FakeUserRepository
        {
            Users = [new PTL.Core.User.User { Username = "m100001", Email = "jane@apha.gov.uk" }]
        };
        var directory = new FakeStaffDirectoryService
        {
            Results =
            [
                new StaffDirectoryUser { Username = "m100001", Email = "jane@apha.gov.uk", FriendlyName = "Jane Smith" },
                new StaffDirectoryUser { Username = "m100002", Email = "john@apha.gov.uk", FriendlyName = "John Doe" }
            ]
        };
        var service = CreateService(repository, directory);

        var result = await service.SearchStaffDirectoryAsync("m1");

        var match = Assert.Single(result);
        Assert.Equal("m100002", match.Username);
    }

    [Fact]
    public async Task SearchStaffDirectoryAsync_AllCandidatesAlreadyHaveAccounts_ReturnsEmpty()
    {
        var repository = new FakeUserRepository
        {
            Users = [new PTL.Core.User.User { Username = "m100001" }]
        };
        var directory = new FakeStaffDirectoryService
        {
            Results = [new StaffDirectoryUser { Username = "m100001", FriendlyName = "Jane Smith" }]
        };
        var service = CreateService(repository, directory);

        var result = await service.SearchStaffDirectoryAsync("jane");

        Assert.Empty(result);
    }

    [Fact]
    public async Task CreateUserAsync_ValidInput_PersistsAndReturnsUser()
    {
        var repository = new FakeUserRepository();
        var service = CreateService(repository);

        var created = await service.CreateUserAsync("m100001", "jane@apha.gov.uk", "Jane Smith", "Jane", "Smith", "Science");

        Assert.Equal("m100001", created.Username);
        Assert.False(created.IsInactive);
        Assert.Single(repository.Created);
    }

    [Fact]
    public async Task CreateUserAsync_NoEmail_ThrowsValidationExceptionAndDoesNotPersist()
    {
        var service = CreateService(new FakeUserRepository());

        var ex = await Assert.ThrowsAsync<UserValidationException>(
            () => service.CreateUserAsync("m100001", string.Empty, "Jane Smith", "Jane", "Smith", "Science"));

        Assert.Contains(ex.Errors, e => e.Message == UserValidator.NoEmailMessage);
    }

    [Fact]
    public async Task CreateUserAsync_DuplicateUsername_ThrowsValidationException()
    {
        var repository = new FakeUserRepository
        {
            Users = [new PTL.Core.User.User { Username = "m100001" }]
        };
        var service = CreateService(repository);

        var ex = await Assert.ThrowsAsync<UserValidationException>(
            () => service.CreateUserAsync("M100001", "jane@apha.gov.uk", "Jane Smith", "Jane", "Smith", "Science"));

        Assert.Contains(ex.Errors, e => e.Message == UserValidator.DuplicateMessage);
        Assert.Empty(repository.Created);
    }
}
