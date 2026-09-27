using PTL.Core.Contract.Document;

namespace PTL.Data.Contract.Document;

/// <summary>
/// Resolves a document type to its committed <c>.docx</c> template. Only <c>.docx</c> is accepted -
/// binary <c>.doc</c> cannot be merged without a commercial library, so legacy templates must be
/// converted and committed before they resolve here.
/// </summary>
public sealed class FileTemplateRepository(ITemplateLoader templateLoader) : ITemplateRepository
{
    private const string DocxExtension = ".docx";

    private readonly string _rootDirectory = templateLoader.GetTemplatesRoot();

    public Task<DocumentTemplate?> GetAsync(string documentType, string? templateName = null, CancellationToken cancellationToken = default)
    {
        var templateKey = string.IsNullOrWhiteSpace(templateName)
            ? ContractDocumentTypes.GetTemplateKey(documentType)
            : Path.GetFileNameWithoutExtension(templateName);

        if (string.IsNullOrWhiteSpace(templateKey))
        {
            return Task.FromResult<DocumentTemplate?>(null);
        }

        var template = EnumerateTemplates(documentType)
            .FirstOrDefault(t => string.Equals(t.TemplateKey, templateKey, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(template);
    }

    public Task<IReadOnlyList<DocumentTemplate>> GetByDocumentTypeAsync(string documentType, CancellationToken cancellationToken = default)
    {
        var templateKey = ContractDocumentTypes.GetTemplateKey(documentType);
        var templates = EnumerateTemplates(documentType)
            .Where(t => templateKey is null || t.TemplateKey.StartsWith(templateKey, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return Task.FromResult<IReadOnlyList<DocumentTemplate>>(templates);
    }

    private IEnumerable<DocumentTemplate> EnumerateTemplates(string documentType)
    {
        if (!Directory.Exists(_rootDirectory))
        {
            return [];
        }

        return Directory.EnumerateFiles(_rootDirectory, "*" + DocxExtension, SearchOption.AllDirectories)
            .Select(path => new DocumentTemplate(
                Path.GetFileNameWithoutExtension(path),
                documentType,
                Path.GetFileNameWithoutExtension(path),
                path));
    }
}
