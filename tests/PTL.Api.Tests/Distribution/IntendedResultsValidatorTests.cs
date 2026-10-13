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
}
