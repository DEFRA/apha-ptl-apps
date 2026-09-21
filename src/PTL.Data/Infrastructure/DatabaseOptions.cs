using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace PTL.Data.Infrastructure;

public sealed record DatabaseOptions(string Host, string Name, string User, string Password, bool TrustServerCertificate, bool IntegratedSecurity = false)
{
    public string ToConnectionString()
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = Host,
            InitialCatalog = Name,
            TrustServerCertificate = TrustServerCertificate
        };

        if (IntegratedSecurity)
        {
            builder.IntegratedSecurity = true;
        }
        else
        {
            builder.UserID = User;
            builder.Password = Password;
        }

        return builder.ConnectionString;
    }

    /// <summary>
    /// Fails fast if any DB config value is missing - a clear, immediate error beats an app that
    /// starts and only fails later on first database use. Host/Name/User/Password come from
    /// Parameter Store as four separate values (no pre-built connection string), so this validates
    /// and packages them for SqlConnectionFactory to compose. TrustServerCertificate defaults to
    /// false (secure by default, correct for RDS's valid certificate) and should only be set true
    /// locally, for a self-signed dev SQL Server certificate.
    /// </summary>
    public static DatabaseOptions RequireFromConfiguration(IConfiguration configuration)
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
}
