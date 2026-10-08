using Dapper;
using PTL.Core.SystemMessage;
using PTL.Data.Infrastructure;

namespace PTL.Data.SystemMessage;

public sealed class SystemMessageRepository(IDbConnectionFactory connectionFactory) : ISystemMessageRepository
{
    public async Task<string?> GetImportantMessageAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        // Reuses the legacy external Home page's existing spgMainPageMessageBySsoId proc rather
        // than adding a new one - Dapper's single-result Query* methods only ever read the first
        // of its two result sets. @SsoId only affects the optional second result set (a
        // participant/viewer/test consultant display name lookup) that this doesn't need; the
        // message row itself is unconditional (WHERE fldMessageId = 0). Guid.NewGuid() rather than
        // Guid.Empty - fldSsoId = Guid.Empty is a legacy "not yet linked" sentinel matching
        // hundreds of real rows, needlessly triggering that second query's work server-side; a
        // fresh random Guid is guaranteed not to match any stored fldSsoId. A dynamic row avoids a
        // one-off DapperColumnMappings entry for a proc whose other 3 columns are never read.
        var row = await connection.QuerySingleOrDefaultAsync(
            "EXEC dbo.spgMainPageMessageBySsoId @SsoId",
            new { SsoId = Guid.NewGuid() });

        if (row is null)
        {
            return null;
        }

        IDictionary<string, object> columns = row;
        return columns["fldImportantMessage"] as string;
    }
}
