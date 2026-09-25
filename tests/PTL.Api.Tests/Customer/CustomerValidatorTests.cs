using PTL.Core.Customer;

namespace PTL.Api.Tests.Customer;

public class CustomerValidatorTests
{
    private static PTL.Core.Customer.Customer ValidActiveCustomer() => new()
    {
        Name = "Sample Laboratories Ltd",
        CustomerTypeId = Guid.NewGuid(),
        ContactName = "Alice Example",
        Organisation = "Sample Laboratories Ltd",
        Address1 = "1 Sample Street",
        Address2 = "Sample District",
        CountryId = Guid.NewGuid(),
        Telephone = "01234 567890",
        Email = "alice@example.com",
        InvoiceOrganisation = "Sample Laboratories Ltd",
        InvoiceAddress1 = "1 Sample Street",
        InvoiceAddress2 = "Sample District",
        InvoiceCountryId = Guid.NewGuid(),
        InvoiceEmail = "invoices@example.com",
        IsActive = true
    };

    [Fact]
    public void Validate_ValidActiveCustomer_ReturnsNoErrors()
    {
        var result = CustomerValidator.Validate(ValidActiveCustomer());

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_EmptyCustomerTypeId_ReturnsError()
    {
        var customer = ValidActiveCustomer();
        customer.CustomerTypeId = Guid.Empty;

        var result = CustomerValidator.Validate(customer);

        Assert.Contains(result.Errors, e => e.Field == "CustomerTypeId");
    }

    [Fact]
    public void Validate_MissingName_ReturnsError()
    {
        var customer = ValidActiveCustomer();
        customer.Name = string.Empty;

        var result = CustomerValidator.Validate(customer);

        Assert.Contains(result.Errors, e => e.Field == "Name");
    }

    [Theory]
    [InlineData("QAL/12345")]
    [InlineData("")]
    public void Validate_ValidRegisteredFileNumberFormats_ReturnNoRegisteredFileNumberError(string value)
    {
        var customer = ValidActiveCustomer();
        customer.RegisteredFileNumber = value;

        var result = CustomerValidator.Validate(customer);

        Assert.DoesNotContain(result.Errors, e => e.Field == "RegisteredFileNumber");
    }

    [Fact]
    public void Validate_InvalidRegisteredFileNumberFormat_ReturnsError()
    {
        var customer = ValidActiveCustomer();
        customer.RegisteredFileNumber = "NOT-VALID";

        var result = CustomerValidator.Validate(customer);

        Assert.Contains(result.Errors, e => e.Field == "RegisteredFileNumber");
    }

    [Fact]
    public void Validate_InvalidTelephoneCharacters_ReturnsError()
    {
        var customer = ValidActiveCustomer();
        customer.Telephone = "call-me-maybe";

        var result = CustomerValidator.Validate(customer);

        Assert.Contains(result.Errors, e => e.Field == "Telephone");
    }

    [Fact]
    public void Validate_NameExceedsMaxLength_ReturnsError()
    {
        var customer = ValidActiveCustomer();
        customer.Name = new string('a', 51);

        var result = CustomerValidator.Validate(customer);

        Assert.Contains(result.Errors, e => e.Field == "Name");
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
    [InlineData("InvoiceEmail")]
    public void Validate_ActiveCustomerMissingConditionallyRequiredField_ReturnsError(string field)
    {
        var customer = ValidActiveCustomer();
        typeof(PTL.Core.Customer.Customer).GetProperty(field)!.SetValue(customer, string.Empty);

        var result = CustomerValidator.Validate(customer);

        Assert.Contains(result.Errors, e => e.Field == field);
    }

    [Fact]
    public void Validate_ActiveCustomerMissingInvoiceCountryId_ReturnsError()
    {
        var customer = ValidActiveCustomer();
        customer.InvoiceCountryId = Guid.Empty;

        var result = CustomerValidator.Validate(customer);

        Assert.Contains(result.Errors, e => e.Field == "InvoiceCountryId");
    }

    [Fact]
    public void Validate_ActiveCustomerMissingCountryId_ReturnsError()
    {
        var customer = ValidActiveCustomer();
        customer.CountryId = Guid.Empty;

        var result = CustomerValidator.Validate(customer);

        Assert.Contains(result.Errors, e => e.Field == "CountryId");
    }

    [Fact]
    public void Validate_ActiveCustomerAllRequiredFieldsMissing_ReturnsAllErrorsInOneCall()
    {
        var customer = new PTL.Core.Customer.Customer { IsActive = true };

        var result = CustomerValidator.Validate(customer);

        Assert.Contains(result.Errors, e => e.Field == "Name");
        Assert.Contains(result.Errors, e => e.Field == "CustomerTypeId");
        Assert.Contains(result.Errors, e => e.Field == "ContactName");
        Assert.Contains(result.Errors, e => e.Field == "Organisation");
        Assert.Contains(result.Errors, e => e.Field == "Address1");
        Assert.Contains(result.Errors, e => e.Field == "Address2");
        Assert.Contains(result.Errors, e => e.Field == "CountryId");
        Assert.Contains(result.Errors, e => e.Field == "Telephone");
        Assert.Contains(result.Errors, e => e.Field == "Email");
        Assert.Contains(result.Errors, e => e.Field == "InvoiceOrganisation");
        Assert.Contains(result.Errors, e => e.Field == "InvoiceAddress1");
        Assert.Contains(result.Errors, e => e.Field == "InvoiceAddress2");
        Assert.Contains(result.Errors, e => e.Field == "InvoiceEmail");
        Assert.Contains(result.Errors, e => e.Field == "InvoiceCountryId");
    }

    [Fact]
    public void Validate_InactiveCustomerMissingConditionallyRequiredFields_ReturnsNoErrorsForThem()
    {
        var customer = ValidActiveCustomer();
        customer.IsActive = false;
        customer.ContactName = string.Empty;
        customer.Organisation = string.Empty;
        customer.CountryId = Guid.Empty;
        customer.InvoiceEmail = string.Empty;
        customer.InvoiceCountryId = Guid.Empty;
        customer.CustomerStatusId = Guid.NewGuid();

        var result = CustomerValidator.Validate(customer);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_InvalidEmailFormat_ReturnsError()
    {
        var customer = ValidActiveCustomer();
        customer.Email = "not-an-email";

        var result = CustomerValidator.Validate(customer);

        Assert.Contains(result.Errors, e => e.Field == "Email");
    }

    // Request DTO string properties are declared non-nullable, but a JSON body that omits a
    // property still deserializes it to null at runtime - Validate() must not throw for that.
    [Theory]
    [InlineData("Name")]
    [InlineData("ContactName")]
    [InlineData("Organisation")]
    [InlineData("Telephone")]
    [InlineData("Email")]
    [InlineData("InvoiceEmail")]
    public void Validate_NullRequiredStringField_DoesNotThrowAndStillReportsRequiredError(string field)
    {
        var customer = ValidActiveCustomer();
        typeof(PTL.Core.Customer.Customer).GetProperty(field)!.SetValue(customer, null);

        var result = CustomerValidator.Validate(customer);

        Assert.Contains(result.Errors, e => e.Field == field);
    }

    [Fact]
    public void Validate_NullRegisteredFileNumber_DoesNotThrow()
    {
        var customer = ValidActiveCustomer();
        customer.RegisteredFileNumber = null!;

        var result = CustomerValidator.Validate(customer);

        Assert.DoesNotContain(result.Errors, e => e.Field == "RegisteredFileNumber");
    }
}
