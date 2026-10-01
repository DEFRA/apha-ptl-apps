using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.PostagePricingPlan;

namespace PTL.ApiClient;

public interface IPostagePricingPlanApiClient
{
    Task<PostagePricingPlanYearsResponse> GetYearsAsync(CancellationToken cancellationToken = default);
    Task<PostagePricingPlanSaveResult> SetPriceAsync(UpdatePostagePricingPlanPriceRequest request, CancellationToken cancellationToken = default);
    Task<PostagePricingPlanRenewResponse> RenewAsync(CancellationToken cancellationToken = default);
}

// Thin typed HttpClient wrapper around PTL.Api's postage pricing plan endpoints, shared by every
// web front-end (PTL.InternalWeb, PTL.ExternalWeb, ...).
public sealed class PostagePricingPlanApiClient(HttpClient httpClient) : IPostagePricingPlanApiClient
{
    public async Task<PostagePricingPlanYearsResponse> GetYearsAsync(CancellationToken cancellationToken = default)
    {
        var years = await httpClient.GetFromJsonAsync<PostagePricingPlanYearsResponse>("/api/postage-pricing-plan/years", cancellationToken);
        return years ?? new PostagePricingPlanYearsResponse([], false, null, null);
    }

    public async Task<PostagePricingPlanSaveResult> SetPriceAsync(UpdatePostagePricingPlanPriceRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync("/api/postage-pricing-plan/price", request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(cancellationToken);
            var errors = problem?.Errors is { Count: > 0 }
                ? problem.Errors.ToDictionary(e => e.Key, e => e.Value)
                : new Dictionary<string, string[]> { [string.Empty] = ["The request was invalid."] };
            return new PostagePricingPlanSaveResult(false, errors);
        }

        response.EnsureSuccessStatusCode();
        return new PostagePricingPlanSaveResult(true, new Dictionary<string, string[]>());
    }

    public async Task<PostagePricingPlanRenewResponse> RenewAsync(CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsync("/api/postage-pricing-plan/renew", null, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PostagePricingPlanRenewResponse>(cancellationToken);
        return result ?? new PostagePricingPlanRenewResponse(false, "The renew request did not return a result.");
    }
}
