using Amazon;
using Amazon.Runtime;
using PTL.Data.Storage;

namespace PTL.Data.Tests.Storage;

public class AwsS3ClientFactoryTests
{
    [Fact]
    public void Create_WithRegion_SetsRegionEndpoint()
    {
        using var client = AwsS3ClientFactory.Create("eu-west-2");

        Assert.Equal(RegionEndpoint.EUWest2, client.Config.RegionEndpoint);
    }

    // The AWS SDK's AmazonS3Client constructor validates its config and throws when neither
    // RegionEndpoint nor ServiceURL is set - this environment has no AWS_REGION/profile default,
    // so a null/blank region always hits that validation rather than silently leaving it unset.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithNullOrWhitespaceRegion_ThrowsBecauseNoRegionIsConfigured(string? region)
    {
        Assert.Throws<AmazonClientException>(() => AwsS3ClientFactory.Create(region));
    }
}
