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

        services.AddHttpClient<IApiClient, ApiClient>(client =>
        {
            client.BaseAddress = new Uri(apiBaseUrl);
        })
            .AddStandardResilienceHandler();

        return services;
    }
}
