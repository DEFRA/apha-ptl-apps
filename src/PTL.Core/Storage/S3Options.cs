namespace PTL.Core.Storage;

/// <summary>
/// Single shared S3 configuration model for every S3-backed feature (export templates, invoice
/// archives) - one bucket, one prefix per feature. Do not add a second, independent bucket setting
/// for a new feature; add a new nested prefix section here instead.
/// </summary>
public sealed class S3Options
{
    public const string SectionName = "S3";

    /// <summary>
    /// AWS region never varies by deployed environment, so it is not configuration - every real
    /// environment's bucket lives in this region. A local developer who needs a different region
    /// for a personal test bucket can still override it via the AWS SDK's own default credential/
    /// region chain (e.g. the AWS_REGION environment variable or a local profile).
    /// </summary>
    public const string DefaultRegion = "eu-west-2";

    public string Bucket { get; set; } = string.Empty;

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
