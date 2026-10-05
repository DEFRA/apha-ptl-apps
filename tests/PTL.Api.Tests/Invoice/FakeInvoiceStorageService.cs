using System.Collections.Concurrent;
using PTL.Core.Invoice;

namespace PTL.Api.Tests.Invoice;

// In-memory IInvoiceStorageService test double - equivalent to the real InMemoryInvoiceStorageService
// but owned by the test project so InvoiceService tests can assert what was saved.
internal sealed class FakeInvoiceStorageService : IInvoiceStorageService
{
    private readonly ConcurrentDictionary<string, byte[]> _content = new();

    public string? LastStorageKey { get; private set; }
    public byte[]? LastContent { get; private set; }
    public bool ThrowOnSave { get; set; }

    public Task SaveAsync(string storageKey, byte[] content, string contentType, CancellationToken cancellationToken = default)
    {
        if (ThrowOnSave)
        {
            throw new InvalidOperationException("Simulated storage failure.");
        }

        LastStorageKey = storageKey;
        LastContent = content;
        _content[storageKey] = content;
        return Task.CompletedTask;
    }

    public Task<byte[]?> GetAsync(string storageKey, CancellationToken cancellationToken = default) =>
        Task.FromResult(_content.TryGetValue(storageKey, out var value) ? value : null);

    public Task<IReadOnlyList<string>> ListAsync(string prefix, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> keys = [.. _content.Keys.Where(key => key.StartsWith(prefix, StringComparison.Ordinal)).OrderBy(key => key, StringComparer.Ordinal)];
        return Task.FromResult(keys);
    }
}
