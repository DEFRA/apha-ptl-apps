using PTL.Auth.Cidm.Options;

namespace PTL.Auth.Cidm.Tests.Options;

public class CidmOptionsValidatorTests
{
    private static CidmOptions ValidOptions() => new()
    {
        Address = "https://your-account.cpdev.cui.defra.gov.uk/idphub/b2c",
        Policy = "b2c_1a_cui_cpdev_signupsignin",
        ClientId = "client-id",
        ClientSecret = "client-secret",
        ServiceId = "service-id"
    };

    [Fact]
    public void Validate_AllRequiredValuesPresent_Succeeds()
    {
        var result = new CidmOptionsValidator().Validate(null, ValidOptions());

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(nameof(CidmOptions.Address))]
    [InlineData(nameof(CidmOptions.Policy))]
    [InlineData(nameof(CidmOptions.ClientId))]
    [InlineData(nameof(CidmOptions.ClientSecret))]
    [InlineData(nameof(CidmOptions.ServiceId))]
    [InlineData(nameof(CidmOptions.CallbackPath))]
    [InlineData(nameof(CidmOptions.SignedOutCallbackPath))]
    public void Validate_MissingRequiredValue_Fails(string propertyName)
    {
        var options = ValidOptions();
        typeof(CidmOptions).GetProperty(propertyName)!.SetValue(options, string.Empty);

        var result = new CidmOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains(propertyName));
    }

    [Fact]
    public void Validate_AddressNotHttps_Fails()
    {
        var options = ValidOptions();
        options.Address = "http://your-account.cpdev.cui.defra.gov.uk/idphub/b2c";

        var result = new CidmOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
    }

    [Fact]
    public void Validate_AddressNotAbsoluteUri_Fails()
    {
        var options = ValidOptions();
        options.Address = "not-a-url";

        var result = new CidmOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
    }

    [Fact]
    public void Validate_ScopesMissingOpenId_Fails()
    {
        var options = ValidOptions();
        options.Scopes = ["offline_access"];

        var result = new CidmOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
    }

    [Fact]
    public void Validate_ScopesEmpty_Fails()
    {
        var options = ValidOptions();
        options.Scopes = [];

        var result = new CidmOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
    }

    [Fact]
    public void Validate_RefreshBeforeExpiryNotPositive_Fails()
    {
        var options = ValidOptions();
        options.RefreshBeforeExpiry = TimeSpan.Zero;

        var result = new CidmOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
    }

    [Fact]
    public void MetadataAddress_ComposesFromAddressAndPolicy()
    {
        var options = ValidOptions();

        Assert.Equal(
            "https://your-account.cpdev.cui.defra.gov.uk/idphub/b2c/b2c_1a_cui_cpdev_signupsignin/.well-known/openid-configuration",
            options.MetadataAddress);
    }

    [Fact]
    public void AllScopes_AppendsClientIdToConfiguredScopes()
    {
        var options = ValidOptions();

        Assert.Equal(["openid", "offline_access", "client-id"], options.AllScopes);
    }
}
