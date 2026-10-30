using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace PTL.Common.Graph;

public static class GraphServiceCollectionExtensions
{
    /// <summary>The named HttpClient that already carries the Graph base address, bearer token and resilience handling.</summary>
    public const string GraphHttpClientName = "Graph";

    /// <summary>
    /// Registers the reusable Microsoft Graph plumbing: options from the <c>Graph</c> section, a caching token provider,
    /// and a named client (<see cref="GraphHttpClientName"/>) that features resolve with <c>IHttpClientFactory</c>.
    /// Missing credentials are reported on first use, not at startup, so the host still starts without them.
    /// </summary>
    public static IHttpClientBuilder AddGraphClient(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<GraphOptions>()
            .Bind(configuration.GetSection(GraphOptions.SectionName))
            .PostConfigure(options =>
            {
                // Graph credentials are sourced from the same host's Entra settings unless explicit Graph values are supplied.
                options.TenantId = Resolve(options.TenantId, configuration["Entra:TenantId"]);
                options.ClientId = Resolve(options.ClientId, configuration["Entra:ClientId"]);
                options.ClientSecret = Resolve(options.ClientSecret, configuration["Entra:ClientSecret"]);

                static string Resolve(string current, string? fallback) =>
                    !string.IsNullOrWhiteSpace(current) ? current : fallback ?? string.Empty;
            });
        services.TryAddSingleton(TimeProvider.System);

        services.AddHttpClient(GraphTokenProvider.HttpClientName).AddStandardResilienceHandler();
        services.AddSingleton<IGraphTokenProvider, GraphTokenProvider>();
        services.AddTransient<GraphAuthorizationHandler>();

        var graphClient = services.AddHttpClient(GraphHttpClientName, (provider, client) =>
            client.BaseAddress = new Uri(provider.GetRequiredService<IOptions<GraphOptions>>().Value.BaseUrl));

        // Resilience is added before the authorization handler so it is the outer layer: a retry re-enters the
        // handler and picks up the (cached or refreshed) token again.
        graphClient.AddStandardResilienceHandler();
        graphClient.AddHttpMessageHandler<GraphAuthorizationHandler>();
        return graphClient;
    }
}
