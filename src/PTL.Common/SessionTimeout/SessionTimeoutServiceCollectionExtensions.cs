using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace PTL.Common.SessionTimeout;

public static class SessionTimeoutServiceCollectionExtensions
{
    /// <summary>Binds and validates the <c>SessionTimeout</c> section for the inactivity warning; a bad value stops startup.</summary>
    public static IServiceCollection AddPtlSessionTimeout(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<SessionTimeoutOptions>()
            .Bind(configuration.GetSection(SessionTimeoutOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<SessionTimeoutOptions>, SessionTimeoutOptionsValidator>();
        return services;
    }
}
