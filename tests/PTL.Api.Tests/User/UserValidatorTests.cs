using PTL.Core.User;

namespace PTL.Api.Tests.User;

public class UserValidatorTests
{
    private static NewUserDetails Details(string email = "jane.smith@apha.gov.uk", string department = "Science") =>
        new("m100001", email, "Jane Smith", "Jane", "Smith", department);

    [Fact]
    public void Validate_AllValuesValid_IsValid()
    {
        var result = UserValidator.Validate(Details());

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_MissingEmail_ReturnsNoEmailError(string email)
    {
        var result = UserValidator.Validate(Details(email: email));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "Email" && e.Message == UserValidator.NoEmailMessage);
    }

    [Fact]
    public void Validate_DepartmentTooLong_ReturnsError()
    {
        var result = UserValidator.Validate(Details(department: new string('a', 51)));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "Department");
    }

    [Fact]
    public void Validate_DepartmentOfFiftyCharactersPlusPadding_IsValid()
    {
        var result = UserValidator.Validate(Details(department: $"  {new string('a', 50)}  "));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_EmailTooLong_ReturnsLengthError()
    {
        var result = UserValidator.Validate(Details(email: $"{new string('a', 45)}@apha.gov.uk"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "Email" && e.Message == "Email address must be 50 characters or fewer");
    }
}
