using PTL.Auth.Entra.Options;

namespace PTL.Auth.Entra.Tests.Options;

public class EntraOptionsValidatorTests
{
    private static EntraOptions ValidOptions() => new()
    {
        TenantId = "00000000-0000-0000-0000-000000000000",
        ClientId = "client-id",
        ClientSecret = "client-secret"
    };

    [Fact]
    public void Validate_AllRequiredValuesPresent_Succeeds()
    {
        var result = new EntraOptionsValidator().Validate(null, ValidOptions());

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(nameof(EntraOptions.TenantId))]
    [InlineData(nameof(EntraOptions.ClientId))]
    [InlineData(nameof(EntraOptions.ClientSecret))]
    [InlineData(nameof(EntraOptions.CallbackPath))]
    [InlineData(nameof(EntraOptions.SignedOutCallbackPath))]
    [InlineData(nameof(EntraOptions.FrontChannelLogoutPath))]
    public void Validate_MissingRequiredValue_Fails(string propertyName)
    {
        var options = ValidOptions();
        typeof(EntraOptions).GetProperty(propertyName)!.SetValue(options, string.Empty);

        var result = new EntraOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains(propertyName));
    }

    [Fact]
    public void Validate_ScopesEmpty_Fails()
    {
        var options = ValidOptions();
        options.Scopes = [];

        var result = new EntraOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
    }

    [Fact]
    public void Validate_ScopesMissingOpenId_Fails()
    {
        var options = ValidOptions();
        options.Scopes = ["profile"];

        var result = new EntraOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
    }

    [Fact]
    public void Validate_RefreshBeforeExpiryNotPositive_Fails()
    {
        var options = ValidOptions();
        options.RefreshBeforeExpiry = TimeSpan.Zero;

        var result = new EntraOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
    }

    [Fact]
    public void Authority_ComposesFromTenantId()
    {
        var options = ValidOptions();

        Assert.Equal("https://login.microsoftonline.com/00000000-0000-0000-0000-000000000000/v2.0", options.Authority);
    }
}
