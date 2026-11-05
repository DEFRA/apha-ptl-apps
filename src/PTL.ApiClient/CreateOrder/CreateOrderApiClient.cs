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

// Errors is only filled when the API rejected the save or submit (400) - Status is Ok in that case, as the request was allowed.
public sealed record CreateOrderSubmitApiResult(CreateOrderApiStatus Status, IReadOnlyList<CreateOrderValidationErrorResponse> Errors)
{
    public bool Succeeded => Status == CreateOrderApiStatus.Ok && Errors.Count == 0;
}

public interface ICreateOrderApiClient
{
    Task<CreateOrderEligibilityResponse?> GetEligibilityAsync(Guid participantId, CancellationToken cancellationToken = default);

    // year is "current" or "next". review also loads the stage 2 data consent section.
    Task<CreateOrderApiResult> GetAsync(Guid participantId, string year, bool review = false, CancellationToken cancellationToken = default);
    Task<CreateOrderApiResult> ChangeAsync(Guid participantId, string year, CreateOrderLineChangeRequest request, CancellationToken cancellationToken = default);
    Task<CreateOrderApiStatus> ClearAsync(Guid participantId, string year, CancellationToken cancellationToken = default);
    Task<CreateOrderSubmitApiResult> SavePurchaseOrderAsync(Guid participantId, string year, CreateOrderSaveRequest request, CancellationToken cancellationToken = default);
    Task<CreateOrderSubmitApiResult> SubmitAsync(Guid participantId, string year, CreateOrderSubmitRequest request, CancellationToken cancellationToken = default);
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

    public async Task<CreateOrderApiResult> GetAsync(Guid participantId, string year, bool review = false, CancellationToken cancellationToken = default) =>
        await ReadAsync(await httpClient.GetAsync($"/api/create-order/{participantId}/{year}{(review ? "?review=true" : string.Empty)}", cancellationToken), cancellationToken);

    public async Task<CreateOrderApiResult> ChangeAsync(Guid participantId, string year, CreateOrderLineChangeRequest request, CancellationToken cancellationToken = default) =>
        await ReadAsync(await httpClient.PostAsJsonAsync($"/api/create-order/{participantId}/{year}/change", request, cancellationToken), cancellationToken);

    public async Task<CreateOrderApiStatus> ClearAsync(Guid participantId, string year, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsync($"/api/create-order/{participantId}/{year}/clear", content: null, cancellationToken);
        return ToStatus(response);
    }

    public async Task<CreateOrderSubmitApiResult> SavePurchaseOrderAsync(Guid participantId, string year, CreateOrderSaveRequest request, CancellationToken cancellationToken = default) =>
        await ReadSubmitAsync(await httpClient.PostAsJsonAsync($"/api/create-order/{participantId}/{year}/purchase-order", request, cancellationToken), cancellationToken);

    public async Task<CreateOrderSubmitApiResult> SubmitAsync(Guid participantId, string year, CreateOrderSubmitRequest request, CancellationToken cancellationToken = default) =>
        await ReadSubmitAsync(await httpClient.PostAsJsonAsync($"/api/create-order/{participantId}/{year}/submit", request, cancellationToken), cancellationToken);

    private static async Task<CreateOrderSubmitApiResult> ReadSubmitAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var validation = await response.Content.ReadFromJsonAsync<CreateOrderValidationResponse>(cancellationToken);
            return new CreateOrderSubmitApiResult(CreateOrderApiStatus.Ok, validation?.Errors ?? []);
        }

        return new CreateOrderSubmitApiResult(ToStatus(response), []);
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
