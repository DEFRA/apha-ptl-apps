using Amazon;
using Amazon.S3;

namespace PTL.Data.Storage;

public static class AwsS3ClientFactory
{
    public static IAmazonS3 Create(string? region)
    {
        var config = new AmazonS3Config();

        if (!string.IsNullOrWhiteSpace(region))
        {
            config.RegionEndpoint = RegionEndpoint.GetBySystemName(region.Trim());
        }

        return new AmazonS3Client(config);
    }
}
