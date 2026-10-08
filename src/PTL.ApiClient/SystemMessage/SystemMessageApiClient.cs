using System.Net.Http.Json;
using PTL.Contracts.SystemMessage;

namespace PTL.ApiClient;

public sealed class SystemMessageApiClient(HttpClient httpClient) : ISystemMessageApiClient
{
    public async Task<GetImportantMessageResponse> GetImportantMessageAsync(CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync("/api/system-messages/important", cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<GetImportantMessageResponse>(cancellationToken);
        return result ?? new GetImportantMessageResponse(null);
    }
}
