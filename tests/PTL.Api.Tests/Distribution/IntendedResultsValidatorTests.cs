using PTL.Core.Distribution;

namespace PTL.Api.Tests.Distribution;

// Pins legacy SampleNumbers/IntendedResults.aspx.vb's RegularExpressionValidator-only wiring: no
// RequiredFieldValidator exists anywhere on this screen, and no validator constrains a dropdown
// value to one of its own options, so only the regex format check (and only for regex items, and
// only when a value is actually supplied) is a real rule.
public class IntendedResultsValidatorTests
{
    [Theory]
    [InlineData("^[0-9]+$", true)]
    [InlineData("^[A-Za-z]*$", true)]
    [InlineData("Positive|Negative|Inconclusive", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsRegexFormat_StartsWithCaret_IsTheOnlyRegexIndicator(string? formatString, bool expected)
    {
        Assert.Equal(expected, IntendedResultsValidator.IsRegexFormat(formatString));
    }

    [Fact]
    public void ValidateValue_DropdownFormat_NeverValidatesTheValueAgainstItsOwnOptions()
    {
        // Legacy has no validator constraining a dropdown value to one of its configured options.
        var message = IntendedResultsValidator.ValidateValue("Positive|Negative", "Result", "Not one of the options");

        Assert.Null(message);
    }

    [Fact]
    public void ValidateValue_RegexFormat_MatchingValue_IsValid()
    {
        var message = IntendedResultsValidator.ValidateValue("^[0-9]+$", "Numeric", "12345");

        Assert.Null(message);
    }

    [Fact]
    public void ValidateValue_RegexFormat_NonMatchingValue_ReturnsFormatMessage()
    {
        var message = IntendedResultsValidator.ValidateValue("^[0-9]+$", "Numeric", "abc");

        Assert.Equal("Input must be of format: Numeric", message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ValidateValue_RegexFormat_EmptyValue_IsAlwaysValid(string? value)
    {
        // Nothing is required on this screen - ASP.NET's RegularExpressionValidator never fires
        // on an empty value either.
        var message = IntendedResultsValidator.ValidateValue("^[0-9]+$", "Numeric", value);

        Assert.Null(message);
    }

    [Fact]
    public void ValidateValue_NonRegexFormat_AnyValueIsValid()
    {
        var message = IntendedResultsValidator.ValidateValue("Positive|Negative", "Result", "Anything at all");

        Assert.Null(message);
    }

    [Fact]
    public void ValidateValue_NullFormatString_IsTreatedAsNonRegex()
    {
        var message = IntendedResultsValidator.ValidateValue(null, "Result", "Anything");

        Assert.Null(message);
    }

    [Theory]
    [InlineData("01/02/2026")]
    [InlineData("31/12/2026")]
    public void ValidateValue_DateReturnType_WithDdMmYyyyValue_ReturnsNull(string value)
    {
        var message = IntendedResultsValidator.ValidateValue("^.*$", "Date", value);

        Assert.Null(message);
    }

    [Fact]
    public void ValidateValue_DateReturnType_WithInvalidDateFormat_ReturnsDdMmYyyyError()
    {
        var message = IntendedResultsValidator.ValidateValue("^.*$", "Date", "2026-12-31");

        Assert.Equal("Input must be of format: dd/MM/yyyy", message);
    }

    // Representative patterns only - the real format strings are configured per scheme in the database.
    [Theory]
    [InlineData("^.{0,12}$", "X(12)", "Positive", true)]
    [InlineData("^.{0,12}$", "X(12)", "Longer than twelve characters", false)]
    [InlineData("^[0-9]+(\\.[0-9]+)?$", "9.999", "12.345", true)]
    [InlineData("^[0-9]+(\\.[0-9]+)?$", "9.999", "12,5", false)]
    [InlineData("^(100(\\.0+)?|[0-9]{1,2}(\\.[0-9]+)?)%?$", "Percentage", "87.5%", true)]
    [InlineData("^(100(\\.0+)?|[0-9]{1,2}(\\.[0-9]+)?)%?$", "Percentage", "101%", false)]
    [InlineData("^[0-9]{1,3}/[0-9]{1,4}$", "999/9999", "1/128", true)]
    [InlineData("^[0-9]{1,3}/[0-9]{1,4}$", "999/9999", "0.5", false)]
    [InlineData("^(0[1-9]|[12][0-9]|3[01])/(0[1-9]|1[0-2])/[0-9]{4}$", "dd/mm/yyyy", "31/12/2026", true)]
    [InlineData("^(0[1-9]|[12][0-9]|3[01])/(0[1-9]|1[0-2])/[0-9]{4}$", "dd/mm/yyyy", "2026-12-31", false)]
    public void ValidateValue_ConfiguredFormat_AcceptsMatchingValuesAndNamesTheFormatOtherwise(
        string formatString, string returnValueType, string value, bool valid)
    {
        var message = IntendedResultsValidator.ValidateValue(formatString, returnValueType, value);

        Assert.Equal(valid ? null : $"Input must be of format: {returnValueType}", message);
    }
}
