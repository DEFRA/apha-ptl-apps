using System.Diagnostics;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PTL.Core.Contract.Export.Templates;

namespace PTL.Data.Storage;

/// <summary>
/// The only type in the solution that touches the AWS SDK. Credentials are resolved by the AWS SDK
/// default chain (SSO profile locally, instance role in the deployed environment); no access keys are
/// embedded in the application or configuration.
/// </summary>
public sealed class S3TemplateStorageService(IAmazonS3 s3Client, IOptions<TemplateStorageOptions> options, ILogger<S3TemplateStorageService> logger) : ITemplateStorageService
{
    private readonly TemplateStorageOptions _options = options.Value;

    public async Task SaveAsync(string storageKey, byte[] content, string contentType, CancellationToken cancellationToken = default)
    {
        var prefixCheck = Stopwatch.StartNew();
        var upload = new Stopwatch();
        try
        {
            await EnsureFolderExistsAsync(storageKey, cancellationToken);
            prefixCheck.Stop();

            upload.Start();
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
            upload.Stop();

            logger.LogInformation(
                "S3TemplateStorage timing for {Key}: PrefixCheck={PrefixCheckMs}ms Upload={UploadMs}ms",
                storageKey, prefixCheck.ElapsedMilliseconds, upload.ElapsedMilliseconds);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            prefixCheck.Stop();
            upload.Stop();
            logger.LogError(
                ex,
                "Unable to upload template '{Key}' to bucket '{Bucket}'. PrefixCheck={PrefixCheckMs}ms Upload={UploadMs}ms ExceptionType={ExceptionType}",
                storageKey, BucketName(), prefixCheck.ElapsedMilliseconds, upload.ElapsedMilliseconds, ex.GetType().Name);
            throw;
        }
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
        catch (AmazonS3Exception ex)
        {
            logger.LogWarning(ex, "Unable to read template '{Key}' from bucket '{Bucket}'.", storageKey, BucketName());
            return null;
        }
    }

    public async Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        try
        {
            await s3Client.DeleteObjectAsync(BucketName(), storageKey, cancellationToken);
        }
        catch (AmazonS3Exception ex)
        {
            logger.LogWarning(ex, "Unable to delete template '{Key}' from bucket '{Bucket}'.", storageKey, BucketName());
            throw;
        }
    }

    public async Task<IReadOnlyList<string>> ListAsync(string? prefix = null, CancellationToken cancellationToken = default)
    {
        var listPrefix = BuildListPrefix(prefix);

        try
        {
            var response = await s3Client.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = BucketName(),
                Prefix = listPrefix,
                MaxKeys = 1000
            }, cancellationToken);

            return response.S3Objects
                .Select(item => item.Key)
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (AmazonS3Exception ex)
        {
            logger.LogWarning(ex, "Unable to list objects in bucket '{Bucket}' using prefix '{Prefix}'.", BucketName(), listPrefix);
            return [];
        }
    }

    private async Task EnsureFolderExistsAsync(string storageKey, CancellationToken cancellationToken)
    {
        var folderPrefix = GetFolderPrefix(storageKey);
        if (string.IsNullOrWhiteSpace(folderPrefix))
        {
            return;
        }

        var folderMarkerKey = folderPrefix.TrimEnd('/') + "/.folder-marker";
        var bucketName = BucketName();

        var headCheck = Stopwatch.StartNew();
        try
        {
            await s3Client.GetObjectMetadataAsync(bucketName, folderMarkerKey, cancellationToken);
            headCheck.Stop();
            logger.LogInformation("S3 prefix check for '{FolderMarker}': HeadCheck={HeadCheckMs}ms (marker already existed)", folderMarkerKey, headCheck.ElapsedMilliseconds);
            return;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            headCheck.Stop();
            var markerCreate = Stopwatch.StartNew();
            await s3Client.PutObjectAsync(
                new PutObjectRequest
                {
                    BucketName = bucketName,
                    Key = folderMarkerKey,
                    InputStream = new MemoryStream(Array.Empty<byte>()),
                    ContentType = "application/octet-stream"
                },
                cancellationToken);
            markerCreate.Stop();
            logger.LogInformation(
                "S3 prefix creation for '{FolderMarker}': HeadCheck={HeadCheckMs}ms MarkerCreate={MarkerCreateMs}ms",
                folderMarkerKey, headCheck.ElapsedMilliseconds, markerCreate.ElapsedMilliseconds);
        }
        catch (AmazonS3Exception ex)
        {
            headCheck.Stop();
            logger.LogWarning(
                ex,
                "Unable to ensure folder marker '{FolderMarker}' exists in bucket '{Bucket}'. HeadCheck={HeadCheckMs}ms",
                folderMarkerKey, bucketName, headCheck.ElapsedMilliseconds);
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            headCheck.Stop();
            logger.LogWarning(
                ex,
                "Unable to reach S3 while checking folder marker '{FolderMarker}' in bucket '{Bucket}'. HeadCheck={HeadCheckMs}ms ExceptionType={ExceptionType}",
                folderMarkerKey, bucketName, headCheck.ElapsedMilliseconds, ex.GetType().Name);
            throw;
        }
    }

    private static string GetFolderPrefix(string storageKey)
    {
        var trimmed = storageKey.Trim('/');
        var lastSlash = trimmed.LastIndexOf('/');
        if (lastSlash <= 0)
        {
            return string.Empty;
        }

        return trimmed[..lastSlash];
    }

    private string BuildListPrefix(string? prefix)
    {
        var configuredPrefix = string.IsNullOrWhiteSpace(_options.Prefix)
            ? string.Empty
            : _options.Prefix.Trim('/');
        var requestedPrefix = string.IsNullOrWhiteSpace(prefix)
            ? configuredPrefix
            : prefix.Trim('/');

        return string.IsNullOrWhiteSpace(requestedPrefix) ? string.Empty : requestedPrefix + "/";
    }

    private string BucketName() =>
        string.IsNullOrWhiteSpace(_options.BucketName)
            ? throw new InvalidOperationException($"Configuration value '{TemplateStorageOptions.SectionName}:BucketName' is required.")
            : _options.BucketName;
}
