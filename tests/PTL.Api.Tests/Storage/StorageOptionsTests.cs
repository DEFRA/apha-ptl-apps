using PTL.Core.Contract.Export.Templates;
using PTL.Core.Storage;

namespace PTL.Api.Tests.Storage;

public class StorageOptionsTests
{
    [Fact]
    public void S3Options_DefaultsToS3ProviderWithPerFeaturePrefixes()
    {
        var options = new S3Options();

        Assert.Equal("S3", options.Provider);
        Assert.Equal(string.Empty, options.Region);
        Assert.Equal(string.Empty, options.BucketName);
        Assert.Equal("templates", options.Templates.Prefix);
        Assert.Equal("invoices", options.Invoices.Prefix);
        Assert.Equal("S3", S3Options.SectionName);
    }

    // Legacy ExportBase rejected anything over 4MB.
    [Fact]
    public void S3Options_DefaultsTemplateUploadLimitTo4Mb()
    {
        Assert.Equal(4 * 1024 * 1024, new S3Options().Templates.MaxUploadBytes);
    }

    [Fact]
    public void S3Options_PropertiesAreSettable()
    {
        var options = new S3Options
        {
            Provider = "InMemory",
            Region = "eu-west-2",
            BucketName = "bucket",
            Templates = new S3TemplatesOptions { Prefix = "t", MaxUploadBytes = 10 },
            Invoices = new S3InvoicesOptions { Prefix = "i" }
        };

        Assert.Equal("InMemory", options.Provider);
        Assert.Equal("eu-west-2", options.Region);
        Assert.Equal("bucket", options.BucketName);
        Assert.Equal("t", options.Templates.Prefix);
        Assert.Equal(10, options.Templates.MaxUploadBytes);
        Assert.Equal("i", options.Invoices.Prefix);
    }

    [Fact]
    public void TemplateStorageOptions_Defaults()
    {
        var options = new TemplateStorageOptions();

        Assert.Equal("TemplateStorage", TemplateStorageOptions.SectionName);
        Assert.Equal("S3", options.Provider);
        Assert.Equal(string.Empty, options.BucketName);
        Assert.Equal(string.Empty, options.Region);
        Assert.Equal("templates", options.Prefix);
        Assert.Equal(4 * 1024 * 1024, options.MaxUploadBytes);
    }

    // TemplatesPrefix is an alias so S3-shaped configuration binds to the same value as Prefix.
    [Fact]
    public void TemplateStorageOptions_TemplatesPrefixIsAnAliasForPrefix()
    {
        var options = new TemplateStorageOptions();

        Assert.Equal(options.Prefix, options.TemplatesPrefix);

        options.TemplatesPrefix = "custom";
        Assert.Equal("custom", options.Prefix);

        options.Prefix = "other";
        Assert.Equal("other", options.TemplatesPrefix);
    }
}
