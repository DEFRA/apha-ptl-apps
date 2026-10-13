using PTL.Core.ExternalSiteMessage;

namespace PTL.Api.Tests.ExternalSiteMessage;

public class ExternalSiteMessageValidatorTests
{
    [Fact]
    public void Validate_ValidImportantMessageAndEmail_IsValid()
    {
        var result = ExternalSiteMessageValidator.Validate("<p>Short notice</p>", "vetqas@apha.gov.uk");

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_ImportantMessageClearedEmpty_IsValid()
    {
        var result = ExternalSiteMessageValidator.Validate(string.Empty, "vetqas@apha.gov.uk");

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ImportantMessageOver500VisibleCharacters_ReturnsError()
    {
        var result = ExternalSiteMessageValidator.Validate(new string('a', 501), "vetqas@apha.gov.uk");

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "ImportantMessage" && e.Message == "The Important Message cannot exceed 500 characters.");
    }

    // The raw HTML is well over 500 characters once markup is included, but the VISIBLE text is
    // only "Bold text here" (14 characters) - must not be rejected, matching legacy's
    // StripStyling.CountVisibleCharacters behaviour rather than a naive raw-length check.
    [Fact]
    public void Validate_ImportantMessageWithHeavyMarkupButShortVisibleText_IsValid()
    {
        var html = string.Concat(Enumerable.Repeat("<span style=\"color:red\">", 40)) + "Bold text here" + string.Concat(Enumerable.Repeat("</span>", 40));

        var result = ExternalSiteMessageValidator.Validate(html, "vetqas@apha.gov.uk");

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_MissingEmail_ReturnsRequiredError()
    {
        var result = ExternalSiteMessageValidator.Validate(string.Empty, string.Empty);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "SupportEmailAddress" && e.Message == "The Email Address is required");
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("missing@domain")]
    [InlineData("@noLocalPart.com")]
    public void Validate_InvalidEmailFormat_ReturnsInvalidError(string email)
    {
        var result = ExternalSiteMessageValidator.Validate(string.Empty, email);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "SupportEmailAddress" && e.Message == "The Email Address is invalid");
    }

    [Fact]
    public void Validate_EmailWithApostrophe_IsValid()
    {
        // The shared legacy regex (GlobalResources.EmailRegEx) explicitly allows an apostrophe in
        // the local part, unlike the different regex hardcoded on ManageExternalTCs/ManageViewers.
        var result = ExternalSiteMessageValidator.Validate(string.Empty, "o'brien@apha.gov.uk");

        Assert.True(result.IsValid);
    }

    [Fact]
    public void CountVisibleCharacters_StripsTagsDecodesEntitiesAndTrims()
    {
        var count = ExternalSiteMessageValidator.CountVisibleCharacters("<p>Hello&nbsp;&amp;&nbsp;welcome</p>  ");

        Assert.Equal("Hello & welcome".Length, count);
    }

    [Fact]
    public void CountVisibleCharacters_EmptyInput_ReturnsZero()
    {
        Assert.Equal(0, ExternalSiteMessageValidator.CountVisibleCharacters(string.Empty));
    }
}
