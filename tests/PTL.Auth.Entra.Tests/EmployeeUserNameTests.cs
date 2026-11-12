namespace PTL.Auth.Entra.Tests;

public class EmployeeUserNameTests
{
    [Theory]
    [InlineData("12345", @"DEMETER\12345")]
    [InlineData("  12345  ", @"DEMETER\12345")]
    [InlineData("A1.b_c-2", @"DEMETER\A1.b_c-2")]
    public void FromEmployeeId_BuildsTheDemeterUserName(string employeeId, string expected) =>
        Assert.Equal(expected, EmployeeUserName.FromEmployeeId(employeeId));

    [Fact]
    public void FromEmployeeId_AcceptsTheLongestPermittedId() =>
        Assert.Equal($"DEMETER\\{new string('1', 40)}", EmployeeUserName.FromEmployeeId(new string('1', 40)));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(@"12\34")]
    [InlineData("12 34")]
    [InlineData("12'34")]
    [InlineData("12;34")]
    [InlineData("ÀÉ12")]
    public void FromEmployeeId_ReturnsNull_ForMissingOrUnexpectedIds(string? employeeId) =>
        Assert.Null(EmployeeUserName.FromEmployeeId(employeeId));

    [Fact]
    public void FromEmployeeId_ReturnsNull_WhenTheIdIsTooLongForTheUsernameColumn() =>
        Assert.Null(EmployeeUserName.FromEmployeeId(new string('1', 41)));
}
