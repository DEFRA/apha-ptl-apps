using System.Net;
using System.Net.Http.Json;
using PTL.Contracts.LoadTesting;

namespace PTL.ApiClient;

public interface ILoadTestingLookupClient
{
    /// <summary>The directory object id PTL.Api already stores for the username, or <see langword="null"/> when PTL.Api has no such lookup (404).</summary>
    Task<LoadTestingInternalLookupResponse?> LookupInternalUserAsync(string username, CancellationToken cancellationToken);

    /// <summary>The contact id PTL.Api already stores for the email, or <see langword="null"/> when PTL.Api has no such lookup (404).</summary>
    Task<LoadTestingLookupResponse?> LookupExternalUserAsync(string email, CancellationToken cancellationToken);
}

public sealed class LoadTestingLookupClient(HttpClient httpClient) : ILoadTestingLookupClient
{
    public Task<LoadTestingInternalLookupResponse?> LookupInternalUserAsync(string username, CancellationToken cancellationToken) =>
        PostAsync<LoadTestingInternalLookupResponse>("/api/load-testing/internal-users/lookup", new LoadTestingInternalLookupRequest(username), cancellationToken);

    public Task<LoadTestingLookupResponse?> LookupExternalUserAsync(string email, CancellationToken cancellationToken) =>
        PostAsync<LoadTestingLookupResponse>("/api/load-testing/external-users/lookup", new LoadTestingLookupRequest(email), cancellationToken);

    private async Task<TResponse?> PostAsync<TResponse>(string path, object body, CancellationToken cancellationToken)
        where TResponse : class
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Add(LoadTestingHeaders.Enabled, "true");

        using var response = await httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken);
    }
}
