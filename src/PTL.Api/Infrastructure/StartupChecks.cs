using Microsoft.Extensions.Configuration;

namespace PTL.Api.Infrastructure;

internal static class StartupChecks
{
    /// <summary>
    /// Fails fast at startup if any DB config value is missing - a clear,
    /// immediate error beats an app that starts and only fails later on
    /// first database use. Host/Name/User/Password come from Parameter
    /// Store as four separate values (no pre-built connection string), so
    /// this validates and packages them for SqlConnectionFactory to
    /// compose. TrustServerCertificate defaults to false (secure by
    /// default, correct for RDS's valid certificate) and should only be
    /// set true locally, for a self-signed dev SQL Server certificate.
    /// </summary>
    public static DatabaseOptions RequireDatabaseOptions(IConfiguration configuration)
    {
        var host = configuration["Database:Host"];
        var name = configuration["Database:Name"];
        var integratedSecurity = configuration.GetValue("Database:IntegratedSecurity", false);
        var user = configuration["Database:User"];
        var password = configuration["Database:Password"];

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException(
                "Database:Host and Database:Name must always be configured. " +
                "Locally, set them in appsettings.Development.json; in a deployed environment, check the " +
                "Database__Host / Database__Name secret wiring in the ECS task definition.");
        }

        if (!integratedSecurity && (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(password)))
        {
            throw new InvalidOperationException(
                "Database:User and Database:Password must be configured unless Database:IntegratedSecurity is true. " +
                "Locally, set them in appsettings.Development.json; in a deployed environment, check the " +
                "Database__User / Database__Password secret wiring in the ECS task definition.");
        }

        var trustServerCertificate = configuration.GetValue("Database:TrustServerCertificate", false);

        return new DatabaseOptions(host, name, user ?? string.Empty, password ?? string.Empty, trustServerCertificate, integratedSecurity);
    }

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
