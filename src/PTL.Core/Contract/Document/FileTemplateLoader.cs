namespace PTL.Core.Contract.Document;

public interface ITemplateLoader
{
    string GetTemplatesRoot();
}

/// <summary>
/// Resolves the folder holding the committed <c>.docx</c> merge templates. Templates ship as build
/// content with the hosting web application; nothing is discovered or copied at runtime.
/// </summary>
public sealed class FileTemplateLoader : ITemplateLoader
{
    private readonly string _rootPath;

    public FileTemplateLoader(string? rootPath = null)
    {
        _rootPath = string.IsNullOrWhiteSpace(rootPath)
            ? Path.Combine(AppContext.BaseDirectory, "Documents", "Templates")
            : Path.GetFullPath(rootPath, AppContext.BaseDirectory);
    }

    public string GetTemplatesRoot() => _rootPath;
}
