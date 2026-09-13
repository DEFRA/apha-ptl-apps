using System.Data;
using Microsoft.Data.SqlClient;

namespace PTL.Api.Infrastructure;

public sealed class SqlConnectionFactory(IConfiguration configuration) : IDbConnectionFactory
{
    public IDbConnection CreateConnection()
    {
        var options = StartupChecks.RequireDatabaseOptions(configuration);
        var connectionString = new SqlConnectionStringBuilder
        {
            DataSource = options.Host,
            InitialCatalog = options.Name,
            UserID = options.User,
            Password = options.Password,
            TrustServerCertificate = options.TrustServerCertificate
        }.ConnectionString;

        return new SqlConnection(connectionString);
    }
}
