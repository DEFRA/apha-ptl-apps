using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.User;

namespace PTL.ApiClient;

public interface IUserApiClient
{
    Task<IReadOnlyList<UserResponse>> GetUsersAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StaffDirectoryUserResponse>> SearchStaffDirectoryAsync(string searchTerm, CancellationToken cancellationToken = default);
    Task<CreateUserSaveResult> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserRoleRowResponse>> GetUserRoleGridAsync(CancellationToken cancellationToken = default);
    Task<SetUserRolesResponse> SetUserRolesAsync(Guid userId, SetUserRolesRequest request, CancellationToken cancellationToken = default);
    Task<UserRemoveResponse> RemoveUserAsync(Guid userId, Guid? actingUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserResponse>> GetTestConsultantsAsync(CancellationToken cancellationToken = default);
    Task UpdateTestConsultantAsync(Guid userId, UpdateTestConsultantRequest request, CancellationToken cancellationToken = default);
}

public sealed class UserApiClient(HttpClient httpClient) : IUserApiClient
{
    public async Task<IReadOnlyList<UserResponse>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        var items = await httpClient.GetFromJsonAsync<IReadOnlyList<UserResponse>>("/api/users", cancellationToken);
        return items ?? [];
    }

    public async Task<IReadOnlyList<StaffDirectoryUserResponse>> SearchStaffDirectoryAsync(string searchTerm, CancellationToken cancellationToken = default)
    {
        var items = await httpClient.GetFromJsonAsync<IReadOnlyList<StaffDirectoryUserResponse>>(
            $"/api/users/directory-search?searchTerm={Uri.EscapeDataString(searchTerm)}", cancellationToken);
        return items ?? [];
    }

    public async Task<CreateUserSaveResult> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync("/api/users", request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(cancellationToken);
            var errors = problem?.Errors is { Count: > 0 }
                ? problem.Errors.ToDictionary(e => e.Key, e => e.Value)
                : new Dictionary<string, string[]> { [string.Empty] = ["The request was invalid."] };
            return new CreateUserSaveResult(false, null, errors);
        }

        response.EnsureSuccessStatusCode();
        var user = await response.Content.ReadFromJsonAsync<UserResponse>(cancellationToken);
        return new CreateUserSaveResult(true, user, new Dictionary<string, string[]>());
    }

    public async Task<IReadOnlyList<UserRoleRowResponse>> GetUserRoleGridAsync(CancellationToken cancellationToken = default)
    {
        var items = await httpClient.GetFromJsonAsync<IReadOnlyList<UserRoleRowResponse>>("/api/users/roles", cancellationToken);
        return items ?? [];
    }

    public async Task<SetUserRolesResponse> SetUserRolesAsync(Guid userId, SetUserRolesRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync($"/api/users/{userId}/roles", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SetUserRolesResponse>(cancellationToken);
        return result ?? new SetUserRolesResponse(false, "The role change could not be saved.");
    }

    public async Task<UserRemoveResponse> RemoveUserAsync(Guid userId, Guid? actingUserId, CancellationToken cancellationToken = default)
    {
        var url = actingUserId is Guid id ? $"/api/users/{userId}?actingUserId={id}" : $"/api/users/{userId}";
        var response = await httpClient.DeleteAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<UserRemoveResponse>(cancellationToken);
        return result ?? new UserRemoveResponse(false, "The user could not be removed.");
    }

    public async Task<IReadOnlyList<UserResponse>> GetTestConsultantsAsync(CancellationToken cancellationToken = default)
    {
        var items = await httpClient.GetFromJsonAsync<IReadOnlyList<UserResponse>>("/api/users/test-consultants", cancellationToken);
        return items ?? [];
    }

    public async Task UpdateTestConsultantAsync(Guid userId, UpdateTestConsultantRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync($"/api/users/{userId}/test-consultant", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
