using System.Net;
using System.Net.Http.Json;
using PTL.Contracts.LabUsers;
using PTL.Contracts.LoginInstructions;

namespace PTL.ApiClient;

public interface ILabUserApiClient
{
    /// <summary>The lab and its lab users, or <see langword="null"/> when the lab does not exist.</summary>
    Task<LabUserListResponse?> GetListAsync(Guid participantId, CancellationToken cancellationToken = default);

    /// <summary>One lab user of the lab, or <see langword="null"/> when they are not linked to it.</summary>
    Task<LabUserDetailsResponse?> GetAsync(Guid participantId, Guid labUserId, CancellationToken cancellationToken = default);

    /// <summary>The result of adding the lab user, or <see langword="null"/> when the lab does not exist.</summary>
    Task<LabUserSaveResult?> AddAsync(Guid participantId, LabUserSaveRequest request, CancellationToken cancellationToken = default);

    /// <summary>The result of changing the lab user, or <see langword="null"/> when they are not linked to the lab.</summary>
    Task<LabUserSaveResult?> UpdateAsync(Guid participantId, Guid labUserId, LabUserSaveRequest request, CancellationToken cancellationToken = default);

    /// <summary>Whether the login instructions were sent, or <see langword="null"/> when the lab user is not linked to the lab.</summary>
    Task<LoginInstructionsResponse?> SendLoginInstructionsAsync(Guid participantId, Guid labUserId, CancellationToken cancellationToken = default);
}

// Thin typed HttpClient wrapper around PTL.Api's lab user endpoints - PTL.ExternalWeb only (a lab's primary contact).
public sealed class LabUserApiClient(HttpClient httpClient) : ILabUserApiClient
{
    private static string Base(Guid participantId) => $"/api/participants/{participantId}/lab-users";

    public async Task<LabUserListResponse?> GetListAsync(Guid participantId, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(Base(participantId), cancellationToken);
        return await ReadOrNullAsync<LabUserListResponse>(response, cancellationToken);
    }

    public async Task<LabUserDetailsResponse?> GetAsync(Guid participantId, Guid labUserId, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync($"{Base(participantId)}/{labUserId}", cancellationToken);
        return await ReadOrNullAsync<LabUserDetailsResponse>(response, cancellationToken);
    }

    public async Task<LabUserSaveResult?> AddAsync(Guid participantId, LabUserSaveRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync(Base(participantId), request, cancellationToken);
        return await ReadOrNullAsync<LabUserSaveResult>(response, cancellationToken);
    }

    public async Task<LabUserSaveResult?> UpdateAsync(Guid participantId, Guid labUserId, LabUserSaveRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PutAsJsonAsync($"{Base(participantId)}/{labUserId}", request, cancellationToken);
        return await ReadOrNullAsync<LabUserSaveResult>(response, cancellationToken);
    }

    public async Task<LoginInstructionsResponse?> SendLoginInstructionsAsync(Guid participantId, Guid labUserId, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsync($"{Base(participantId)}/{labUserId}/login-instructions", content: null, cancellationToken);
        return await ReadOrNullAsync<LoginInstructionsResponse>(response, cancellationToken);
    }

    // 404 is an expected answer (not found / not this lab's); anything else that is not a success is a fault.
    private static async Task<T?> ReadOrNullAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
        where T : class
    {
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken);
    }
}
