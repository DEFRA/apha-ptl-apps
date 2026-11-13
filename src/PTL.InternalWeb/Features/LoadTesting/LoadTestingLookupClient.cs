using System.Net;
using System.Net.Http.Json;
using PTL.Contracts.LoadTesting;

namespace PTL.InternalWeb.Features.LoadTesting;

public interface ILoadTestingLookupClient
{
    /// <summary>The directory object id PTL.Api already stores for the username, or <see langword="null"/> when PTL.Api has no such lookup (404).</summary>
    Task<LoadTestingInternalLookupResponse?> LookupAsync(string username, CancellationToken cancellationToken);
}

public sealed class LoadTestingLookupClient(HttpClient httpClient) : ILoadTestingLookupClient
{
    public async Task<LoadTestingInternalLookupResponse?> LookupAsync(string username, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/load-testing/internal-users/lookup")
        {
            Content = JsonContent.Create(new LoadTestingInternalLookupRequest(username))
        };
        request.Headers.Add(LoadTestingHeaders.Enabled, "true");

        using var response = await httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<LoadTestingInternalLookupResponse>(cancellationToken);
    }
}
