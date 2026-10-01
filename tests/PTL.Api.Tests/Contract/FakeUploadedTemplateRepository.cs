using PTL.Core.Contract.Export.Templates;

namespace PTL.Api.Tests.Contract;

internal sealed class FakeUploadedTemplateRepository : IUploadedTemplateRepository
{
    public List<UploadedTemplate> Templates { get; } = [];

    public Task<IReadOnlyList<UploadedTemplate>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<UploadedTemplate>>([.. Templates]);

    public Task<IReadOnlyList<UploadedTemplate>> GetByDocumentTypeAsync(string documentType, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<UploadedTemplate>>(
            [.. Templates.Where(t => string.Equals(t.DocumentType, documentType, StringComparison.OrdinalIgnoreCase))]);

    public Task CreateAsync(UploadedTemplate uploadedTemplate, CancellationToken cancellationToken = default)
    {
        Templates.Add(uploadedTemplate);
        return Task.CompletedTask;
    }

    public Task SetSelectedAsync(Guid fileId, bool selected, CancellationToken cancellationToken = default)
    {
        var template = Templates.FirstOrDefault(t => t.FileId == fileId);
        if (template is not null)
        {
            template.Selected = selected;
        }

        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid fileId, CancellationToken cancellationToken = default)
    {
        Templates.RemoveAll(t => t.FileId == fileId);
        return Task.CompletedTask;
    }
}

internal sealed class FakeTemplateStorageService : ITemplateStorageService
{
    public Dictionary<string, byte[]> Files { get; } = new(StringComparer.Ordinal);

    public bool ThrowOnSave { get; set; }

    public Task SaveAsync(string storageKey, byte[] content, string contentType, CancellationToken cancellationToken = default)
    {
        if (ThrowOnSave)
        {
            throw new InvalidOperationException("Storage unavailable.");
        }

        Files[storageKey] = content;
        return Task.CompletedTask;
    }

    public Task<byte[]?> GetAsync(string storageKey, CancellationToken cancellationToken = default) =>
        Task.FromResult(Files.TryGetValue(storageKey, out var content) ? content : null);

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        Files.Remove(storageKey);
        return Task.CompletedTask;
    }
}
