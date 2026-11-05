using PTL.ApiClient;
using PTL.Contracts.User;

namespace PTL.InternalWeb.Tests.TestSupport;

// Test double for IUserApiClient so SystemAdministrationController tests don't need a real HTTP
// call to PTL.Api.
internal sealed class FakeUserApiClient : IUserApiClient
{
    public IReadOnlyList<UserResponse> Users { get; set; } = [];
    public CreateUserSaveResult SaveResult { get; set; } = new(true, null, new Dictionary<string, string[]>());
    public IReadOnlyList<UserRoleRowResponse> UserRoleGrid { get; set; } = [];
    public SetUserRolesResponse SetRolesResult { get; set; } = new(true, null);
    public CreateUserRequest? LastCreateRequest { get; private set; }
    public List<(Guid UserId, SetUserRolesRequest Request)> SetRolesCalls { get; } = [];

    public Task<IReadOnlyList<UserResponse>> GetUsersAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Users);


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

    public SetUserRolesBatchResponse? SetRolesBatchResult { get; set; }
    public List<SetUserRolesBatchRequest> SetRolesBatchCalls { get; } = [];

    public Task<SetUserRolesBatchResponse> SetUserRolesBatchAsync(SetUserRolesBatchRequest request, CancellationToken cancellationToken = default)
    {
        SetRolesBatchCalls.Add(request);
        foreach (var item in request.Items)
        {
            SetRolesCalls.Add((item.UserId, new SetUserRolesRequest(item.RoleIds, request.ActingUserId)));
        }

        var response = SetRolesBatchResult ?? new SetUserRolesBatchResponse(
            request.Items.Select(i => new SetUserRolesItemResult(i.UserId, SetRolesResult.Success, SetRolesResult.Message)).ToList());
        return Task.FromResult(response);
    }

    public UserRemoveResponse RemoveResult { get; set; } = new(true, null);
    public List<Guid> RemoveCalls { get; } = [];
    public Guid? LastRemoveActingUserId { get; private set; }

    public Task<UserRemoveResponse> RemoveUserAsync(Guid userId, Guid? actingUserId, CancellationToken cancellationToken = default)
    {
        RemoveCalls.Add(userId);
        LastRemoveActingUserId = actingUserId;
        return Task.FromResult(RemoveResult);
    }

    public IReadOnlyList<UserResponse> TestConsultants { get; set; } = [];
    public List<(Guid UserId, UpdateTestConsultantRequest Request)> UpdateTestConsultantCalls { get; } = [];

    public Task<IReadOnlyList<UserResponse>> GetTestConsultantsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(TestConsultants);

    public Task UpdateTestConsultantAsync(Guid userId, UpdateTestConsultantRequest request, CancellationToken cancellationToken = default)
    {
        UpdateTestConsultantCalls.Add((userId, request));
        return Task.CompletedTask;
    }
}
