using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.Customer;

namespace PTL.ApiClient;

public interface ICustomerApiClient
{
    Task<IReadOnlyList<CustomerSummaryResponse>> GetCustomersAsync(CustomerStatusFilter status = CustomerStatusFilter.Active, CancellationToken cancellationToken = default);
    Task<CustomerResponse?> GetCustomerAsync(Guid customerId, CancellationToken cancellationToken = default);
    Task<CustomerSearchResponse> SearchCustomersAsync(CustomerSearchRequest request, CancellationToken cancellationToken = default);
    Task<CustomerSaveResult> CreateCustomerAsync(CreateCustomerRequest request, CancellationToken cancellationToken = default);
    Task<CustomerSaveResult> UpdateCustomerAsync(Guid customerId, UpdateCustomerRequest request, CancellationToken cancellationToken = default);
    Task<CustomerSaveResult> DeactivateCustomerAsync(Guid customerId, Guid customerStatusId, CancellationToken cancellationToken = default);
    Task<CustomerSaveResult> ReactivateCustomerAsync(Guid customerId, CancellationToken cancellationToken = default);
}

// Thin typed HttpClient wrapper around PTL.Api's customer endpoints, shared by every web
// front-end (PTL.InternalWeb, PTL.ExternalWeb, ...).
public sealed class CustomerApiClient(HttpClient httpClient) : ICustomerApiClient
{
    public async Task<IReadOnlyList<CustomerSummaryResponse>> GetCustomersAsync(CustomerStatusFilter status = CustomerStatusFilter.Active, CancellationToken cancellationToken = default)
    {
        var customers = await httpClient.GetFromJsonAsync<IReadOnlyList<CustomerSummaryResponse>>(
            $"/api/customers?status={status}", cancellationToken);
        return customers ?? [];
    }

    public async Task<CustomerResponse?> GetCustomerAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"/api/customers/{customerId}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CustomerResponse>(cancellationToken);
    }

    public async Task<CustomerSearchResponse> SearchCustomersAsync(CustomerSearchRequest request, CancellationToken cancellationToken = default)
    {
        var query = $"searchTerm={Uri.EscapeDataString(request.SearchTerm ?? string.Empty)}&status={request.Status}&page={request.Page}&pageSize={request.PageSize}";
        var result = await httpClient.GetFromJsonAsync<CustomerSearchResponse>($"/api/customers/search?{query}", cancellationToken);
        return result ?? new CustomerSearchResponse([], 0, request.Page, request.PageSize);
    }

    public async Task<CustomerSaveResult> CreateCustomerAsync(CreateCustomerRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync("/api/customers", request, cancellationToken);
        return await ToSaveResultAsync(response, cancellationToken);
    }

    public async Task<CustomerSaveResult> UpdateCustomerAsync(Guid customerId, UpdateCustomerRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync($"/api/customers/{customerId}", request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new CustomerSaveResult(false, null, new Dictionary<string, string[]> { [string.Empty] = ["Customer was not found."] });
        }

        return await ToSaveResultAsync(response, cancellationToken);
    }

    public async Task<CustomerSaveResult> DeactivateCustomerAsync(Guid customerId, Guid customerStatusId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync($"/api/customers/{customerId}/deactivate", new DeactivateCustomerRequest(customerStatusId), cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new CustomerSaveResult(false, null, new Dictionary<string, string[]> { [string.Empty] = ["Customer was not found."] });
        }

        return await ToSaveResultAsync(response, cancellationToken);
    }

    public async Task<CustomerSaveResult> ReactivateCustomerAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsync($"/api/customers/{customerId}/reactivate", null, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new CustomerSaveResult(false, null, new Dictionary<string, string[]> { [string.Empty] = ["Customer was not found."] });
        }

        return await ToSaveResultAsync(response, cancellationToken);
    }

    private static async Task<CustomerSaveResult> ToSaveResultAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(cancellationToken);
            var errors = problem?.Errors is { Count: > 0 }
                ? problem.Errors.ToDictionary(e => e.Key, e => e.Value)
                : new Dictionary<string, string[]> { [string.Empty] = ["The request was invalid."] };
            return new CustomerSaveResult(false, null, errors);
        }

        response.EnsureSuccessStatusCode();
        var customer = await response.Content.ReadFromJsonAsync<CustomerResponse>(cancellationToken);
        return new CustomerSaveResult(true, customer, new Dictionary<string, string[]>());
    }
}
