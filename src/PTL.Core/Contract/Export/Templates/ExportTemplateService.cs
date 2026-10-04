using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
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
    IOptions<TemplateStorageOptions> options,
    ILogger<ExportTemplateService> logger) : IExportTemplateService
{
    public const string DocxContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    // Legacy ExportBase error text - reused verbatim.
    private const string FileNotFound = "File not found";
    private const string FileTooLarge = "Maximum file size is 4MB";
    private const string WrongExtension = "The file does not have a .Doc or .Docx extension";
    private const string DuplicateName = "A file with this name already exists";
    private const string SaveFailed = "File could not be saved";

    private readonly TemplateStorageOptions _options = options.Value;

    // Source-generated rather than LoggerMessage.Define, which caps at six message parameters.
    // Timings are pre-formatted into one value to keep the parameter list readable.
    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "ExportTemplateUpload failed for {DocumentType}/{FileName}: {Timings} ExceptionType={ExceptionType}")]
    private static partial void LogUploadFailedMessage(ILogger logger, string documentType, string fileName, string timings, string exceptionType, Exception exception);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "ExportTemplateUpload timing for {DocumentType}/{FileName}: {Timings}")]
    private static partial void LogUploadTimingMessage(ILogger logger, string documentType, string fileName, string timings);

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
        var total = Stopwatch.StartNew();

        var validation = Stopwatch.StartNew();
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
        validation.Stop();

        // Legacy kept every template in one folder, so a name must be unique across all four types.
        var duplicateCheck = Stopwatch.StartNew();
        var existing = await repository.GetAllAsync(cancellationToken);
        duplicateCheck.Stop();
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

        var s3Upload = Stopwatch.StartNew();
        try
        {
            await storage.SaveAsync(StorageKeyFor(template), content, DocxContentType, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            s3Upload.Stop();
            total.Stop();
            LogUploadFailedMessage(
                logger,
                storageName,
                safeName,
                string.Create(
                    System.Globalization.CultureInfo.InvariantCulture,
                    $"Validation={validation.ElapsedMilliseconds}ms DuplicateCheck={duplicateCheck.ElapsedMilliseconds}ms S3Upload={s3Upload.ElapsedMilliseconds}ms Total={total.ElapsedMilliseconds}ms"),
                ex.GetType().Name,
                ex);
            return new ExportTemplateUploadResult(false, SaveFailed, null);
        }
        s3Upload.Stop();

        var metadataPersist = Stopwatch.StartNew();
        await repository.CreateAsync(template, cancellationToken);
        metadataPersist.Stop();

        total.Stop();
        LogUploadTimingMessage(
            logger,
            storageName,
            safeName,
            string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"Validation={validation.ElapsedMilliseconds}ms DuplicateCheck={duplicateCheck.ElapsedMilliseconds}ms S3Upload={s3Upload.ElapsedMilliseconds}ms MetadataPersist={metadataPersist.ElapsedMilliseconds}ms Total={total.ElapsedMilliseconds}ms"));

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
    // Bucket layout must follow the required S3 folders under templates/ without altering the
    // database metadata values already persisted by the legacy model.
    private string StorageKeyFor(UploadedTemplate template)
    {
        var prefix = string.IsNullOrWhiteSpace(_options.Prefix) ? string.Empty : _options.Prefix.Trim('/');
        var folder = DocumentTypeFolderName(template.DocumentType);
        var extension = Path.GetExtension(template.Filename).ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(prefix))
        {
            return $"{folder}/{template.FileId}{extension}";
        }

        return $"{prefix}/{folder}/{template.FileId}{extension}";
    }

    private static string DocumentTypeFolderName(string documentType) => documentType switch
    {
        ExportDocumentTypes.Contracts => "contracts",
        ExportDocumentTypes.JobSheets => "job-sheets",
        ExportDocumentTypes.RenewalLetters => "renewal-letters",
        ExportDocumentTypes.AddressConfirmationLetters => "address-confirmation",
        _ => documentType.Trim('/').ToLowerInvariant()
    };

    [GeneratedRegex(@"^\w[\w\s]*\.(doc|docx)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TemplateFileNamePattern();
}
