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

    public async Task<IReadOnlyList<SchemeViewerEntity>> GetSchemeViewersAsync(Guid schemeId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        // spgaViewers takes no parameters and returns every link, so the scheme filter is applied
        // here - the same approach LookupService already uses for spgaSchemeCurrency.
        using var results = await connection.QueryMultipleAsync("EXEC dbo.spgaViewers");

        await results.ReadAsync();
        var links = await results.ReadAsync<SchemeViewerEntity>();
        return links.Where(link => link.SchemeId == schemeId).ToList();
    }

    public async Task AddSchemeViewerAsync(Guid viewerSchemeId, Guid viewerId, Guid schemeId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        await connection.ExecuteAsync(
            "EXEC dbo.spiViewerScheme @ViewerSchemeId = @ViewerSchemeId, @ViewerId = @ViewerId, @SchemeId = @SchemeId",
            new { ViewerSchemeId = viewerSchemeId, ViewerId = viewerId, SchemeId = schemeId });
    }

    public async Task RemoveSchemeViewerAsync(Guid viewerSchemeId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        await connection.ExecuteAsync(
            "EXEC dbo.spdViewerScheme @ViewerSchemeId",
            new { ViewerSchemeId = viewerSchemeId });
    }
}
