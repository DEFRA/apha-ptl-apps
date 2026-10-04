namespace PTL.Core.Contract.Export.Templates;

public sealed class TemplateStorageOptions
{
    public const string SectionName = "TemplateStorage";

    /// <summary>"S3" or "InMemory". InMemory lets the feature run before a bucket is provisioned.</summary>
    public string Provider { get; set; } = "S3";

    public string BucketName { get; set; } = string.Empty;

    public string Region { get; set; } = string.Empty;

    public string Prefix { get; set; } = "templates";

    /// <summary>Alias used by S3-focused configuration and keeps environment config readable.</summary>
    public string TemplatesPrefix
    {
        get => Prefix;
        set => Prefix = value;
    }

    /// <summary>Legacy ExportBase rejected anything over 4MB.</summary>
    public long MaxUploadBytes { get; set; } = 4 * 1024 * 1024;
}
