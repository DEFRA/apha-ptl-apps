using PTL.Core.GroupAddress;
using CoreGroupAddress = PTL.Core.GroupAddress.GroupAddress;

namespace PTL.Api.Tests.GroupAddress;

public class GroupAddressValidatorTests
{
    private static CoreGroupAddress ValidGroupAddress() => new()
    {
        GroupAddressId = Guid.NewGuid(),
        Identifier = "GA1",
        Address1 = "1 Group Street",
        Address2 = "Groupville",
        CountryId = Guid.NewGuid(),
        Telephone = "01234 567890"
    };

    [Fact]
    public void Validate_ValidGroupAddress_ReturnsNoErrors()
    {
        var result = GroupAddressValidator.Validate(ValidGroupAddress());

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData("Identifier")]
    [InlineData("Address1")]
    [InlineData("Address2")]
    public void Validate_MissingRequiredField_ReturnsError(string field)
    {
        var groupAddress = ValidGroupAddress();
        typeof(CoreGroupAddress).GetProperty(field)!.SetValue(groupAddress, string.Empty);

        var result = GroupAddressValidator.Validate(groupAddress);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == field && e.Message.EndsWith("is required", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_MissingCountry_ReturnsError()
    {
        var groupAddress = ValidGroupAddress();
        groupAddress.CountryId = Guid.Empty;

        var result = GroupAddressValidator.Validate(groupAddress);

        Assert.Contains(result.Errors, e => e.Field == nameof(CoreGroupAddress.CountryId) && e.Message == "Country must be selected");
    }

    [Theory]
    [InlineData("Identifier", 51)]
    [InlineData("Address1", 101)]
    [InlineData("Address2", 101)]
    [InlineData("Address3", 101)]
    [InlineData("Address4", 101)]
    [InlineData("Address5", 101)]
    [InlineData("Telephone", 21)]
    [InlineData("PackingInstructions", 501)]
    public void Validate_FieldExceedingMaxLength_ReturnsError(string field, int length)
    {
        var groupAddress = ValidGroupAddress();
        // Telephone also has a format rule, so pad with an allowed character rather than letters.
        var filler = field == "Telephone" ? '1' : 'x';
        typeof(CoreGroupAddress).GetProperty(field)!.SetValue(groupAddress, new string(filler, length));

        var result = GroupAddressValidator.Validate(groupAddress);

        Assert.Contains(result.Errors, e => e.Field == field && e.Message.Contains("must not exceed", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_TelephoneWithDisallowedCharacters_ReturnsFormatError()
    {
        var groupAddress = ValidGroupAddress();
        groupAddress.Telephone = "call me";

        var result = GroupAddressValidator.Validate(groupAddress);

        Assert.Contains(result.Errors, e => e.Field == nameof(CoreGroupAddress.Telephone)
            && e.Message.Contains("contains characters that are not allowed", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_EmptyTelephone_IsAllowed()
    {
        var groupAddress = ValidGroupAddress();
        groupAddress.Telephone = string.Empty;

        var result = GroupAddressValidator.Validate(groupAddress);

        Assert.True(result.IsValid);
    }
}
