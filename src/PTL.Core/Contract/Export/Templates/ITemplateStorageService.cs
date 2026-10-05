namespace PTL.Core.Contract.Export.Templates;

/// <summary>
/// Object storage for mail merge template files, replacing the legacy shared folder named by the
/// <c>TempFolder</c> appSetting. Deliberately free of any storage-vendor types so business logic,
/// controllers and tests never reference the AWS SDK.
/// </summary>
public interface ITemplateStorageService
{
    Task SaveAsync(string storageKey, byte[] content, string contentType, CancellationToken cancellationToken = default);

    /// <summary>Returns null when the key does not exist.</summary>
    Task<byte[]?> GetAsync(string storageKey, CancellationToken cancellationToken = default);

    Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> ListAsync(string? prefix = null, CancellationToken cancellationToken = default);
}
