namespace PTL.Core.Invoice;

public sealed class InvoiceStorageOptions
{
    public const string SectionName = "InvoiceStorage";

    /// <summary>"S3" or "InMemory". InMemory lets the feature run before a bucket is provisioned.</summary>
    public string Provider { get; set; } = "S3";

    public string BucketName { get; set; } = string.Empty;

    public string Region { get; set; } = string.Empty;

    public string Prefix { get; set; } = "invoices";
}
