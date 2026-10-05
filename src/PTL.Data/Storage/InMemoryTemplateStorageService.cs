using System.Collections.Concurrent;
using PTL.Core.Contract.Export.Templates;

namespace PTL.Data.Storage;

/// <summary>
/// Non-persistent template storage used when no bucket has been provisioned, so the Exports screens
/// remain usable in development. Selected via <c>TemplateStorage:Provider</c>.
/// </summary>
public sealed class InMemoryTemplateStorageService : ITemplateStorageService
{
    private readonly ConcurrentDictionary<string, byte[]> _files = new(StringComparer.Ordinal);

    public Task SaveAsync(string storageKey, byte[] content, string contentType, CancellationToken cancellationToken = default)
    {
        _files[storageKey] = content;
        return Task.CompletedTask;
    }

    public Task<byte[]?> GetAsync(string storageKey, CancellationToken cancellationToken = default) =>
        Task.FromResult(_files.TryGetValue(storageKey, out var content) ? content : null);

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        _files.TryRemove(storageKey, out _);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> ListAsync(string? prefix = null, CancellationToken cancellationToken = default)
    {
        var normalized = prefix?.Trim('/') ?? string.Empty;
        var keys = _files.Keys
            .Where(key => string.IsNullOrEmpty(normalized) || key.StartsWith(normalized, StringComparison.OrdinalIgnoreCase))
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return Task.FromResult<IReadOnlyList<string>>(keys);
    }
}
