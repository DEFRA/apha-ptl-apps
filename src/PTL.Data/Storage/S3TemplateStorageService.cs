using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using PTL.Core.Contract.Export.Templates;

namespace PTL.Data.Storage;

/// <summary>
/// The only type in the solution that touches the AWS SDK. The client is resolved lazily so the
/// application starts, and every test runs, without credentials or bucket access.
/// </summary>
public sealed class S3TemplateStorageService(IAmazonS3 s3Client, IOptions<TemplateStorageOptions> options) : ITemplateStorageService
{
    private readonly TemplateStorageOptions _options = options.Value;

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

    public async Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default) =>
        await s3Client.DeleteObjectAsync(BucketName(), storageKey, cancellationToken);

    private string BucketName() =>
        string.IsNullOrWhiteSpace(_options.BucketName)
            ? throw new InvalidOperationException($"Configuration value '{TemplateStorageOptions.SectionName}:BucketName' is required.")
            : _options.BucketName;
}
