using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using PTL.Core.Invoice;
using PTL.Data.Storage;

namespace PTL.Data.Tests.Storage;

// AWSSDK clients expose their operations as virtual, specifically so callers can subclass and
// override them for unit tests without a real bucket/network call or a mocking framework.
internal sealed class FakeAmazonS3Client() : AmazonS3Client(new BasicAWSCredentials("test", "test"), new AmazonS3Config { ServiceURL = "http://localhost" })
{
    public string? LastBucketName { get; private set; }
    public string? LastKey { get; private set; }
    public string? LastContentType { get; private set; }
    public byte[]? GetObjectContent { get; set; }
    public bool ThrowNotFoundOnGet { get; set; }

    public override Task<PutObjectResponse> PutObjectAsync(PutObjectRequest request, CancellationToken cancellationToken = default)
    {
        LastBucketName = request.BucketName;
        LastKey = request.Key;
        LastContentType = request.ContentType;
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
}

public class S3InvoiceStorageServiceTests
{
    private static S3InvoiceStorageService CreateService(FakeAmazonS3Client s3Client, string bucketName = "invoices-bucket") =>
        new(s3Client, Options.Create(new InvoiceStorageOptions { BucketName = bucketName }));

    [Fact]
    public async Task SaveAsync_PutsObjectWithExpectedBucketKeyAndContentType()
    {
        var s3Client = new FakeAmazonS3Client();
        var service = CreateService(s3Client);

        await service.SaveAsync("invoices/PT_Invoices_2026-01-01.csv", [1, 2, 3], "text/csv");

        Assert.Equal("invoices-bucket", s3Client.LastBucketName);
        Assert.Equal("invoices/PT_Invoices_2026-01-01.csv", s3Client.LastKey);
        Assert.Equal("text/csv", s3Client.LastContentType);
    }

    [Fact]
    public async Task SaveAsync_EmptyBucketName_Throws()
    {
        var s3Client = new FakeAmazonS3Client();
        var service = CreateService(s3Client, bucketName: string.Empty);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync("key.csv", [1], "text/csv"));
    }

    [Fact]
    public async Task GetAsync_ReturnsStoredContent()
    {
        var s3Client = new FakeAmazonS3Client { GetObjectContent = [4, 5, 6] };
        var service = CreateService(s3Client);

        var result = await service.GetAsync("invoices/PT_Invoices_2026-01-01.csv");

        Assert.Equal(new byte[] { 4, 5, 6 }, result);
        Assert.Equal("invoices-bucket", s3Client.LastBucketName);
    }

    [Fact]
    public async Task GetAsync_NotFound_ReturnsNull()
    {
        var s3Client = new FakeAmazonS3Client { ThrowNotFoundOnGet = true };
        var service = CreateService(s3Client);

        var result = await service.GetAsync("missing.csv");

        Assert.Null(result);
    }
}
