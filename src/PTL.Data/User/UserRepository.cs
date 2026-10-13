using Dapper;
using PTL.Core.User;
using PTL.Data.Infrastructure;
using CoreUser = PTL.Core.User.User;

namespace PTL.Data.User;

// The stored procedures are unchanged legacy objects (spgaUser, spiUser) - this only adds a
// Dapper-based access path to tblUsers.
public sealed class UserRepository(IDbConnectionFactory connectionFactory) : IUserRepository
{
    public async Task<IReadOnlyList<CoreUser>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return (await connection.QueryAsync<CoreUser>("EXEC dbo.spgaUser")).ToList();
    }

    public async Task<IReadOnlyList<CoreUser>> GetTestConsultantsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return (await connection.QueryAsync<CoreUser>("EXEC dbo.spgaUserTestConsultant")).ToList();
    }

    public async Task UpdateDepartmentAsync(Guid userId, string department, bool isInactive, DateTime? inactiveDate, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            "EXEC dbo.spuUserDept @UserId, @Department, @IsInactive, @InactiveDate",
            new { UserId = userId, Department = department, IsInactive = isInactive, InactiveDate = inactiveDate });
    }

    public async Task CreateAsync(CoreUser user, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            "EXEC dbo.spiUser @UserId, @Username, @FriendlyName, @FirstName, @LastName, @Email, @Department, @IsInactive, @InactiveDate",
            new { user.UserId, user.Username, user.FriendlyName, user.FirstName, user.LastName, user.Email, user.Department, user.IsInactive, user.InactiveDate });
    }

    public async Task DeleteAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync("EXEC dbo.spdUser @UserId", new { UserId = userId });
    }
}
