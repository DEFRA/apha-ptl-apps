using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using PTL.Contracts.Contract;

namespace PTL.ApiClient;

public interface IContractApiClient
{
    Task<ContractResponse?> GetContractAsync(Guid contractId, CancellationToken cancellationToken = default);
    Task<ContractSearchResponse> GetContractsForCustomerAsync(Guid customerId, ContractSearchRequest request, CancellationToken cancellationToken = default);
    Task<ContractSearchResponse> GetContractsForCustomerByYearAsync(Guid customerId, int yearId, CancellationToken cancellationToken = default);
    Task<ContractSaveResult> CreateContractAsync(Guid customerId, ContractRequest request, CancellationToken cancellationToken = default);
    Task<ContractSaveResult> UpdateContractAsync(Guid contractId, ContractRequest request, CancellationToken cancellationToken = default);
    Task<ContractItemsResponse?> GetContractItemsAsync(Guid contractId, IReadOnlyCollection<Guid>? pendingRemovalIds = null, CancellationToken cancellationToken = default);
    Task<ContractItemRemovalResult> RemoveContractItemAsync(Guid contractId, Guid participantSchemeId, string? auditWhy = null, CancellationToken cancellationToken = default);
    Task<PendingOrderListResponse> GetPendingOrdersAsync(CancellationToken cancellationToken = default);
    Task<PendingOrderDetailsResponse?> GetPendingOrderAsync(Guid pendingContractId, CancellationToken cancellationToken = default);
    Task<bool> UpdatePendingOrderSchemeAsync(Guid pendingContractId, Guid pendingParticipantSchemeId, PendingOrderSchemeUpdateRequest request, CancellationToken cancellationToken = default);
    Task<PendingOrderDecisionResult> ApprovePendingOrderAsync(Guid pendingContractId, PendingOrderApproveRequest request, CancellationToken cancellationToken = default);
    Task<PendingOrderDecisionResult> DeclinePendingOrderAsync(Guid pendingContractId, PendingOrderDeclineRequest request, CancellationToken cancellationToken = default);
}

// Thin typed HttpClient wrapper around PTL.Api's contract endpoints, shared by every web
// front-end (PTL.InternalWeb, PTL.ExternalWeb, ...).
public sealed class ContractApiClient(HttpClient httpClient) : IContractApiClient
{
    public async Task<PendingOrderListResponse> GetPendingOrdersAsync(CancellationToken cancellationToken = default)
    {
        var result = await httpClient.GetFromJsonAsync<PendingOrderListResponse>("/api/contracts/pending-orders", cancellationToken);
        return result ?? new PendingOrderListResponse([], []);
    }

    public async Task<PendingOrderDetailsResponse?> GetPendingOrderAsync(Guid pendingContractId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"/api/contracts/pending-orders/{pendingContractId}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PendingOrderDetailsResponse>(cancellationToken);
    }

    public async Task<bool> UpdatePendingOrderSchemeAsync(Guid pendingContractId, Guid pendingParticipantSchemeId, PendingOrderSchemeUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync(
            $"/api/contracts/pending-orders/{pendingContractId}/schemes/{pendingParticipantSchemeId}", request, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<PendingOrderDecisionResult> ApprovePendingOrderAsync(Guid pendingContractId, PendingOrderApproveRequest request, CancellationToken cancellationToken = default) =>
        await PostPendingOrderDecisionAsync($"/api/contracts/pending-orders/{pendingContractId}/approve", request, cancellationToken);

    public async Task<PendingOrderDecisionResult> DeclinePendingOrderAsync(Guid pendingContractId, PendingOrderDeclineRequest request, CancellationToken cancellationToken = default) =>
        await PostPendingOrderDecisionAsync($"/api/contracts/pending-orders/{pendingContractId}/decline", request, cancellationToken);

    private async Task<PendingOrderDecisionResult> PostPendingOrderDecisionAsync<TRequest>(string url, TRequest request, CancellationToken cancellationToken)
    {
        var response = await httpClient.PostAsJsonAsync(url, request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new PendingOrderDecisionResult(false, true, new Dictionary<string, string[]>());
        }

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(cancellationToken);
            var errors = problem?.Errors is { Count: > 0 }
                ? problem.Errors.ToDictionary(e => e.Key, e => e.Value)
                : new Dictionary<string, string[]> { [string.Empty] = ["The request was invalid."] };
            return new PendingOrderDecisionResult(false, false, errors);
        }

        response.EnsureSuccessStatusCode();
        return new PendingOrderDecisionResult(true, false, new Dictionary<string, string[]>());
    }

    public async Task<ContractResponse?> GetContractAsync(Guid contractId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"/api/contracts/{contractId}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ContractResponse>(cancellationToken);
    }

    public async Task<ContractSearchResponse> GetContractsForCustomerAsync(Guid customerId, ContractSearchRequest request, CancellationToken cancellationToken = default)
    {
        var query = $"period={request.Period}&searchTerm={Uri.EscapeDataString(request.SearchTerm ?? string.Empty)}&page={request.Page}&pageSize={request.PageSize}";
        if (request.YearId.HasValue)
        {
            query += $"&yearId={request.YearId.Value}";
        }

        var result = await httpClient.GetFromJsonAsync<ContractSearchResponse>($"/api/customers/{customerId}/contracts?{query}", cancellationToken);
        return result ?? new ContractSearchResponse([], 0, request.Page, request.PageSize);
    }

    public Task<ContractSearchResponse> GetContractsForCustomerByYearAsync(Guid customerId, int yearId, CancellationToken cancellationToken = default) =>
        GetContractsForCustomerAsync(customerId, new ContractSearchRequest(YearId: yearId), cancellationToken);

    public async Task<ContractSaveResult> CreateContractAsync(Guid customerId, ContractRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync($"/api/customers/{customerId}/contracts", request, cancellationToken);
        return await ToSaveResultAsync(response, cancellationToken);
    }

    public async Task<ContractSaveResult> UpdateContractAsync(Guid contractId, ContractRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync($"/api/contracts/{contractId}", request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new ContractSaveResult(false, null, new Dictionary<string, string[]> { [string.Empty] = ["Contract was not found."] });
        }

        return await ToSaveResultAsync(response, cancellationToken);
    }

    private static async Task<ContractSaveResult> ToSaveResultAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(cancellationToken);
            var errors = problem?.Errors is { Count: > 0 }
                ? problem.Errors.ToDictionary(e => e.Key, e => e.Value)
                : new Dictionary<string, string[]> { [string.Empty] = ["The request was invalid."] };
            return new ContractSaveResult(false, null, errors);
        }

        response.EnsureSuccessStatusCode();
        var contract = await response.Content.ReadFromJsonAsync<ContractResponse>(cancellationToken);
        return new ContractSaveResult(true, contract, new Dictionary<string, string[]>());
    }

    public async Task<ContractItemsResponse?> GetContractItemsAsync(Guid contractId, IReadOnlyCollection<Guid>? pendingRemovalIds = null, CancellationToken cancellationToken = default)
    {
        var query = pendingRemovalIds is { Count: > 0 } ? "?" + string.Join('&', pendingRemovalIds.Select(id => $"pending={id}")) : string.Empty;
        var response = await httpClient.GetAsync($"/api/contracts/{contractId}/items{query}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ContractItemsResponse>(cancellationToken);
    }

    public async Task<ContractItemRemovalResult> RemoveContractItemAsync(Guid contractId, Guid participantSchemeId, string? auditWhy = null, CancellationToken cancellationToken = default)
    {
        var path = $"/api/contracts/{contractId:D}/items/{participantSchemeId:D}";
        var url = string.IsNullOrWhiteSpace(auditWhy) ? path : QueryHelpers.AddQueryString(path, "auditWhy", auditWhy);
        var response = await httpClient.DeleteAsync(url, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new ContractItemRemovalResult(false, true, "This contract item was not found.");
        }

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(cancellationToken);
            var message = problem?.Errors is { Count: > 0 } errors ? string.Join(" ", errors.Values.SelectMany(v => v)) : "The request was invalid.";
            return new ContractItemRemovalResult(false, false, message);
        }

        response.EnsureSuccessStatusCode();
        return new ContractItemRemovalResult(true, false, null);
    }
}
