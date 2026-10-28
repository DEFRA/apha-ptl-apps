using PTL.Core.Customer;

namespace PTL.Api.Tests.Customer;

public class PendingCustomerUpdateValidatorTests
{
    private static PendingCustomerUpdate Valid() => new()
    {
        ContactName = "Jane Doe",
        Organisation = "Test Lab",
        Address1 = "1 Test Street",
        Address2 = "Testville",
        CountryId = Guid.NewGuid(),
        Telephone = "01234 567890",
        Email = "jane@example.com",
        InvoiceOrganisation = "Test Lab Invoicing",
        InvoiceAddress1 = "1 Test Street",
        InvoiceAddress2 = "Testville",
        InvoiceCountryId = Guid.NewGuid()
    };

    [Fact]
    public void Validate_AllRequiredFieldsPresent_IsValid()
    {
        var result = PendingCustomerUpdateValidator.Validate(Valid());

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
    [InlineData("InvoiceOrganisation")]
    [InlineData("InvoiceAddress1")]
    [InlineData("InvoiceAddress2")]
    public void Validate_RequiredFieldBlank_ReturnsError(string field)
    {
        var update = Valid();
        typeof(PendingCustomerUpdate).GetProperty(field)!.SetValue(update, string.Empty);

        var result = PendingCustomerUpdateValidator.Validate(update);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == field);
    }

    [Theory]
    [InlineData("Address3")]
    [InlineData("Address4")]
    [InlineData("Address5")]
    [InlineData("Telephone2")]
    [InlineData("InvoiceName")]
    [InlineData("InvoiceAddress3")]
    [InlineData("InvoiceAddress4")]
    [InlineData("InvoiceAddress5")]
    [InlineData("InvoiceTelephone")]
    [InlineData("InvoiceTelephone2")]
    [InlineData("InvoiceEmail")]
    public void Validate_OptionalFieldBlank_IsStillValid(string field)
    {
        var update = Valid();
        typeof(PendingCustomerUpdate).GetProperty(field)!.SetValue(update, string.Empty);

        var result = PendingCustomerUpdateValidator.Validate(update);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_CountryIdNotSelected_ReturnsError()
    {
        var update = Valid();
        update.CountryId = Guid.Empty;

        var result = PendingCustomerUpdateValidator.Validate(update);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "CountryId");
    }

    [Fact]
    public void Validate_InvoiceCountryIdNotSelected_ReturnsError()
    {
        var update = Valid();
        update.InvoiceCountryId = Guid.Empty;

        var result = PendingCustomerUpdateValidator.Validate(update);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "InvoiceCountryId");
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("01234/567890!")]
    public void Validate_TelephoneWithDisallowedCharacters_ReturnsError(string telephone)
    {
        var update = Valid();
        update.Telephone = telephone;

        var result = PendingCustomerUpdateValidator.Validate(update);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "Telephone");
    }

    [Fact]
    public void Validate_EmailInvalidFormat_ReturnsError()
    {
        var update = Valid();
        update.Email = "not-an-email";

        var result = PendingCustomerUpdateValidator.Validate(update);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "Email");
    }

    [Fact]
    public void Validate_InvoiceEmailInvalidFormatButSupplied_ReturnsError()
    {
        var update = Valid();
        update.InvoiceEmail = "not-an-email";

        var result = PendingCustomerUpdateValidator.Validate(update);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "InvoiceEmail");
    }

    [Fact]
    public void Validate_ContactNameTooLong_ReturnsError()
    {
        var update = Valid();
        update.ContactName = new string('a', 51);

        var result = PendingCustomerUpdateValidator.Validate(update);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "ContactName");
    }
}
