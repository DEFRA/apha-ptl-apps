using System.Collections.Concurrent;
using PTL.Core.Invoice;

namespace PTL.Data.Storage;

/// <summary>
/// Non-persistent invoice CSV storage used when no bucket has been provisioned, so the Invoice
/// Generation screen remains usable in development. Selected via <c>InvoiceStorage:Provider</c>.
/// </summary>
public sealed class InMemoryInvoiceStorageService : IInvoiceStorageService
{
    private readonly ConcurrentDictionary<string, byte[]> _files = new(StringComparer.Ordinal);

    public Task SaveAsync(string storageKey, byte[] content, string contentType, CancellationToken cancellationToken = default)
    {
        _files[storageKey] = content;
        return Task.CompletedTask;
    }

    public Task<byte[]?> GetAsync(string storageKey, CancellationToken cancellationToken = default) =>
        Task.FromResult(_files.TryGetValue(storageKey, out var content) ? content : null);
}
