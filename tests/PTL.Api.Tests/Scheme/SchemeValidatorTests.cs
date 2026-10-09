using PTL.Core.Scheme;

namespace PTL.Api.Tests.Scheme;

public class SchemeValidatorTests
{
    private static readonly Guid TestConsultantTabulationId = Guid.NewGuid();

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
        CustomsVolume = "1kg",
        // Not an assessment scheme, so a Primary Test Consultant is required.
        TestConsultant1 = Guid.NewGuid(),
        // A non-assessment scheme needs one tabulation for the Test Consultant and one that can
        // be published.
        TestConsultantTabulationId = TestConsultantTabulationId,
        Tabulations =
        [
            new PTL.Core.Scheme.SchemeTabulation { TabulationId = TestConsultantTabulationId, Name = "Test Consultant" },
            new PTL.Core.Scheme.SchemeTabulation { TabulationId = Guid.NewGuid(), Name = "Published" }
        ]
    };

    [Fact]
    public void Validate_ValidScheme_ReturnsNoErrors()
    {
        var result = SchemeValidator.Validate(ValidScheme());

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_NonAssessmentSchemeWithoutPrimaryTestConsultant_ReturnsError()
    {
        var scheme = ValidScheme();
        scheme.TestConsultant1 = null;

        var result = SchemeValidator.Validate(scheme);

        Assert.Contains(result.Errors, e => e.Field == "TestConsultant1" && e.Message == "Select a Primary Test Consultant");
    }

    [Fact]
    public void Validate_SameTestConsultantSelectedTwice_ReturnsError()
    {
        var scheme = ValidScheme();
        scheme.TestConsultant2 = scheme.TestConsultant1;

        var result = SchemeValidator.Validate(scheme);

        Assert.Contains(result.Errors, e => e.Field == "TestConsultant2" && e.Message == "The same Test Consultant cannot be selected more than once");
    }

    [Fact]
    public void Validate_AssessmentSchemeWithFewerThanTwoAssessors_ReturnsError()
    {
        var scheme = ValidScheme();
        scheme.RequiresAssessment = true;
        scheme.TestConsultant1 = null;
        scheme.Assessor1 = Guid.NewGuid();

        var result = SchemeValidator.Validate(scheme);

        Assert.Contains(result.Errors, e => e.Field == "Assessor2" && e.Message == "Select a Secondary Assessor");
        Assert.DoesNotContain(result.Errors, e => e.Field == "Assessor1");
    }

    [Fact]
    public void Validate_AssessmentSchemeWithTwoAssessors_ReturnsNoStaffingError()
    {
        var scheme = ValidScheme();
        scheme.RequiresAssessment = true;
        scheme.TestConsultant1 = null;
        scheme.Assessor1 = Guid.NewGuid();
        scheme.Assessor2 = Guid.NewGuid();

        var result = SchemeValidator.Validate(scheme);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_SameAssessorSelectedTwice_ReturnsError()
    {
        var scheme = ValidScheme();
        scheme.RequiresAssessment = true;
        scheme.TestConsultant1 = null;
        scheme.Assessor1 = Guid.NewGuid();
        scheme.Assessor2 = Guid.NewGuid();
        scheme.Assessor3 = scheme.Assessor1;

        var result = SchemeValidator.Validate(scheme);

        Assert.Contains(result.Errors, e => e.Field == "Assessor3" && e.Message == "The same Assessor cannot be selected more than once");
    }

    [Fact]
    public void Validate_AssessmentSchemeIgnoresTestConsultants()
    {
        var scheme = ValidScheme();
        scheme.RequiresAssessment = true;
        scheme.TestConsultant1 = null;
        scheme.Assessor1 = Guid.NewGuid();
        scheme.Assessor2 = Guid.NewGuid();

        var result = SchemeValidator.Validate(scheme);

        Assert.DoesNotContain(result.Errors, e => e.Field.StartsWith("TestConsultant", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_NonAssessmentSchemeWithOneTabulation_ReturnsError()
    {
        var scheme = ValidScheme();
        scheme.Tabulations = [scheme.Tabulations[0]];

        var result = SchemeValidator.Validate(scheme);

        Assert.Contains(result.Errors, e =>
            e.Field == "Tabulations" &&
            e.Message == "You must define at least two Tabulations - one for the Test Consultant and one that can be Published");
    }

    [Fact]
    public void Validate_AssessmentSchemeWithOneTabulation_ReturnsNoTabulationError()
    {
        var scheme = ValidScheme();
        scheme.RequiresAssessment = true;
        scheme.TestConsultant1 = null;
        scheme.Assessor1 = Guid.NewGuid();
        scheme.Assessor2 = Guid.NewGuid();
        scheme.Tabulations = [scheme.Tabulations[0]];

        var result = SchemeValidator.Validate(scheme);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_AssessmentSchemeWithNoTabulations_ReturnsError()
    {
        var scheme = ValidScheme();
        scheme.RequiresAssessment = true;
        scheme.TestConsultant1 = null;
        scheme.Assessor1 = Guid.NewGuid();
        scheme.Assessor2 = Guid.NewGuid();
        scheme.Tabulations = [];

        var result = SchemeValidator.Validate(scheme);

        Assert.Contains(result.Errors, e => e.Field == "Tabulations" && e.Message == "You must define at least one Tabulation");
    }

    [Fact]
    public void Validate_NoTestConsultantTabulationSelected_ReturnsError()
    {
        var scheme = ValidScheme();
        scheme.TestConsultantTabulationId = null;

        var result = SchemeValidator.Validate(scheme);

        Assert.Contains(result.Errors, e =>
            e.Field == "TestConsultantTabulationId" &&
            e.Message == "A Tabulation must be selected to send to the test consultants");
    }

    [Fact]
    public void Validate_TestConsultantTabulationNoLongerInTheList_ReturnsError()
    {
        var scheme = ValidScheme();
        scheme.TestConsultantTabulationId = Guid.NewGuid();

        var result = SchemeValidator.Validate(scheme);

        Assert.Contains(result.Errors, e => e.Field == "TestConsultantTabulationId");
    }

    [Fact]
    public void Validate_AssessmentSchemeDoesNotRequireATestConsultantTabulation()
    {
        var scheme = ValidScheme();
        scheme.RequiresAssessment = true;
        scheme.TestConsultant1 = null;
        scheme.TestConsultantTabulationId = null;
        scheme.Assessor1 = Guid.NewGuid();
        scheme.Assessor2 = Guid.NewGuid();

        var result = SchemeValidator.Validate(scheme);

        Assert.True(result.IsValid);
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
