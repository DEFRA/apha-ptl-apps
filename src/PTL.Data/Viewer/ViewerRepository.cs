using Dapper;
using PTL.Core.Viewer;
using PTL.Data.Infrastructure;

namespace PTL.Data.Viewer;

public sealed class ViewerRepository(IDbConnectionFactory connectionFactory) : IViewerRepository
{
    // spgaViewers returns 3 result sets (viewers, viewer-schemes, viewer-participants); only the
    // first is needed here, which is also all a plain QueryAsync ever reads.
    public async Task<IReadOnlyList<ViewerEntity>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return (await connection.QueryAsync<ViewerEntity>("EXEC dbo.spgaViewers")).ToList();
    }
}
