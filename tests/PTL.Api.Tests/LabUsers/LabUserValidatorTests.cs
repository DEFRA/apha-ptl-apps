using PTL.Contracts.LabUsers;
using PTL.Core.LabUsers;

namespace PTL.Api.Tests.LabUsers;

public class LabUserValidatorTests
{
    [Fact]
    public void Validate_NameAndEmailOk_HasNoErrors()
    {
        Assert.Empty(LabUserValidator.Validate("Ramesh Mehara", "ramesh_mehara@lab.com"));
    }

    [Theory]
    [InlineData("name@example.com")]
    [InlineData("first.last+tag@sub.example.co.uk")]
    [InlineData("o'brien@example.photography")]
    [InlineData("  padded@example.com  ")]
    public void Validate_ValidEmail_Accepted(string email)
    {
        Assert.False(LabUserValidator.Validate("Ramesh", email).ContainsKey(LabUserValidator.EmailField));
    }

    [Theory]
    [InlineData("plainaddress")]
    [InlineData("@example.com")]
    [InlineData("name@")]
    [InlineData("name@example")]
    [InlineData("name@@example.com")]
    [InlineData("na me@example.com")]
    [InlineData("name@example.com, other@example.com")]
    [InlineData("name@example.")]
    [InlineData("name@example.com\r\nBcc: x@y.com")]
    public void Validate_NotASingleAddress_IsRejected(string email)
    {
        var errors = LabUserValidator.Validate("Ramesh", email);

        Assert.Equal([LabUserValidator.EmailInvalidMessage], errors[LabUserValidator.EmailField]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_BlankEmail_IsRequired(string email)
    {
        Assert.Equal([LabUserValidator.EmailRequiredMessage], LabUserValidator.Validate("Ramesh", email)[LabUserValidator.EmailField]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Validate_BlankName_IsRequired(string name)
    {
        Assert.Equal([LabUserValidator.NameRequiredMessage], LabUserValidator.Validate(name, "a@b.com")[LabUserValidator.NameField]);
    }

    [Fact]
    public void Validate_LongEmail_IsRejectedBeforeThePatternRuns()
    {
        var email = new string('a', LabUserSaveRequest.MaxEmailLength) + "@example.com";

        Assert.Equal([LabUserValidator.EmailTooLongMessage], LabUserValidator.Validate("Ramesh", email)[LabUserValidator.EmailField]);
    }

    [Fact]
    public void Validate_LongName_IsRejected()
    {
        var name = new string('a', LabUserSaveRequest.MaxNameLength + 1);

        Assert.Equal([LabUserValidator.NameTooLongMessage], LabUserValidator.Validate(name, "a@b.com")[LabUserValidator.NameField]);
    }

    [Fact]
    public void Validate_NameAtTheLimit_Accepted()
    {
        Assert.Empty(LabUserValidator.Validate(new string('a', LabUserSaveRequest.MaxNameLength), "a@b.com"));
    }

    [Theory]
    [InlineData("Line\nbreak")]
    [InlineData("Tab\there")]
    public void Validate_NameWithControlCharacters_IsRejected(string name)
    {
        Assert.Equal([LabUserValidator.NameInvalidMessage], LabUserValidator.Validate(name, "a@b.com")[LabUserValidator.NameField]);
    }

    [Fact]
    public void Validate_BothInvalid_ReportsBoth()
    {
        var errors = LabUserValidator.Validate("", "");

        Assert.Equal(2, errors.Count);
    }
}
