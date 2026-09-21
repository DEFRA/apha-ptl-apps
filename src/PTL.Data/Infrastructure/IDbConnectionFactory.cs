using System.Data;

namespace PTL.Data.Infrastructure;

/// <summary>
/// Single place that knows how to open a SQL Server connection. Dapper works
/// against IDbConnection and expects the caller to own its lifetime, so this
/// creates one per unit of work rather than sharing a long-lived connection.
/// </summary>
public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
}
