using Dapper;
using PTL.Contracts.ExternalUser;
using PTL.Core.ExternalUser;
using PTL.Data.Infrastructure;

namespace PTL.Data.ExternalUser;

public sealed class ExternalUserRepository(IDbConnectionFactory connectionFactory) : IExternalUserRepository
{
    private const int EmailMaxLength = 255;

    public Task<IReadOnlyList<ExternalUserMatch>> FindBySsoIdExtAsync(Guid ssoIdExt, CancellationToken cancellationToken = default) =>
        QueryAsync("EXEC dbo.spgExternalUserBySsoIdExt @SsoIdExt", new { SsoIdExt = ssoIdExt }, cancellationToken);

    public Task<IReadOnlyList<ExternalUserMatch>> FindByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        QueryAsync("EXEC dbo.spgExternalUserByEmail @Email", new { Email = AnsiEmail(email) }, cancellationToken);

    public Task<IReadOnlyList<ExternalUserMatch>> LinkAsync(ExternalUserType userType, string email, Guid ssoIdExt, CancellationToken cancellationToken = default) =>
        QueryAsync(
            "EXEC dbo.spuExternalUserSsoIdExt @UserType, @Email, @SsoIdExt",
            new { UserType = (int)userType, Email = AnsiEmail(email), SsoIdExt = ssoIdExt },
            cancellationToken);

    // The procedures take varchar, so the email is sent as varchar rather than Dapper's default nvarchar - otherwise
    // SQL Server would convert the indexed column on every row to compare it.
    private static DbString AnsiEmail(string email) =>
        new() { Value = email, IsAnsi = true, Length = EmailMaxLength };

    private async Task<IReadOnlyList<ExternalUserMatch>> QueryAsync(string sql, object parameters, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();

        var matches = await connection.QueryAsync<ExternalUserMatch>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return matches.ToList();
    }
}
