using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PTL.ApiClient;

public static class ApiClientServiceCollectionExtensions
{
    // Registers a typed HttpClient for PTL.Api, resolving the base address from configuration
    // (Api:BaseUrl) so local dev, ECS Service Connect (internal DNS alias) and any other
    // environment just change the one value.
    public static IServiceCollection AddPtlApiClient(this IServiceCollection services, IConfiguration configuration)
    {
        var apiBaseUrl = configuration["Api:BaseUrl"]
            ?? throw new InvalidOperationException("Configuration value 'Api:BaseUrl' is required.");

        // Fail fast at startup on a malformed value (e.g. a Service Connect DNS name
        // configured without an http(s):// scheme), rather than a confusing failure on
        // the first outgoing request.
        if (!Uri.TryCreate(apiBaseUrl, UriKind.Absolute, out var baseUri) ||
            (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException(
                $"Configuration value 'Api:BaseUrl' ('{apiBaseUrl}') must be an absolute http:// or https:// URL, e.g. 'http://ptl-api:8080'.");
        }

        services.AddHttpClient<IApiClient, ApiClient>(client =>
        {
            client.BaseAddress = baseUri;
        })
            .AddStandardResilienceHandler();

        services.AddHttpClient<ICustomerApiClient, CustomerApiClient>(client =>
        {
            client.BaseAddress = new Uri(apiBaseUrl);
        })
            .AddStandardResilienceHandler();

        services.AddHttpClient<IParticipantApiClient, ParticipantApiClient>(client =>
        {
            client.BaseAddress = new Uri(apiBaseUrl);
        })
            .AddStandardResilienceHandler();

        services.AddHttpClient<IContractApiClient, ContractApiClient>(client =>
        {
            client.BaseAddress = new Uri(apiBaseUrl);
        })
            .AddStandardResilienceHandler();

        services.AddHttpClient<ISchemeApiClient, SchemeApiClient>(client =>
        {
            client.BaseAddress = new Uri(apiBaseUrl);
        })
            .AddStandardResilienceHandler();

        services.AddHttpClient<ILookupApiClient, LookupApiClient>(client =>
        {
            client.BaseAddress = new Uri(apiBaseUrl);
        })
            .AddStandardResilienceHandler();

        services.AddHealthChecks()
            .AddCheck<ApiConnectivityHealthCheck>("api-connectivity");

        return services;
    }
}
