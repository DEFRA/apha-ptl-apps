using PTL.Core.User;

namespace PTL.Api.Tests.User;

public class UserValidatorTests
{
    [Fact]
    public void Validate_EmailAndDepartmentValid_IsValid()
    {
        var result = UserValidator.Validate("jane.smith@apha.gov.uk", "Science");

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_MissingEmail_ReturnsNoEmailError(string email)
    {
        var result = UserValidator.Validate(email, "Science");

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "Email" && e.Message == UserValidator.NoEmailMessage);
    }

    [Fact]
    public void Validate_DepartmentTooLong_ReturnsError()
    {
        var result = UserValidator.Validate("jane.smith@apha.gov.uk", new string('a', 51));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "Department");
    }
}
