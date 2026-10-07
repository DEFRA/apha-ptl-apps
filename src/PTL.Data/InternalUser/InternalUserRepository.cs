using Dapper;
using PTL.Core.InternalUser;
using PTL.Data.Infrastructure;
using CoreInternalUser = PTL.Core.InternalUser.InternalUser;

namespace PTL.Data.InternalUser;

public sealed class InternalUserRepository(IDbConnectionFactory connectionFactory) : IInternalUserRepository
{
    public async Task<CoreInternalUser?> GetBySsoIdIntAsync(Guid ssoIdInt, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        using var multi = await connection.QueryMultipleAsync(
            "EXEC dbo.sppAuthenticate @SsoIdInt=@SsoIdInt",
            new { SsoIdInt = ssoIdInt });

        return await ReadResultAsync(multi);
    }

    public async Task<CoreInternalUser?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        using var multi = await connection.QueryMultipleAsync(
            "EXEC dbo.sppAuthenticate @Username",
            new { Username = username });

        return await ReadResultAsync(multi);
    }

    public async Task UpdateSsoIdIntAsync(Guid userId, Guid ssoIdInt, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        await connection.ExecuteAsync(
            "EXEC dbo.spuUserSsoIdInt @UserId, @SsoIdInt",
            new { UserId = userId, SsoIdInt = ssoIdInt });
    }

    // sppAuthenticate always returns 2 result sets: the user row (0 or 1 rows) then the role list
    // (0+ rows) - see 0009-ExtendSppAuthenticateForSsoIdInt.sql for why the original proc's
    // conditional first result set was made unconditional.
    private static async Task<CoreInternalUser?> ReadResultAsync(SqlMapper.GridReader multi)
    {
        var user = await multi.ReadSingleOrDefaultAsync<CoreInternalUser>();
        // fldRole is a fixed-width varchar column - legacy's own CustomIdentity.vb TrimEnd()s it too.
        var roles = (await multi.ReadAsync<string>()).Select(role => role.TrimEnd()).ToList();

        if (user is null)
        {
            return null;
        }

        user.Roles = roles;
        return user;
    }
}
