using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using PTL.Common.Graph;

namespace PTL.InternalWeb.Infrastructure;

/// <summary>
/// <see cref="IEmployeeIdLookup"/> backed by Microsoft Graph, using the shared "Graph" client that already carries the
/// app's bearer token. Failures surface as <see cref="GraphException"/> without any response body.
/// </summary>
public sealed class GraphEmployeeIdLookup(IHttpClientFactory httpClientFactory) : IEmployeeIdLookup
{
    public async Task<string?> FindEmployeeIdAsync(Guid objectId, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(GraphServiceCollectionExtensions.GraphHttpClientName);

        try
        {
            using var response = await client.GetAsync($"users/{objectId:D}?$select=employeeId", cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new GraphException($"Microsoft Graph employee id lookup failed with HTTP {(int)response.StatusCode}.");
            }

            var user = await response.Content.ReadFromJsonAsync<GraphEmployee>(cancellationToken);

            return string.IsNullOrWhiteSpace(user?.EmployeeId) ? null : user.EmployeeId.Trim();
        }
        catch (Exception exception) when (exception is not GraphException && !cancellationToken.IsCancellationRequested)
        {
            throw new GraphException("The Microsoft Graph employee id lookup failed.", exception);
        }
    }

    private sealed class GraphEmployee
    {
        [JsonPropertyName("employeeId")]
        public string? EmployeeId { get; set; }
    }
}
