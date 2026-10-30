using System.Net;
using System.Net.Http.Json;
using PTL.Contracts.CreateOrder;

namespace PTL.ApiClient;

public enum CreateOrderApiStatus
{
    Ok,
    NotAllowed,
    NotFound
}

public sealed record CreateOrderApiResult(CreateOrderApiStatus Status, CreateOrderResponse? Order = null);

public interface ICreateOrderApiClient
{
    Task<CreateOrderEligibilityResponse?> GetEligibilityAsync(Guid participantId, CancellationToken cancellationToken = default);

    // year is "current" or "next".
    Task<CreateOrderApiResult> GetAsync(Guid participantId, string year, CancellationToken cancellationToken = default);
    Task<CreateOrderApiResult> ChangeAsync(Guid participantId, string year, CreateOrderLineChangeRequest request, CancellationToken cancellationToken = default);
    Task<CreateOrderApiStatus> ClearAsync(Guid participantId, string year, CancellationToken cancellationToken = default);
}

// Thin typed HttpClient wrapper around PTL.Api's participant-scoped create-order endpoint - PTL.ExternalWeb only
// (legacy PendingContractOrder.aspx stage 1).
public sealed class CreateOrderApiClient(HttpClient httpClient) : ICreateOrderApiClient
{
    public async Task<CreateOrderEligibilityResponse?> GetEligibilityAsync(Guid participantId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"/api/create-order/{participantId}/eligibility", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CreateOrderEligibilityResponse>(cancellationToken);
    }

    public async Task<CreateOrderApiResult> GetAsync(Guid participantId, string year, CancellationToken cancellationToken = default) =>
        await ReadAsync(await httpClient.GetAsync($"/api/create-order/{participantId}/{year}", cancellationToken), cancellationToken);

    public async Task<CreateOrderApiResult> ChangeAsync(Guid participantId, string year, CreateOrderLineChangeRequest request, CancellationToken cancellationToken = default) =>
        await ReadAsync(await httpClient.PostAsJsonAsync($"/api/create-order/{participantId}/{year}/change", request, cancellationToken), cancellationToken);

    public async Task<CreateOrderApiStatus> ClearAsync(Guid participantId, string year, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsync($"/api/create-order/{participantId}/{year}/clear", content: null, cancellationToken);
        return ToStatus(response);
    }

    private static async Task<CreateOrderApiResult> ReadAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var status = ToStatus(response);
        if (status != CreateOrderApiStatus.Ok)
        {
            return new CreateOrderApiResult(status);
        }

        var order = await response.Content.ReadFromJsonAsync<CreateOrderResponse>(cancellationToken);
        return new CreateOrderApiResult(CreateOrderApiStatus.Ok, order);
    }

    private static CreateOrderApiStatus ToStatus(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            return CreateOrderApiStatus.NotAllowed;
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return CreateOrderApiStatus.NotFound;
        }

        response.EnsureSuccessStatusCode();
        return CreateOrderApiStatus.Ok;
    }
}
