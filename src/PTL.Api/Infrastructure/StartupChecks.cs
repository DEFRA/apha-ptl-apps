using Microsoft.Extensions.Configuration;
using PTL.Data.Infrastructure;

namespace PTL.Api.Infrastructure;

internal static class StartupChecks
{
    /// <summary>
    /// Fails fast at startup if any DB config value is missing - see
    /// <see cref="DatabaseOptions.RequireFromConfiguration"/> (PTL.Data) for the validation rules,
    /// which SqlConnectionFactory also relies on when creating a connection per request.
    /// </summary>
    public static DatabaseOptions RequireDatabaseOptions(IConfiguration configuration) =>
        DatabaseOptions.RequireFromConfiguration(configuration);

    /// <summary>
    /// Fails fast at startup if the readiness-check key isn't configured.
    /// ReadinessKeyFilter itself can't tell "not configured" apart from
    /// "wrong key" at request time (both must return an identical 404, so a
    /// scanner can't fingerprint the difference) - without this, a broken
    /// secret wiring would silently make /health/ready return 404 forever
    /// instead of surfacing as an obvious deployment failure.
    /// </summary>
    public static string RequireReadinessKey(IConfiguration configuration)
    {
        var key = configuration["HealthCheck:ReadinessKey"];
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException(
                "HealthCheck:ReadinessKey is not configured. Locally, set it in appsettings.Development.json; " +
                "in a deployed environment, check the HealthCheck__ReadinessKey secret wiring in the ECS task definition.");
        }

        return key;
    }
}
