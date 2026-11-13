using System.Net;
using System.Net.Http.Json;
using PTL.Contracts.LoadTesting;

namespace PTL.ExternalWeb.Features.LoadTesting;

public interface ILoadTestingLookupClient
{
    /// <summary>The contact id PTL.Api already stores for the email, or <see langword="null"/> when PTL.Api has no such lookup (404).</summary>
    Task<LoadTestingLookupResponse?> LookupAsync(string email, CancellationToken cancellationToken);
}

public sealed class LoadTestingLookupClient(HttpClient httpClient) : ILoadTestingLookupClient
{
    public async Task<LoadTestingLookupResponse?> LookupAsync(string email, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/load-testing/external-users/lookup")
        {
            Content = JsonContent.Create(new LoadTestingLookupRequest(email))
        };
        request.Headers.Add(LoadTestingHeaders.Enabled, "true");

        using var response = await httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<LoadTestingLookupResponse>(cancellationToken);
    }
}
