namespace PTL.Core.Storage;

/// <summary>
/// Single shared S3 configuration model for every S3-backed feature (export templates, invoice
/// archives) - one bucket/region, one prefix per feature. Do not add a second, independent
/// bucket/region setting for a new feature; add a new nested prefix section here instead.
/// </summary>
public sealed class S3Options
{
    public const string SectionName = "S3";

    /// <summary>"S3" or "InMemory". InMemory lets a feature run before a bucket is provisioned.</summary>
    public string Provider { get; set; } = "S3";

    public string Region { get; set; } = string.Empty;

    public string BucketName { get; set; } = string.Empty;

    public S3TemplatesOptions Templates { get; set; } = new();

    public S3InvoicesOptions Invoices { get; set; } = new();
}

public sealed class S3TemplatesOptions
{
    public string Prefix { get; set; } = "templates";

    /// <summary>Legacy ExportBase rejected anything over 4MB.</summary>
    public long MaxUploadBytes { get; set; } = 4 * 1024 * 1024;
}

public sealed class S3InvoicesOptions
{
    public string Prefix { get; set; } = "invoices";
}
