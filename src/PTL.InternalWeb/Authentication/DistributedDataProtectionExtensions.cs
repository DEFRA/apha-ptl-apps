using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;

namespace PTL.InternalWeb.Authentication;

/// <summary>
/// Persists the ASP.NET Core Data Protection key ring to Redis so every ECS task, in every AZ,
/// can encrypt/decrypt the same auth, correlation, and antiforgery cookies.
/// </summary>
public static class DistributedDataProtectionExtensions
{
    public const string ApplicationName = "PTL.InternalWeb";
    private const string RedisKeyName = $"{ApplicationName}-DataProtection-Keys";

    // Every ECS task must be able to decrypt cookies encrypted by every other task, or
    // load-balanced requests intermittently fail to unprotect them - surfacing as random
    // sign-outs or endless redirect loops back to Entra ID. The default local-filesystem key
    // ring is per-container and does not survive this, so keys are persisted to the shared
    // Redis (ElastiCache) instance.
    public static IServiceCollection AddDistributedDataProtection(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var connectionString = configuration["Redis:ConnectionString"];
        var dataProtectionBuilder = services.AddDataProtection().SetApplicationName(ApplicationName);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            if (!environment.IsDevelopment())
            {
                throw new InvalidOperationException(
                    "Configuration value 'Redis:ConnectionString' is required outside Development so that " +
                    "Data Protection keys can be shared across every ECS task; without it, SSO breaks " +
                    "intermittently as soon as more than one task is running behind the load balancer.");
            }

            // Local, single-instance development only - keys stay on disk and never need to be shared.
            return services;
        }

        var redisOptions = ConfigurationOptions.Parse(connectionString);
        redisOptions.AbortOnConnectFail = false; // don't fail app startup if Redis is briefly unreachable during a deploy
        var connectionMultiplexer = ConnectionMultiplexer.Connect(redisOptions);

        dataProtectionBuilder.PersistKeysToStackExchangeRedis(connectionMultiplexer, RedisKeyName);

        return services;
    }
}
