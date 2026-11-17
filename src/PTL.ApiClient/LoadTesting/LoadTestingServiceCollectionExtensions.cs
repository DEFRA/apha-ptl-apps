using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PTL.Common.Correlation;

namespace PTL.ApiClient;

public static class LoadTestingServiceCollectionExtensions
{
    /// <summary>
    /// Registers the load-testing sign-in; inert unless <c>LoadTesting:Enabled</c> is true. Call after
    /// <see cref="ApiClientServiceCollectionExtensions.AddPtlApiClient"/>, which validates <c>Api:BaseUrl</c> and registers the
    /// correlation handler. Delete the app's LoadTesting feature folder, this folder and this call to remove it.
    /// </summary>
    /// <param name="bypassedSignIn">Which identity provider is bypassed and how, for the startup warning.</param>
    public static IServiceCollection AddPtlLoadTesting(this IServiceCollection services, IConfiguration configuration, string bypassedSignIn)
    {
        var apiBaseUrl = configuration["Api:BaseUrl"]
            ?? throw new InvalidOperationException("Configuration value 'Api:BaseUrl' is required.");

        services.Configure<LoadTestingOptions>(configuration.GetSection(LoadTestingOptions.SectionName));
        services.Configure<MvcOptions>(options => options.Filters.Add<LoadTestingLoginRedirectFilter>());
        services.AddHostedService(provider => new LoadTestingStartupWarning(
            provider.GetRequiredService<IOptions<LoadTestingOptions>>(),
            provider.GetRequiredService<ILogger<LoadTestingStartupWarning>>(),
            bypassedSignIn));

        services.AddHttpClient<ILoadTestingLookupClient, LoadTestingLookupClient>(client =>
        {
            client.BaseAddress = new Uri(apiBaseUrl);
        })
            .AddHttpMessageHandler<CorrelationIdDelegatingHandler>()
            .AddStandardResilienceHandler();

        return services;
    }
}

internal sealed partial class LoadTestingStartupWarning(
    IOptions<LoadTestingOptions> options,
    ILogger<LoadTestingStartupWarning> logger,
    string bypassedSignIn) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (options.Value.Enabled)
        {
            LogEnabled(bypassedSignIn);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Warning, Message = "LoadTesting is enabled: {BypassedSignIn} at /perf/login")]
    private partial void LogEnabled(string bypassedSignIn);
}
