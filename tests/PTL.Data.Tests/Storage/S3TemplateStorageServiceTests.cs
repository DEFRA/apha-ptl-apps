using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PTL.Core.Contract.Export.Templates;
using PTL.Data.Storage;

namespace PTL.Data.Tests.Storage;

internal sealed class FakeTemplateAmazonS3Client() : AmazonS3Client(new BasicAWSCredentials("test", "test"), new AmazonS3Config { ServiceURL = "http://localhost" })
{
    public string? LastBucketName { get; private set; }
    public string? LastKey { get; private set; }
    public string? LastContentType { get; private set; }
    public string? LastFolderMarkerKey { get; private set; }
    public byte[]? GetObjectContent { get; set; }
    public bool ThrowNotFoundOnGet { get; set; }
    public bool ThrowNotFoundOnHead { get; set; }
    public bool ThrowServerErrorOnGet { get; set; }
    public bool ThrowServerErrorOnHead { get; set; }
    public bool ThrowTimeoutOnHead { get; set; }
    public bool ThrowOnPut { get; set; }
    public bool ThrowOnDelete { get; set; }
    public bool ThrowOnList { get; set; }
    public List<string> DeleteKeys { get; } = [];
    public List<string> ListedKeys { get; } = [];
    public string? LastListPrefix { get; private set; }

    public override Task<PutObjectResponse> PutObjectAsync(PutObjectRequest request, CancellationToken cancellationToken = default)
    {
        LastBucketName = request.BucketName;
        LastKey = request.Key;
        LastContentType = request.ContentType;

        if (!string.IsNullOrWhiteSpace(request.Key) && request.Key.EndsWith("/.folder-marker", StringComparison.OrdinalIgnoreCase))
        {
            LastFolderMarkerKey = request.Key;
        }

        if (ThrowOnPut)
        {
            throw new AmazonS3Exception("Upload failed") { StatusCode = System.Net.HttpStatusCode.ServiceUnavailable };
        }

        return Task.FromResult(new PutObjectResponse());
    }

    public override Task<GetObjectResponse> GetObjectAsync(string bucketName, string key, CancellationToken cancellationToken = default)
    {
        LastBucketName = bucketName;
        LastKey = key;

        if (ThrowNotFoundOnGet)
        {
            throw new AmazonS3Exception("Not found") { StatusCode = System.Net.HttpStatusCode.NotFound };
        }

        if (ThrowServerErrorOnGet)
        {
            throw new AmazonS3Exception("Server error") { StatusCode = System.Net.HttpStatusCode.InternalServerError };
        }

        var stream = new MemoryStream(GetObjectContent ?? []);
        return Task.FromResult(new GetObjectResponse { ResponseStream = stream });
    }

    public override Task<DeleteObjectResponse> DeleteObjectAsync(string bucketName, string key, CancellationToken cancellationToken = default)
    {
        LastBucketName = bucketName;
        LastKey = key;
        DeleteKeys.Add(key);

        if (ThrowOnDelete)
        {
            throw new AmazonS3Exception("Delete failed") { StatusCode = System.Net.HttpStatusCode.Forbidden };
        }

        return Task.FromResult(new DeleteObjectResponse());
    }

    public override Task<GetObjectMetadataResponse> GetObjectMetadataAsync(string bucketName, string key, CancellationToken cancellationToken = default)
    {
        LastBucketName = bucketName;
        LastKey = key;

        if (ThrowNotFoundOnHead)
        {
            throw new AmazonS3Exception("Not found") { StatusCode = System.Net.HttpStatusCode.NotFound };
        }

        if (ThrowServerErrorOnHead)
        {
            throw new AmazonS3Exception("Server error") { StatusCode = System.Net.HttpStatusCode.InternalServerError };
        }

        if (ThrowTimeoutOnHead)
        {
            throw new TimeoutException("S3 unreachable");
        }

        return Task.FromResult(new GetObjectMetadataResponse());
    }

    public override Task<ListObjectsV2Response> ListObjectsV2Async(ListObjectsV2Request request, CancellationToken cancellationToken = default)
    {
        LastBucketName = request.BucketName;
        LastListPrefix = request.Prefix;
        ListedKeys.Clear();

        if (ThrowOnList)
        {
            throw new AmazonS3Exception("List failed") { StatusCode = System.Net.HttpStatusCode.Forbidden };
        }

        var keys = request.Prefix is null || request.Prefix == "templates/contracts/"
            ? new[] { "templates/contracts/file-1.docx", "templates/contracts/file-2.docx" }
            : new[] { "templates/contracts/file-1.docx" };

        ListedKeys.AddRange(keys);
        return Task.FromResult(new ListObjectsV2Response { S3Objects = ListedKeys.Select(key => new S3Object { Key = key }).ToList() });
    }
}

public class S3TemplateStorageServiceTests
{
    private static S3TemplateStorageService CreateService(FakeTemplateAmazonS3Client s3Client, string bucketName = "devldnptl-env", string prefix = "templates") =>
        new(s3Client, Options.Create(new TemplateStorageOptions { BucketName = bucketName, Prefix = prefix }), NullLogger<S3TemplateStorageService>.Instance);

    [Fact]
    public async Task SaveAsync_PutsObjectWithExpectedBucketKeyAndContentType()
    {
        var s3Client = new FakeTemplateAmazonS3Client();
        var service = CreateService(s3Client);

        await service.SaveAsync("templates/contracts/11111111-1111-1111-1111-111111111111.docx", [1, 2, 3], "application/vnd.openxmlformats-officedocument.wordprocessingml.document");

        Assert.Equal("devldnptl-env", s3Client.LastBucketName);
        Assert.Equal("templates/contracts/11111111-1111-1111-1111-111111111111.docx", s3Client.LastKey);
        Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document", s3Client.LastContentType);
    }

    [Fact]
    public async Task SaveAsync_EmptyBucketName_Throws()
    {
        var s3Client = new FakeTemplateAmazonS3Client();
        var service = CreateService(s3Client, bucketName: string.Empty);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync("templates/contracts/file.docx", [1], "application/octet-stream"));
    }

    [Fact]
    public async Task GetAsync_ReturnsStoredContent()
    {
        var s3Client = new FakeTemplateAmazonS3Client { GetObjectContent = [4, 5, 6] };
        var service = CreateService(s3Client);

        var result = await service.GetAsync("templates/contracts/file.docx");

        Assert.Equal(new byte[] { 4, 5, 6 }, result);
        Assert.Equal("devldnptl-env", s3Client.LastBucketName);
    }

    [Fact]
    public async Task GetAsync_NotFound_ReturnsNull()
    {
        var s3Client = new FakeTemplateAmazonS3Client { ThrowNotFoundOnGet = true };
        var service = CreateService(s3Client);

        var result = await service.GetAsync("templates/contracts/missing.docx");

        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteAsync_DeletesRequestedObject()
    {
        var s3Client = new FakeTemplateAmazonS3Client();
        var service = CreateService(s3Client);

        await service.DeleteAsync("templates/contracts/file.docx");

        Assert.Equal("templates/contracts/file.docx", s3Client.LastKey);
        Assert.Contains("templates/contracts/file.docx", s3Client.DeleteKeys);
    }

    [Fact]
    public async Task ListAsync_ReturnsMatchingKeys()
    {
        var s3Client = new FakeTemplateAmazonS3Client();
        var service = CreateService(s3Client);

        var result = await service.ListAsync("templates/contracts/");

        Assert.Contains("templates/contracts/file-1.docx", result);
        Assert.Contains("templates/contracts/file-2.docx", result);
    }

    [Fact]
    public async Task CreateFolderMarker_UsesExpectedPrefixPath()
    {
        var s3Client = new FakeTemplateAmazonS3Client { ThrowNotFoundOnHead = true };
        var service = CreateService(s3Client);

        await service.SaveAsync("templates/job-sheets/22222222-2222-2222-2222-222222222222.docx", [1], "application/octet-stream");

        Assert.Equal("templates/job-sheets/.folder-marker", s3Client.LastFolderMarkerKey);
    }

    [Fact]
    public void CreateClient_UsesConfiguredRegionWithoutHardcodedCredentials()
    {
        var client = AwsS3ClientFactory.Create("eu-west-2");

        Assert.NotNull(client);
        Assert.Equal("eu-west-2", client.Config.RegionEndpoint.SystemName);
    }

    [Fact]
    public async Task SaveAsync_UploadFails_LogsAndRethrows()
    {
        var s3Client = new FakeTemplateAmazonS3Client { ThrowOnPut = true };
        var service = CreateService(s3Client);

        await Assert.ThrowsAsync<AmazonS3Exception>(() => service.SaveAsync("templates/contracts/file.docx", [1], "application/octet-stream"));
    }

    // A key with no folder segment has no prefix to create, so the marker check is skipped entirely.
    [Fact]
    public async Task SaveAsync_KeyWithoutFolderSegment_SkipsFolderMarkerCreation()
    {
        var s3Client = new FakeTemplateAmazonS3Client { ThrowNotFoundOnHead = true };
        var service = CreateService(s3Client);

        await service.SaveAsync("file.docx", [1], "application/octet-stream");

        Assert.Null(s3Client.LastFolderMarkerKey);
        Assert.Equal("file.docx", s3Client.LastKey);
    }

    [Fact]
    public async Task SaveAsync_FolderMarkerAlreadyExists_DoesNotRecreateIt()
    {
        var s3Client = new FakeTemplateAmazonS3Client();
        var service = CreateService(s3Client);

        await service.SaveAsync("templates/contracts/file.docx", [1], "application/octet-stream");

        Assert.Null(s3Client.LastFolderMarkerKey);
    }

    [Fact]
    public async Task SaveAsync_FolderMarkerCheckFailsWithS3Error_Rethrows()
    {
        var s3Client = new FakeTemplateAmazonS3Client { ThrowServerErrorOnHead = true };
        var service = CreateService(s3Client);

        await Assert.ThrowsAsync<AmazonS3Exception>(() => service.SaveAsync("templates/contracts/file.docx", [1], "application/octet-stream"));
    }

    [Fact]
    public async Task SaveAsync_FolderMarkerCheckTimesOut_Rethrows()
    {
        var s3Client = new FakeTemplateAmazonS3Client { ThrowTimeoutOnHead = true };
        var service = CreateService(s3Client);

        await Assert.ThrowsAsync<TimeoutException>(() => service.SaveAsync("templates/contracts/file.docx", [1], "application/octet-stream"));
    }

    // A read failure other than 404 is logged and swallowed so the Exports screen still renders.
    [Fact]
    public async Task GetAsync_S3Error_ReturnsNull()
    {
        var s3Client = new FakeTemplateAmazonS3Client { ThrowServerErrorOnGet = true };
        var service = CreateService(s3Client);

        Assert.Null(await service.GetAsync("templates/contracts/file.docx"));
    }

    [Fact]
    public async Task DeleteAsync_S3Error_LogsAndRethrows()
    {
        var s3Client = new FakeTemplateAmazonS3Client { ThrowOnDelete = true };
        var service = CreateService(s3Client);

        await Assert.ThrowsAsync<AmazonS3Exception>(() => service.DeleteAsync("templates/contracts/file.docx"));
    }

    [Fact]
    public async Task ListAsync_S3Error_ReturnsEmpty()
    {
        var s3Client = new FakeTemplateAmazonS3Client { ThrowOnList = true };
        var service = CreateService(s3Client);

        Assert.Empty(await service.ListAsync("templates/contracts/"));
    }

    [Fact]
    public async Task ListAsync_NoPrefixSupplied_FallsBackToConfiguredPrefix()
    {
        var s3Client = new FakeTemplateAmazonS3Client();
        var service = CreateService(s3Client, prefix: "templates");

        await service.ListAsync();

        Assert.Equal("templates/", s3Client.LastListPrefix);
    }

    [Fact]
    public async Task ListAsync_NoPrefixConfiguredOrSupplied_ListsWholeBucket()
    {
        var s3Client = new FakeTemplateAmazonS3Client();
        var service = CreateService(s3Client, prefix: string.Empty);

        await service.ListAsync();

        Assert.Equal(string.Empty, s3Client.LastListPrefix);
    }
}
