using PTL.Core.Participant;

namespace PTL.Api.Tests.Participant;

public class PendingParticipantUpdateValidatorTests
{
    private static PendingParticipantUpdate Valid() => new()
    {
        ContactName = "Jane Doe",
        Organisation = "Test Lab",
        Address1 = "1 Test Street",
        Address2 = "Testville",
        CountryId = Guid.NewGuid(),
        Telephone = "01234 567890",
        Email = "jane@example.com"
    };

    [Fact]
    public void Validate_AllRequiredFieldsPresent_IsValid()
    {
        var result = PendingParticipantUpdateValidator.Validate(Valid());

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData("ContactName")]
    [InlineData("Organisation")]
    [InlineData("Address1")]
    [InlineData("Address2")]
    [InlineData("Telephone")]
    [InlineData("Email")]
    public void Validate_RequiredFieldBlank_ReturnsError(string field)
    {
        var update = Valid();
        typeof(PendingParticipantUpdate).GetProperty(field)!.SetValue(update, string.Empty);

        var result = PendingParticipantUpdateValidator.Validate(update);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == field);
    }

    [Theory]
    [InlineData("Address3")]
    [InlineData("Address4")]
    [InlineData("Address5")]
    [InlineData("Email2")]
    public void Validate_OptionalFieldBlank_IsStillValid(string field)
    {
        var update = Valid();
        typeof(PendingParticipantUpdate).GetProperty(field)!.SetValue(update, string.Empty);

        var result = PendingParticipantUpdateValidator.Validate(update);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_CountryIdNotSelected_ReturnsError()
    {
        var update = Valid();
        update.CountryId = Guid.Empty;

        var result = PendingParticipantUpdateValidator.Validate(update);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "CountryId");
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("01234/567890!")]
    public void Validate_TelephoneWithDisallowedCharacters_ReturnsError(string telephone)
    {
        var update = Valid();
        update.Telephone = telephone;

        var result = PendingParticipantUpdateValidator.Validate(update);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "Telephone");
    }

    [Fact]
    public void Validate_EmailInvalidFormat_ReturnsError()
    {
        var update = Valid();
        update.Email = "not-an-email";

        var result = PendingParticipantUpdateValidator.Validate(update);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "Email");
    }

    [Fact]
    public void Validate_Email2InvalidFormatButSupplied_ReturnsError()
    {
        var update = Valid();
        update.Email2 = "not-an-email";

        var result = PendingParticipantUpdateValidator.Validate(update);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "Email2");
    }

    [Fact]
    public void Validate_ContactNameTooLong_ReturnsError()
    {
        var update = Valid();
        update.ContactName = new string('a', 51);

        var result = PendingParticipantUpdateValidator.Validate(update);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "ContactName");
    }
}
