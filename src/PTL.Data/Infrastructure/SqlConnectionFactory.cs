using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace PTL.Data.Infrastructure;

public sealed class SqlConnectionFactory(IConfiguration configuration) : IDbConnectionFactory
{
    public IDbConnection CreateConnection()
    {
        var options = DatabaseOptions.RequireFromConfiguration(configuration);
        return new SqlConnection(options.ToConnectionString());
    }
}
