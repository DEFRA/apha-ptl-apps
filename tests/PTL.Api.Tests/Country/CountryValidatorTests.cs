using PTL.Core.Country;

namespace PTL.Api.Tests.Country;

public class CountryValidatorTests
{
    [Fact]
    public void Validate_NameAndTypeProvided_IsValid()
    {
        var result = CountryValidator.Validate("France", Guid.NewGuid());

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_MissingName_ReturnsError(string country)
    {
        var result = CountryValidator.Validate(country, Guid.NewGuid());

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "Country");
    }

    [Fact]
    public void Validate_NameTooLong_ReturnsError()
    {
        var result = CountryValidator.Validate(new string('a', 51), Guid.NewGuid());

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "Country");
    }

    [Fact]
    public void Validate_MissingCountryType_ReturnsError()
    {
        var result = CountryValidator.Validate("France", Guid.Empty);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "CountryTypeId");
    }
}
