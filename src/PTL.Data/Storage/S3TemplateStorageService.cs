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

    private static readonly Action<ILogger, string, long, long, Exception?> LogSaveTimingMessage =
        LoggerMessage.Define<string, long, long>(
            LogLevel.Information,
            new EventId(1, nameof(LogSaveTimingMessage)),
            "S3TemplateStorage timing for {Key}: PrefixCheck={PrefixCheckMs}ms Upload={UploadMs}ms");

    private static readonly Action<ILogger, string, string, long, long, string, Exception?> LogSaveFailedMessage =
        LoggerMessage.Define<string, string, long, long, string>(
            LogLevel.Error,
            new EventId(2, nameof(LogSaveFailedMessage)),
            "Unable to upload template '{Key}' to bucket '{Bucket}'. PrefixCheck={PrefixCheckMs}ms Upload={UploadMs}ms ExceptionType={ExceptionType}");

    private static readonly Action<ILogger, string, string, Exception?> LogReadFailedMessage =
        LoggerMessage.Define<string, string>(
            LogLevel.Warning,
            new EventId(3, nameof(LogReadFailedMessage)),
            "Unable to read template '{Key}' from bucket '{Bucket}'.");

    private static readonly Action<ILogger, string, string, Exception?> LogDeleteFailedMessage =
        LoggerMessage.Define<string, string>(
            LogLevel.Warning,
            new EventId(4, nameof(LogDeleteFailedMessage)),
            "Unable to delete template '{Key}' from bucket '{Bucket}'.");

    private static readonly Action<ILogger, string, string, Exception?> LogListFailedMessage =
        LoggerMessage.Define<string, string>(
            LogLevel.Warning,
            new EventId(5, nameof(LogListFailedMessage)),
            "Unable to list objects in bucket '{Bucket}' using prefix '{Prefix}'.");

    private static readonly Action<ILogger, string, long, Exception?> LogFolderMarkerExistsMessage =
        LoggerMessage.Define<string, long>(
            LogLevel.Information,
            new EventId(6, nameof(LogFolderMarkerExistsMessage)),
            "S3 prefix check for '{FolderMarker}': HeadCheck={HeadCheckMs}ms (marker already existed)");

    private static readonly Action<ILogger, string, long, long, Exception?> LogFolderMarkerCreatedMessage =
        LoggerMessage.Define<string, long, long>(
            LogLevel.Information,
            new EventId(7, nameof(LogFolderMarkerCreatedMessage)),
            "S3 prefix creation for '{FolderMarker}': HeadCheck={HeadCheckMs}ms MarkerCreate={MarkerCreateMs}ms");

    private static readonly Action<ILogger, string, string, long, Exception?> LogFolderMarkerFailedMessage =
        LoggerMessage.Define<string, string, long>(
            LogLevel.Warning,
            new EventId(8, nameof(LogFolderMarkerFailedMessage)),
            "Unable to ensure folder marker '{FolderMarker}' exists in bucket '{Bucket}'. HeadCheck={HeadCheckMs}ms");

    private static readonly Action<ILogger, string, string, long, string, Exception?> LogFolderMarkerUnreachableMessage =
        LoggerMessage.Define<string, string, long, string>(
            LogLevel.Warning,
            new EventId(9, nameof(LogFolderMarkerUnreachableMessage)),
            "Unable to reach S3 while checking folder marker '{FolderMarker}' in bucket '{Bucket}'. HeadCheck={HeadCheckMs}ms ExceptionType={ExceptionType}");

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

            LogSaveTimingMessage(logger, storageKey, prefixCheck.ElapsedMilliseconds, upload.ElapsedMilliseconds, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            prefixCheck.Stop();
            upload.Stop();
            LogSaveFailedMessage(logger, storageKey, BucketName(), prefixCheck.ElapsedMilliseconds, upload.ElapsedMilliseconds, ex.GetType().Name, ex);
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
            LogReadFailedMessage(logger, storageKey, BucketName(), ex);
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
            LogDeleteFailedMessage(logger, storageKey, BucketName(), ex);
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
            LogListFailedMessage(logger, BucketName(), listPrefix, ex);
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
            LogFolderMarkerExistsMessage(logger, folderMarkerKey, headCheck.ElapsedMilliseconds, null);
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
            LogFolderMarkerCreatedMessage(logger, folderMarkerKey, headCheck.ElapsedMilliseconds, markerCreate.ElapsedMilliseconds, null);
        }
        catch (AmazonS3Exception ex)
        {
            headCheck.Stop();
            LogFolderMarkerFailedMessage(logger, folderMarkerKey, bucketName, headCheck.ElapsedMilliseconds, ex);
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            headCheck.Stop();
            LogFolderMarkerUnreachableMessage(logger, folderMarkerKey, bucketName, headCheck.ElapsedMilliseconds, ex.GetType().Name, ex);
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
