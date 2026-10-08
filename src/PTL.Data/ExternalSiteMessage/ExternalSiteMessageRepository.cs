using Dapper;
using PTL.Core.ExternalSiteMessage;
using PTL.Data.Infrastructure;
using CoreExternalSiteMessage = PTL.Core.ExternalSiteMessage.ExternalSiteMessage;

namespace PTL.Data.ExternalSiteMessage;

// The stored procedures are unchanged legacy objects (spgaMainPageMessage, spuMainPageMessage) -
// this only adds a Dapper-based access path to the singleton tblExtWebsiteMessage row.
public sealed class ExternalSiteMessageRepository(IDbConnectionFactory connectionFactory) : IExternalSiteMessageRepository
{
    public async Task<CoreExternalSiteMessage> GetAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        var result = await connection.QuerySingleOrDefaultAsync<CoreExternalSiteMessage>("EXEC dbo.spgaMainPageMessage");
        return result ?? throw new InvalidOperationException("tblExtWebsiteMessage must have a row with Id = 0.");
    }

    public async Task UpdateAsync(CoreExternalSiteMessage message, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            "EXEC dbo.spuMainPageMessage @Message, @ImportantMessage, @SupportEmailAddress",
            new { message.Message, message.ImportantMessage, message.SupportEmailAddress });
    }
}
