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
    public List<string> DeleteKeys { get; } = [];
    public List<string> ListedKeys { get; } = [];

    public override Task<PutObjectResponse> PutObjectAsync(PutObjectRequest request, CancellationToken cancellationToken = default)
    {
        LastBucketName = request.BucketName;
        LastKey = request.Key;
        LastContentType = request.ContentType;

        if (!string.IsNullOrWhiteSpace(request.Key) && request.Key.EndsWith("/.folder-marker", StringComparison.OrdinalIgnoreCase))
        {
            LastFolderMarkerKey = request.Key;
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

        var stream = new MemoryStream(GetObjectContent ?? []);
        return Task.FromResult(new GetObjectResponse { ResponseStream = stream });
    }

    public override Task<DeleteObjectResponse> DeleteObjectAsync(string bucketName, string key, CancellationToken cancellationToken = default)
    {
        LastBucketName = bucketName;
        LastKey = key;
        DeleteKeys.Add(key);
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

        return Task.FromResult(new GetObjectMetadataResponse());
    }

    public override Task<ListObjectsV2Response> ListObjectsV2Async(ListObjectsV2Request request, CancellationToken cancellationToken = default)
    {
        LastBucketName = request.BucketName;
        ListedKeys.Clear();

        var keys = request.Prefix is null || request.Prefix == "templates/contracts/"
            ? new[] { "templates/contracts/file-1.docx", "templates/contracts/file-2.docx" }
            : new[] { "templates/contracts/file-1.docx" };

        ListedKeys.AddRange(keys);
        return Task.FromResult(new ListObjectsV2Response { S3Objects = ListedKeys.Select(key => new S3Object { Key = key }).ToList() });
    }
}

public class S3TemplateStorageServiceTests
{
    private static S3TemplateStorageService CreateService(FakeTemplateAmazonS3Client s3Client, string bucketName = "devldnptl-env", string? prefix = "templates") =>
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
}
