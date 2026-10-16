using System.Collections.Concurrent;
using PTL.Core.Distribution;

namespace PTL.Data.Storage;

// Server memory, like the legacy InProc Session the upload waited in; nothing goes to a folder or S3.
// Entries expire after the legacy Session timeout (20 minutes, sliding) so abandoned uploads are freed.
public sealed class InMemoryAttachmentStagingStore(TimeProvider timeProvider) : IAttachmentStagingStore
{
    public static readonly TimeSpan Expiry = TimeSpan.FromMinutes(20);

    private readonly ConcurrentDictionary<(Guid SchemeId, Guid Token), (StagedAttachment File, DateTimeOffset LastUsed)> _files = new();

    public Task SaveAsync(Guid monthlyDistributionSchemeId, Guid token, StagedAttachment attachment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(attachment);

        var now = timeProvider.GetUtcNow();
        foreach (var entry in _files.Where(e => now - e.Value.LastUsed > Expiry))
        {
            _files.TryRemove(entry.Key, out _);
        }

        _files[(monthlyDistributionSchemeId, token)] = (attachment, now);
        return Task.CompletedTask;
    }

    public Task<StagedAttachment?> GetAsync(Guid monthlyDistributionSchemeId, Guid token, CancellationToken cancellationToken = default)
    {
        var key = (monthlyDistributionSchemeId, token);
        var now = timeProvider.GetUtcNow();
        if (!_files.TryGetValue(key, out var entry))
        {
            return Task.FromResult<StagedAttachment?>(null);
        }

        if (now - entry.LastUsed > Expiry)
        {
            _files.TryRemove(key, out _);
            return Task.FromResult<StagedAttachment?>(null);
        }

        _files[key] = (entry.File, now);
        return Task.FromResult<StagedAttachment?>(entry.File);
    }

    public Task DeleteAsync(Guid monthlyDistributionSchemeId, Guid token, CancellationToken cancellationToken = default)
    {
        _files.TryRemove((monthlyDistributionSchemeId, token), out _);
        return Task.CompletedTask;
    }
}
