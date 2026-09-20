using PTL.Core.Scheme;

namespace PTL.Api.Tests.Scheme;

public class SchemeValidatorTests
{
    private static PTL.Core.Scheme.Scheme ValidScheme() => new()
    {
        YearId = 2027,
        Identifier = "PT1234",
        Name = "Test Scheme",
        ScheduleId = Guid.NewGuid(),
        ScheduleCodeId = Guid.NewGuid(),
        DistributionMonthApr = true,
        NumberOfSamples = 5,
        SampleOrigin = "UK",
        Deadline = 10,
        Instructions = "Follow the packing instructions.",
        CustomsDescription = "Biological samples",
        CustomsVolume = "1kg"
    };

    [Fact]
    public void Validate_ValidScheme_ReturnsNoErrors()
    {
        var result = SchemeValidator.Validate(ValidScheme());

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData("")]
    [InlineData("PT123")]
    [InlineData("XX1234")]
    [InlineData("PT12345")]
    public void Validate_InvalidIdentifier_ReturnsError(string identifier)
    {
        var scheme = ValidScheme();
        scheme.Identifier = identifier;

        var result = SchemeValidator.Validate(scheme);

        Assert.Contains(result.Errors, e => e.Field == "Identifier");
    }

    [Fact]
    public void Validate_EmptyName_ReturnsError()
    {
        var scheme = ValidScheme();
        scheme.Name = string.Empty;

        var result = SchemeValidator.Validate(scheme);

        Assert.Contains(result.Errors, e => e.Field == "Name");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1000)]
    public void Validate_DeadlineOutOfRange_ReturnsError(int deadline)
    {
        var scheme = ValidScheme();
        scheme.Deadline = deadline;

        var result = SchemeValidator.Validate(scheme);

        Assert.Contains(result.Errors, e => e.Field == "Deadline");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1000)]
    public void Validate_NumberOfSamplesOutOfRange_ReturnsError(int numberOfSamples)
    {
        var scheme = ValidScheme();
        scheme.NumberOfSamples = numberOfSamples;

        var result = SchemeValidator.Validate(scheme);

        Assert.Contains(result.Errors, e => e.Field == "NumberOfSamples");
    }

    [Fact]
    public void Validate_NoDistributionMonthsAndNotAsAvailable_ReturnsError()
    {
        var scheme = ValidScheme();
        scheme.DistributionMonthApr = false;
        scheme.DistributionAsAvailable = false;

        var result = SchemeValidator.Validate(scheme);

        Assert.Contains(result.Errors, e => e.Field == "DistributionAsAvailable");
    }

    [Fact]
    public void Validate_DistributionMonthAndAsAvailableBothSet_ReturnsError()
    {
        var scheme = ValidScheme();
        scheme.DistributionMonthApr = true;
        scheme.DistributionAsAvailable = true;

        var result = SchemeValidator.Validate(scheme);

        Assert.Contains(result.Errors, e => e.Field == "DistributionAsAvailable");
    }

    [Fact]
    public void Validate_AsAvailableOnly_ReturnsNoDistributionError()
    {
        var scheme = ValidScheme();
        scheme.DistributionMonthApr = false;
        scheme.DistributionAsAvailable = true;

        var result = SchemeValidator.Validate(scheme);

        Assert.DoesNotContain(result.Errors, e => e.Field == "DistributionAsAvailable");
    }

    [Fact]
    public void Validate_ConsentActiveWithoutText_ReturnsError()
    {
        var scheme = ValidScheme();
        scheme.DataConsentDeclarationActive = true;
        scheme.DataConsentDeclarationText = null;

        var result = SchemeValidator.Validate(scheme);

        Assert.Contains(result.Errors, e => e.Field == "DataConsentDeclarationText");
    }

    [Fact]
    public void Validate_ConsentActiveWithText_ReturnsNoConsentError()
    {
        var scheme = ValidScheme();
        scheme.DataConsentDeclarationActive = true;
        scheme.DataConsentDeclarationText = "I consent";

        var result = SchemeValidator.Validate(scheme);

        Assert.DoesNotContain(result.Errors, e => e.Field == "DataConsentDeclarationText");
    }

    [Fact]
    public void Validate_ConsentInactive_DoesNotRequireText()
    {
        var scheme = ValidScheme();
        scheme.DataConsentDeclarationActive = false;
        scheme.DataConsentDeclarationText = null;

        var result = SchemeValidator.Validate(scheme);

        Assert.DoesNotContain(result.Errors, e => e.Field == "DataConsentDeclarationText");
    }

    [Fact]
    public void Validate_EmptyInstructions_ReturnsError()
    {
        var scheme = ValidScheme();
        scheme.Instructions = string.Empty;

        var result = SchemeValidator.Validate(scheme);

        Assert.Contains(result.Errors, e => e.Field == "Instructions");
    }

    [Fact]
    public void Validate_EmptyCustomsDescription_ReturnsError()
    {
        var scheme = ValidScheme();
        scheme.CustomsDescription = null;

        var result = SchemeValidator.Validate(scheme);

        Assert.Contains(result.Errors, e => e.Field == "CustomsDescription");
    }

    [Fact]
    public void Validate_EmptyCustomsVolume_ReturnsError()
    {
        var scheme = ValidScheme();
        scheme.CustomsVolume = null;

        var result = SchemeValidator.Validate(scheme);

        Assert.Contains(result.Errors, e => e.Field == "CustomsVolume");
    }
}
