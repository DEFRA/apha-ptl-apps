using System.ComponentModel.DataAnnotations;

namespace PTL.InternalWeb.Tests.Features.Customer;

// CustomerFormViewModel.Validate() delegates to PTL.Core.Customer.CustomerValidator and is only
// ever invoked by ASP.NET Core's model-validation pipeline, never by the controller directly -
// existing CustomerControllerTests bypass real model binding, so this is the only place it runs.
public class CustomerFormViewModelTests
{
    private static List<ValidationResult> Validate(PTL.InternalWeb.Features.Customer.CustomerFormViewModel model) =>
        model.Validate(new ValidationContext(model)).ToList();

    [Fact]
    public void Validate_InactiveWithMinimalFields_ReturnsErrorsWithoutThrowing()
    {
        var model = new PTL.InternalWeb.Features.Customer.CustomerFormViewModel { IsActive = false };

        var errors = Validate(model);

        Assert.Contains(errors, e => e.MemberNames.Contains("CustomerTypeId"));
    }

    [Fact]
    public void Validate_ActiveWithoutRequiredFields_ReturnsRequiredFieldErrors()
    {
        var model = new PTL.InternalWeb.Features.Customer.CustomerFormViewModel
        {
            Name = "Acme",
            CustomerTypeId = Guid.NewGuid(),
            RegisteredFileNumber = "QAL/12345",
            IsActive = true
        };

        var errors = Validate(model);

        Assert.Contains(errors, e => e.MemberNames.Contains("ContactName"));
        Assert.Contains(errors, e => e.MemberNames.Contains("Email"));
        Assert.Contains(errors, e => e.MemberNames.Contains("CountryId"));
        Assert.Contains(errors, e => e.MemberNames.Contains("InvoiceEmail"));
        Assert.Contains(errors, e => e.MemberNames.Contains("InvoiceCountryId"));
    }

    [Fact]
    public void Validate_ActiveWithoutAnyFields_ReturnsAllRequiredFieldErrorsInOneSubmission()
    {
        var model = new PTL.InternalWeb.Features.Customer.CustomerFormViewModel { IsActive = true };

        var errors = Validate(model);

        Assert.Contains(errors, e => e.MemberNames.Contains("Name"));
        Assert.Contains(errors, e => e.MemberNames.Contains("CustomerTypeId"));
        Assert.Contains(errors, e => e.MemberNames.Contains("ContactName"));
        Assert.Contains(errors, e => e.MemberNames.Contains("Organisation"));
        Assert.Contains(errors, e => e.MemberNames.Contains("Address1"));
        Assert.Contains(errors, e => e.MemberNames.Contains("Address2"));
        Assert.Contains(errors, e => e.MemberNames.Contains("CountryId"));
        Assert.Contains(errors, e => e.MemberNames.Contains("Telephone"));
        Assert.Contains(errors, e => e.MemberNames.Contains("Email"));
        Assert.Contains(errors, e => e.MemberNames.Contains("InvoiceOrganisation"));
        Assert.Contains(errors, e => e.MemberNames.Contains("InvoiceAddress1"));
        Assert.Contains(errors, e => e.MemberNames.Contains("InvoiceAddress2"));
        Assert.Contains(errors, e => e.MemberNames.Contains("InvoiceEmail"));
        Assert.Contains(errors, e => e.MemberNames.Contains("InvoiceCountryId"));
    }

    [Fact]
    public void Validate_ActiveWithAllRequiredFields_NoErrors()
    {
        var model = new PTL.InternalWeb.Features.Customer.CustomerFormViewModel
        {
            Name = "Acme",
            CustomerTypeId = Guid.NewGuid(),
            RegisteredFileNumber = "QAL/12345",
            ContactName = "Bob",
            Organisation = "Acme Ltd",
            Address1 = "1 Street",
            Address2 = "Town",
            CountryId = Guid.NewGuid(),
            Telephone = "01234 567890",
            Email = "bob@example.com",
            InvoiceOrganisation = "Acme Ltd",
            InvoiceAddress1 = "1 Street",
            InvoiceAddress2 = "Town",
            InvoiceCountryId = Guid.NewGuid(),
            InvoiceEmail = "invoices@example.com",
            IsActive = true
        };

        var errors = Validate(model);

        Assert.Empty(errors);
    }
}
