using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using PTL.Core.Invoice;

namespace PTL.Data.Storage;

/// <summary>
/// S3-backed storage for generated invoice CSVs, replacing legacy's local-filesystem
/// <c>InvoiceArchivePath</c> (docs/migration/invoice-migration.md). Mirrors
/// PTL.Data.Storage.S3TemplateStorageService's lazy-client-resolution pattern exactly.
/// </summary>
public sealed class S3InvoiceStorageService(IAmazonS3 s3Client, IOptions<InvoiceStorageOptions> options) : IInvoiceStorageService
{
    private readonly InvoiceStorageOptions _options = options.Value;

    public async Task SaveAsync(string storageKey, byte[] content, string contentType, CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream(content, writable: false);

        await s3Client.PutObjectAsync(
            new PutObjectRequest
            {
                BucketName = BucketName(),
                Key = storageKey,
                InputStream = stream,
                ContentType = contentType
            },
            cancellationToken);
    }

    public async Task<byte[]?> GetAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await s3Client.GetObjectAsync(BucketName(), storageKey, cancellationToken);
            using var buffer = new MemoryStream();
            await response.ResponseStream.CopyToAsync(buffer, cancellationToken);
            return buffer.ToArray();
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private string BucketName() =>
        string.IsNullOrWhiteSpace(_options.BucketName)
            ? throw new InvalidOperationException($"Configuration value '{InvoiceStorageOptions.SectionName}:BucketName' is required.")
            : _options.BucketName;
}
