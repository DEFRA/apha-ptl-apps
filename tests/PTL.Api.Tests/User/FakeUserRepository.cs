using PTL.Core.User;

namespace PTL.Api.Tests.User;

internal sealed class FakeUserRepository : IUserRepository
{
    public List<PTL.Core.User.User> Users { get; set; } = [];
    public List<PTL.Core.User.User> Created { get; } = [];

    public Task<IReadOnlyList<PTL.Core.User.User>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PTL.Core.User.User>>(Users);

    public Task CreateAsync(PTL.Core.User.User user, CancellationToken cancellationToken = default)
    {
        Created.Add(user);
        Users.Add(user);
        return Task.CompletedTask;
    }
}

internal sealed class FakeStaffDirectoryService : IStaffDirectoryService
{
    public IReadOnlyList<StaffDirectoryUser> Results { get; set; } = [];
    public string? LastSearchTerm { get; private set; }

    public Task<IReadOnlyList<StaffDirectoryUser>> SearchAsync(string searchTerm, CancellationToken cancellationToken = default)
    {
        LastSearchTerm = searchTerm;
        return Task.FromResult(Results);
    }
}
