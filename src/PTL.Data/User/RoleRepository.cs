using Dapper;
using PTL.Core.User;
using PTL.Data.Infrastructure;

namespace PTL.Data.User;

// Unchanged legacy stored procedure (spgaRole) wrapping tblRoles.
public sealed class RoleRepository(IDbConnectionFactory connectionFactory) : IRoleRepository
{
    public async Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return (await connection.QueryAsync<Role>("EXEC dbo.spgaRole")).ToList();
    }
}
