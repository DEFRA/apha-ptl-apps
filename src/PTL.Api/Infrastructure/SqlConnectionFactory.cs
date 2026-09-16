using System.Data;
using Microsoft.Data.SqlClient;

namespace PTL.Api.Infrastructure;

public sealed class SqlConnectionFactory(IConfiguration configuration) : IDbConnectionFactory
{
    public IDbConnection CreateConnection()
    {
        var options = StartupChecks.RequireDatabaseOptions(configuration);
        return new SqlConnection(options.ToConnectionString());
    }
}
