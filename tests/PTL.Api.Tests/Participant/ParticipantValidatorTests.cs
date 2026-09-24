using PTL.Core.Participant;

namespace PTL.Api.Tests.Participant;

public class ParticipantValidatorTests
{
    private static PTL.Core.Participant.Participant ValidActiveParticipant() => new()
    {
        CustomerId = Guid.NewGuid(),
        SsoId = Guid.NewGuid(),
        LabCode = "LAB-001",
        LabName = "Alpha Lab",
        LabTypeId = Guid.NewGuid(),
        ContactName = "Alice Example",
        Organisation = "Alpha Organisation",
        Address1 = "1 Sample Street",
        Address2 = "Sample District",
        CountryId = Guid.NewGuid(),
        Telephone = "01234 567890",
        Email = "alice@example.com",
        IsActive = true
    };

    [Fact]
    public void Validate_ValidParticipant_ReturnsNoErrors()
    {
        var result = ParticipantValidator.Validate(ValidActiveParticipant());

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData("LabCode")]
    [InlineData("LabName")]
    [InlineData("ContactName")]
    [InlineData("Organisation")]
    [InlineData("Address1")]
    [InlineData("Address2")]
    [InlineData("CountryId")]
    [InlineData("Telephone")]
    [InlineData("Email")]
    public void Validate_MissingRequiredField_ReturnsError(string field)
    {
        var participant = ValidActiveParticipant();

        switch (field)
        {
            case "LabCode":
                participant.LabCode = string.Empty;
                break;
            case "LabName":
                participant.LabName = string.Empty;
                break;
            case "ContactName":
                participant.ContactName = string.Empty;
                break;
            case "Organisation":
                participant.Organisation = string.Empty;
                break;
            case "Address1":
                participant.Address1 = string.Empty;
                break;
            case "Address2":
                participant.Address2 = string.Empty;
                break;
            case "CountryId":
                participant.CountryId = Guid.Empty;
                break;
            case "Telephone":
                participant.Telephone = string.Empty;
                break;
            case "Email":
                participant.Email = string.Empty;
                break;
        }

        var result = ParticipantValidator.Validate(participant);

        Assert.Contains(result.Errors, e => e.Field == field);
    }

    [Fact]
    public void Validate_InvalidEmailFormat_ReturnsError()
    {
        var participant = ValidActiveParticipant();
        participant.Email = "not-an-email";

        var result = ParticipantValidator.Validate(participant);

        Assert.Contains(result.Errors, e => e.Field == "Email");
    }
}
