using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PTL.Common.Correlation;

namespace PTL.InternalWeb.Features.LoadTesting;

public static class LoadTestingServiceCollectionExtensions
{
    /// <summary>Registers the load-testing sign-in; inert unless <c>LoadTesting:Enabled</c> is true. Delete this feature folder and this call to remove it.</summary>
    public static WebApplicationBuilder AddLoadTesting(this WebApplicationBuilder builder)
    {
        builder.Services.Configure<LoadTestingOptions>(builder.Configuration.GetSection(LoadTestingOptions.SectionName));
        builder.Services.Configure<MvcOptions>(options => options.Filters.Add<LoadTestingLoginRedirectFilter>());
        builder.Services.AddHostedService<LoadTestingStartupWarning>();

        // Same PTL.Api base address as the other typed clients (Api:BaseUrl is validated by AddPtlApiClient).
        builder.Services.AddHttpClient<ILoadTestingLookupClient, LoadTestingLookupClient>(client =>
        {
            client.BaseAddress = new Uri(builder.Configuration["Api:BaseUrl"] ?? "http://localhost");
        })
            .AddHttpMessageHandler<CorrelationIdDelegatingHandler>()
            .AddStandardResilienceHandler();

        return builder;
    }
}

internal sealed partial class LoadTestingStartupWarning(IOptions<LoadTestingOptions> options, ILogger<LoadTestingStartupWarning> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (options.Value.Enabled)
        {
            LogEnabled();
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Warning, Message = "LoadTesting is enabled: Entra ID is bypassed by the username-only sign-in at /perf/login")]
    private partial void LogEnabled();
}
