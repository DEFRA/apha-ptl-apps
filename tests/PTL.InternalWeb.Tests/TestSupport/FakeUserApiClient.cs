using PTL.ApiClient;
using PTL.Contracts.User;

namespace PTL.InternalWeb.Tests.TestSupport;

// Test double for IUserApiClient so SystemAdministrationController tests don't need a real HTTP
// call to PTL.Api.
internal sealed class FakeUserApiClient : IUserApiClient
{
    public IReadOnlyList<UserResponse> Users { get; set; } = [];
    public IReadOnlyList<StaffDirectoryUserResponse> SearchResults { get; set; } = [];
    public CreateUserSaveResult SaveResult { get; set; } = new(true, null, new Dictionary<string, string[]>());
    public IReadOnlyList<UserRoleRowResponse> UserRoleGrid { get; set; } = [];
    public SetUserRolesResponse SetRolesResult { get; set; } = new(true, null);
    public string? LastSearchTerm { get; private set; }
    public CreateUserRequest? LastCreateRequest { get; private set; }
    public List<(Guid UserId, SetUserRolesRequest Request)> SetRolesCalls { get; } = [];

    public Task<IReadOnlyList<UserResponse>> GetUsersAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Users);

    public Task<IReadOnlyList<StaffDirectoryUserResponse>> SearchStaffDirectoryAsync(string searchTerm, CancellationToken cancellationToken = default)
    {
        LastSearchTerm = searchTerm;
        return Task.FromResult(SearchResults);
    }

    public Task<CreateUserSaveResult> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        LastCreateRequest = request;
        return Task.FromResult(SaveResult);
    }

    public Task<IReadOnlyList<UserRoleRowResponse>> GetUserRoleGridAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(UserRoleGrid);

    public Task<SetUserRolesResponse> SetUserRolesAsync(Guid userId, SetUserRolesRequest request, CancellationToken cancellationToken = default)
    {
        SetRolesCalls.Add((userId, request));
        return Task.FromResult(SetRolesResult);
    }
}
