using System.ComponentModel.DataAnnotations;

namespace PTL.InternalWeb.Tests.Features.Participant;

// ParticipantFormViewModel.Validate() delegates to PTL.Core.Participant.ParticipantValidator and is
// only ever invoked by ASP.NET Core's model-validation pipeline, never by the controller directly -
// existing ParticipantControllerTests bypass real model binding, so this is the only place it runs.
public class ParticipantFormViewModelTests
{
    private static List<ValidationResult> Validate(PTL.InternalWeb.Features.Participant.ParticipantFormViewModel model) =>
        model.Validate(new ValidationContext(model)).ToList();

    [Fact]
    public void Validate_WithoutAnyFields_ReturnsAllRequiredFieldErrorsInOneSubmission()
    {
        var model = new PTL.InternalWeb.Features.Participant.ParticipantFormViewModel();

        var errors = Validate(model);

        Assert.Contains(errors, e => e.MemberNames.Contains("LabCode"));
        Assert.Contains(errors, e => e.MemberNames.Contains("LabName"));
        Assert.Contains(errors, e => e.MemberNames.Contains("ContactName"));
        Assert.Contains(errors, e => e.MemberNames.Contains("Organisation"));
        Assert.Contains(errors, e => e.MemberNames.Contains("Address1"));
        Assert.Contains(errors, e => e.MemberNames.Contains("Address2"));
        Assert.Contains(errors, e => e.MemberNames.Contains("CountryId"));
        Assert.Contains(errors, e => e.MemberNames.Contains("Telephone"));
        Assert.Contains(errors, e => e.MemberNames.Contains("Email"));
    }

    [Fact]
    public void Validate_WithoutCountry_ReturnsCountryError()
    {
        var model = new PTL.InternalWeb.Features.Participant.ParticipantFormViewModel
        {
            LabCode = "LAB-01",
            LabName = "Laboratory 1",
            ContactName = "Contact Name",
            Organisation = "Organisation Name",
            Address1 = "Address Line 1",
            Address2 = "Address Line 2",
            Telephone = "01234567890",
            Email = "test@example.com"
        };

        var errors = Validate(model);

        Assert.Contains(errors, e => e.MemberNames.Contains("CountryId"));
    }

    [Fact]
    public void Validate_WithInvalidEmail_ReturnsEmailFormatError()
    {
        var model = new PTL.InternalWeb.Features.Participant.ParticipantFormViewModel
        {
            LabCode = "LAB-01",
            LabName = "Laboratory 1",
            ContactName = "Contact Name",
            Organisation = "Organisation Name",
            Address1 = "Address Line 1",
            Address2 = "Address Line 2",
            CountryId = Guid.NewGuid(),
            Telephone = "01234567890",
            Email = "not-an-email"
        };

        var errors = Validate(model);

        Assert.Contains(errors, e => e.MemberNames.Contains("Email"));
    }

    [Fact]
    public void Validate_WithAllFieldsPopulated_ReturnsNoErrors()
    {
        var model = new PTL.InternalWeb.Features.Participant.ParticipantFormViewModel
        {
            LabCode = "LAB-01",
            LabName = "Laboratory 1",
            ContactName = "Contact Name",
            Organisation = "Organisation Name",
            Address1 = "Address Line 1",
            Address2 = "Address Line 2",
            CountryId = Guid.NewGuid(),
            Telephone = "01234567890",
            Email = "test@example.com"
        };

        var errors = Validate(model);

        Assert.Empty(errors);
    }

    [Fact]
    public void ParticipantFormViewModel_AllPropertiesAssignable()
    {
        var customerId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var labTypeId = Guid.NewGuid();
        var countryId = Guid.NewGuid();
        var ssoId = Guid.NewGuid();

        var model = new PTL.InternalWeb.Features.Participant.ParticipantFormViewModel
        {
            ParticipantId = participantId,
            CustomerId = customerId,
            SsoId = ssoId,
            LabCode = "LAB-01",
            LabName = "Laboratory 1",
            LabTypeId = labTypeId,
            ContactName = "Contact Name",
            Organisation = "Organisation Name",
            Address1 = "Address Line 1",
            Address2 = "Address Line 2",
            Address3 = "Address Line 3",
            Address4 = "Address Line 4",
            Address5 = "Address Line 5",
            CountryId = countryId,
            Telephone = "01234567890",
            Fax = "01234567891",
            Email = "test@example.com",
            Email2 = "test2@example.com",
            Comments = "Comments",
            IsActive = true,
            InactiveDate = DateTime.UtcNow,
            CustomerContactName = "Customer Contact",
            CustomerOrganisation = "Customer Org",
            CustomerAddress1 = "Customer Address 1",
            CustomerAddress2 = "Customer Address 2",
            CustomerAddress3 = "Customer Address 3",
            CustomerAddress4 = "Customer Address 4",
            CustomerAddress5 = "Customer Address 5",
            CustomerCountryId = Guid.NewGuid(),
            CustomerTelephone = "02345678901",
            CustomerFax = "02345678902",
            CustomerEmail = "customer@example.com"
        };

        Assert.Equal(participantId, model.ParticipantId);
        Assert.Equal(customerId, model.CustomerId);
        Assert.Equal(ssoId, model.SsoId);
        Assert.Equal("LAB-01", model.LabCode);
        Assert.Equal("Laboratory 1", model.LabName);
        Assert.Equal(labTypeId, model.LabTypeId);
        Assert.Equal("Contact Name", model.ContactName);
        Assert.Equal("Organisation Name", model.Organisation);
        Assert.Equal("Address Line 1", model.Address1);
        Assert.Equal("Address Line 2", model.Address2);
        Assert.Equal("Address Line 3", model.Address3);
        Assert.Equal("Address Line 4", model.Address4);
        Assert.Equal("Address Line 5", model.Address5);
        Assert.Equal(countryId, model.CountryId);
        Assert.Equal("01234567890", model.Telephone);
        Assert.Equal("01234567891", model.Fax);
        Assert.Equal("test@example.com", model.Email);
        Assert.Equal("test2@example.com", model.Email2);
        Assert.Equal("Comments", model.Comments);
        Assert.True(model.IsActive);
        Assert.NotNull(model.InactiveDate);
        Assert.Equal("Customer Contact", model.CustomerContactName);
        Assert.Equal("Customer Org", model.CustomerOrganisation);
    }

    [Fact]
    public void ParticipantFormViewModel_DefaultValues()
    {
        var model = new PTL.InternalWeb.Features.Participant.ParticipantFormViewModel();

        Assert.Null(model.ParticipantId);
        Assert.Null(model.CustomerId);
        Assert.Null(model.SsoId);
        Assert.Equal(string.Empty, model.LabCode);
        Assert.Equal(string.Empty, model.LabName);
        Assert.Null(model.LabTypeId);
        Assert.Equal(string.Empty, model.ContactName);
        Assert.Equal(string.Empty, model.Organisation);
        Assert.Equal(string.Empty, model.Address1);
        Assert.Equal(string.Empty, model.Address2);
        Assert.Equal(string.Empty, model.Address3);
        Assert.Equal(string.Empty, model.Address4);
        Assert.Equal(string.Empty, model.Address5);
        Assert.Null(model.CountryId);
        Assert.Equal(string.Empty, model.Telephone);
        Assert.Equal(string.Empty, model.Fax);
        Assert.Equal(string.Empty, model.Email);
        Assert.Equal(string.Empty, model.Email2);
        Assert.Equal(string.Empty, model.Comments);
        Assert.True(model.IsActive);
        Assert.Null(model.InactiveDate);
        Assert.Equal(string.Empty, model.CustomerContactName);
        Assert.Equal(string.Empty, model.CustomerOrganisation);
        Assert.Equal(string.Empty, model.CustomerAddress1);
        Assert.Equal(string.Empty, model.CustomerAddress2);
        Assert.Equal(string.Empty, model.CustomerAddress3);
        Assert.Equal(string.Empty, model.CustomerAddress4);
        Assert.Equal(string.Empty, model.CustomerAddress5);
        Assert.Null(model.CustomerCountryId);
        Assert.Equal(string.Empty, model.CustomerTelephone);
        Assert.Equal(string.Empty, model.CustomerFax);
        Assert.Equal(string.Empty, model.CustomerEmail);
    }
}
