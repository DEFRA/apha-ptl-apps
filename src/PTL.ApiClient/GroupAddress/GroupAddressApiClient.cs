using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.GroupAddress;

namespace PTL.ApiClient;

public interface IGroupAddressApiClient
{
    Task<IReadOnlyList<GroupAddressResponse>> GetGroupAddressesAsync(CancellationToken cancellationToken = default);
    Task<GroupAddressResponse?> GetGroupAddressAsync(Guid groupAddressId, CancellationToken cancellationToken = default);
    Task<GroupAddressSaveResult> CreateGroupAddressAsync(GroupAddressSaveRequest request, CancellationToken cancellationToken = default);
    Task<GroupAddressSaveResult> UpdateGroupAddressAsync(Guid groupAddressId, GroupAddressSaveRequest request, CancellationToken cancellationToken = default);
}

public sealed record GroupAddressSaveResult(bool Success, GroupAddressResponse? GroupAddress, IReadOnlyDictionary<string, string[]> FieldErrors);

public sealed class GroupAddressApiClient(HttpClient httpClient) : IGroupAddressApiClient
{
    public async Task<IReadOnlyList<GroupAddressResponse>> GetGroupAddressesAsync(CancellationToken cancellationToken = default)
    {
        var items = await httpClient.GetFromJsonAsync<IReadOnlyList<GroupAddressResponse>>("/api/group-addresses", cancellationToken);
        return items ?? [];
    }

    public async Task<GroupAddressResponse?> GetGroupAddressAsync(Guid groupAddressId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"/api/group-addresses/{groupAddressId}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<GroupAddressResponse>(cancellationToken);
    }

    public async Task<GroupAddressSaveResult> CreateGroupAddressAsync(GroupAddressSaveRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync("/api/group-addresses", request, cancellationToken);
        return await ToSaveResultAsync(response, cancellationToken);
    }

    public async Task<GroupAddressSaveResult> UpdateGroupAddressAsync(Guid groupAddressId, GroupAddressSaveRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync($"/api/group-addresses/{groupAddressId}", request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new GroupAddressSaveResult(false, null, new Dictionary<string, string[]> { [string.Empty] = ["Group address was not found."] });
        }

        return await ToSaveResultAsync(response, cancellationToken);
    }

    private static async Task<GroupAddressSaveResult> ToSaveResultAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(cancellationToken);
            var errors = problem?.Errors is { Count: > 0 }
                ? problem.Errors.ToDictionary(e => e.Key, e => e.Value)
                : new Dictionary<string, string[]> { [string.Empty] = ["The request was invalid."] };
            return new GroupAddressSaveResult(false, null, errors);
        }

        response.EnsureSuccessStatusCode();
        var groupAddress = await response.Content.ReadFromJsonAsync<GroupAddressResponse>(cancellationToken);
        return new GroupAddressSaveResult(true, groupAddress, new Dictionary<string, string[]>());
    }
}
