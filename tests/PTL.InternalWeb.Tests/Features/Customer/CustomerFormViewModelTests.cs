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
            Telephone = "01234 567890",
            Email = "bob@example.com",
            InvoiceOrganisation = "Acme Ltd",
            InvoiceAddress1 = "1 Street",
            InvoiceAddress2 = "Town",
            InvoiceEmail = "invoices@example.com",
            IsActive = true
        };

        var errors = Validate(model);

        Assert.Empty(errors);
    }
}
