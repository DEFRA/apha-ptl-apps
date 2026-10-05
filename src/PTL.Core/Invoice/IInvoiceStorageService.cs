namespace PTL.Core.Invoice;

/// <summary>
/// Object storage for generated invoice CSVs, replacing the legacy local-filesystem
/// <c>InvoiceArchivePath</c> (docs/migration/invoice-migration.md "CSV Storage"). Deliberately free
/// of any storage-vendor types, mirroring PTL.Core.Contract.Export.Templates.ITemplateStorageService.
/// </summary>
public interface IInvoiceStorageService
{
    Task SaveAsync(string storageKey, byte[] content, string contentType, CancellationToken cancellationToken = default);

    /// <summary>Returns null when the key does not exist.</summary>
    Task<byte[]?> GetAsync(string storageKey, CancellationToken cancellationToken = default);

    /// <summary>Keys under a prefix. A generation's CSV is found by listing its own id prefix.</summary>
    Task<IReadOnlyList<string>> ListAsync(string prefix, CancellationToken cancellationToken = default);
}
