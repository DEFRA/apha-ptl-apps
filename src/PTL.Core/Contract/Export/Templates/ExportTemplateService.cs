using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace PTL.Core.Contract.Export.Templates;

public interface IExportTemplateService
{
    Task<IReadOnlyList<UploadedTemplate>> GetTemplatesAsync(string documentType, CancellationToken cancellationToken = default);

    Task<UploadedTemplate?> GetActiveTemplateAsync(string documentType, CancellationToken cancellationToken = default);

    Task<ExportTemplateUploadResult> UploadAsync(string documentType, string fileName, byte[] content, CancellationToken cancellationToken = default);

    /// <summary>Legacy "Open" - returns the stored template file, or null when it no longer exists.</summary>
    Task<ExportTemplateContent?> DownloadAsync(Guid fileId, CancellationToken cancellationToken = default);

    Task<bool> SelectAsync(Guid fileId, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid fileId, CancellationToken cancellationToken = default);
}

public sealed record ExportTemplateUploadResult(bool Success, string? ErrorMessage, UploadedTemplate? Template);

public sealed record ExportTemplateContent(string FileName, string ContentType, byte[] Content);

public sealed partial class ExportTemplateService(
    IUploadedTemplateRepository repository,
    ITemplateStorageService storage,
    IOptions<TemplateStorageOptions> options) : IExportTemplateService
{
    public const string DocxContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    // Legacy ExportBase error text - reused verbatim.
    private const string FileNotFound = "File not found";
    private const string FileTooLarge = "Maximum file size is 4MB";
    private const string WrongExtension = "The file does not have a .Doc or .Docx extension";
    private const string DuplicateName = "A file with this name already exists";
    private const string SaveFailed = "File could not be saved";

    private readonly TemplateStorageOptions _options = options.Value;

    public async Task<IReadOnlyList<UploadedTemplate>> GetTemplatesAsync(string documentType, CancellationToken cancellationToken = default)
    {
        if (!ExportDocumentTypes.TryResolve(documentType, out var storageName, out _))
        {
            return [];
        }

        var templates = await repository.GetByDocumentTypeAsync(storageName, cancellationToken);

        // spgUploadedFilesByDocumentType does not select fldDocumentType back.
        foreach (var template in templates)
        {
            template.DocumentType = storageName;
        }

        return [.. templates.OrderBy(t => t.Filename, StringComparer.OrdinalIgnoreCase)];
    }

    public async Task<UploadedTemplate?> GetActiveTemplateAsync(string documentType, CancellationToken cancellationToken = default) =>
        (await GetTemplatesAsync(documentType, cancellationToken)).FirstOrDefault(t => t.Selected);

    public async Task<ExportTemplateUploadResult> UploadAsync(string documentType, string fileName, byte[] content, CancellationToken cancellationToken = default)
    {
        if (!ExportDocumentTypes.TryResolve(documentType, out var storageName, out _))
        {
            return new ExportTemplateUploadResult(false, FileNotFound, null);
        }

        if (content.Length == 0 || string.IsNullOrWhiteSpace(fileName))
        {
            return new ExportTemplateUploadResult(false, FileNotFound, null);
        }

        if (content.Length > _options.MaxUploadBytes)
        {
            return new ExportTemplateUploadResult(false, FileTooLarge, null);
        }

        // Strip any client-supplied path before validating - a browser may send a full path.
        var safeName = Path.GetFileName(fileName);
        if (!TemplateFileNamePattern().IsMatch(safeName))
        {
            return new ExportTemplateUploadResult(false, WrongExtension, null);
        }

        // Legacy kept every template in one folder, so a name must be unique across all four types.
        var existing = await repository.GetAllAsync(cancellationToken);
        if (existing.Any(t => string.Equals(t.Filename, safeName, StringComparison.OrdinalIgnoreCase)))
        {
            return new ExportTemplateUploadResult(false, DuplicateName, null);
        }

        var template = new UploadedTemplate
        {
            FileId = Guid.NewGuid(),
            Filename = safeName,
            UploadedDate = DateTime.Now,
            DocumentType = storageName,
            Selected = false
        };

        try
        {
            await storage.SaveAsync(StorageKeyFor(template), content, DocxContentType, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new ExportTemplateUploadResult(false, SaveFailed, null);
        }

        await repository.CreateAsync(template, cancellationToken);
        return new ExportTemplateUploadResult(true, null, template);
    }

    public async Task<ExportTemplateContent?> DownloadAsync(Guid fileId, CancellationToken cancellationToken = default)
    {
        var template = await FindAsync(fileId, cancellationToken);
        if (template is null)
        {
            return null;
        }

        var content = await storage.GetAsync(StorageKeyFor(template), cancellationToken);
        return content is null ? null : new ExportTemplateContent(template.Filename, DocxContentType, content);
    }

    public async Task<bool> SelectAsync(Guid fileId, CancellationToken cancellationToken = default)
    {
        var template = await FindAsync(fileId, cancellationToken);
        if (template is null)
        {
            return false;
        }

        // Legacy clears every flag then sets the one - only one template per type can be active.
        var siblings = await repository.GetByDocumentTypeAsync(template.DocumentType, cancellationToken);
        foreach (var sibling in siblings.Where(s => s.Selected && s.FileId != fileId))
        {
            await repository.SetSelectedAsync(sibling.FileId, false, cancellationToken);
        }

        await repository.SetSelectedAsync(fileId, true, cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid fileId, CancellationToken cancellationToken = default)
    {
        var template = await FindAsync(fileId, cancellationToken);
        if (template is null)
        {
            return false;
        }

        await storage.DeleteAsync(StorageKeyFor(template), cancellationToken);
        await repository.DeleteAsync(fileId, cancellationToken);
        return true;
    }

    private async Task<UploadedTemplate?> FindAsync(Guid fileId, CancellationToken cancellationToken) =>
        (await repository.GetAllAsync(cancellationToken)).FirstOrDefault(t => t.FileId == fileId);

    // The legacy table has no storage-key column, so the key is derived and therefore stable.
    private string StorageKeyFor(UploadedTemplate template)
    {
        var prefix = string.IsNullOrWhiteSpace(_options.Prefix) ? string.Empty : _options.Prefix.Trim('/') + "/";
        return $"{prefix}{template.DocumentType}/{template.FileId}{Path.GetExtension(template.Filename).ToLowerInvariant()}";
    }

    [GeneratedRegex(@"^\w[\w\s]*\.(doc|docx)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TemplateFileNamePattern();
}
