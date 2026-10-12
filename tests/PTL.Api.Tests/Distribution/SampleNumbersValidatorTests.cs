using PTL.Core.Distribution;

namespace PTL.Api.Tests.Distribution;

// Pins the legacy CSLA AddBusinessRules port (IntendedResults.Sample / IntendedResultsSet) -
// message text and boundary lengths are asserted verbatim because legacy shows them to the user.
public class SampleNumbersValidatorTests
{
    private static SampleNumberRowUpdate Sample(
        string sampleNumber = "S1",
        string internalDescription = "Internal",
        string externalDescription = "",
        string tabulationDescription = "",
        string testConsultantsDescription = "") =>
        new(Guid.NewGuid(), sampleNumber, internalDescription, externalDescription, tabulationDescription, testConsultantsDescription, []);

    [Fact]
    public void ValidateSample_AllFieldsWithinLimits_ReturnsNoErrors()
    {
        var errors = SampleNumbersValidator.ValidateSample(Sample());

        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateSample_MissingSampleNumber_ReturnsLegacyRequiredMessage(string sampleNumber)
    {
        var errors = SampleNumbersValidator.ValidateSample(Sample(sampleNumber: sampleNumber));

        var error = Assert.Single(errors, e => e.Field == SampleNumbersValidator.SampleNumber);
        Assert.Equal("Enter the Sample Number", error.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateSample_MissingInternalDescription_ReturnsLegacyRequiredMessage(string internalDescription)
    {
        var errors = SampleNumbersValidator.ValidateSample(Sample(internalDescription: internalDescription));

        var error = Assert.Single(errors, e => e.Field == SampleNumbersValidator.InternalDescription);
        Assert.Equal("Enter the Internal Description", error.Message);
    }

    [Fact]
    public void ValidateSample_SampleNumberExactlyAtLimit_IsValid()
    {
        var errors = SampleNumbersValidator.ValidateSample(Sample(sampleNumber: new string('x', 50)));

        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateSample_SampleNumberOneOverLimit_ReturnsMaxLengthError()
    {
        var errors = SampleNumbersValidator.ValidateSample(Sample(sampleNumber: new string('x', 51)));

        var error = Assert.Single(errors);
        Assert.Equal(SampleNumbersValidator.SampleNumber, error.Field);
        Assert.Equal("Sample Number must not exceed 50 characters", error.Message);
    }

    [Fact]
    public void ValidateSample_InternalDescriptionExactlyAtLimit_IsValid()
    {
        var errors = SampleNumbersValidator.ValidateSample(Sample(internalDescription: new string('x', 100)));

        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateSample_InternalDescriptionOneOverLimit_ReturnsMaxLengthError()
    {
        var errors = SampleNumbersValidator.ValidateSample(Sample(internalDescription: new string('x', 101)));

        var error = Assert.Single(errors);
        Assert.Equal(SampleNumbersValidator.InternalDescription, error.Field);
        Assert.Equal("Internal Description must not exceed 100 characters", error.Message);
    }

    [Fact]
    public void ValidateSample_DescriptionsExactlyAtLimit_AreValid()
    {
        var atLimit = new string('x', 500);

        var errors = SampleNumbersValidator.ValidateSample(Sample(
            externalDescription: atLimit, tabulationDescription: atLimit, testConsultantsDescription: atLimit));

        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateSample_ExternalDescriptionOneOverLimit_UsesTestInstructionMessage()
    {
        var errors = SampleNumbersValidator.ValidateSample(Sample(externalDescription: new string('x', 501)));

        var error = Assert.Single(errors);
        Assert.Equal(SampleNumbersValidator.ExternalDescription, error.Field);
        Assert.Equal("Test Instruction must not exceed 500 characters", error.Message);
    }

    [Fact]
    public void ValidateSample_TabulationDescriptionOneOverLimit_ReturnsMaxLengthError()
    {
        var errors = SampleNumbersValidator.ValidateSample(Sample(tabulationDescription: new string('x', 501)));

        var error = Assert.Single(errors);
        Assert.Equal(SampleNumbersValidator.TabulationDescription, error.Field);
        Assert.Equal("Tabulation Description must not exceed 500 characters", error.Message);
    }

    [Fact]
    public void ValidateSample_TestConsultantsDescriptionOneOverLimit_UsesDescriptionForTcMessage()
    {
        var errors = SampleNumbersValidator.ValidateSample(Sample(testConsultantsDescription: new string('x', 501)));

        var error = Assert.Single(errors);
        Assert.Equal(SampleNumbersValidator.TestConsultantsDescription, error.Field);
        Assert.Equal("Description for TC must not exceed 500 characters", error.Message);
    }

    [Fact]
    public void ValidateSample_MultipleBrokenRules_ReturnsEveryError()
    {
        var errors = SampleNumbersValidator.ValidateSample(Sample(
            sampleNumber: string.Empty,
            internalDescription: new string('x', 101),
            externalDescription: new string('x', 501)));

        Assert.Equal(3, errors.Count);
        Assert.Contains(errors, e => e.Field == SampleNumbersValidator.SampleNumber);
        Assert.Contains(errors, e => e.Field == SampleNumbersValidator.InternalDescription);
        Assert.Contains(errors, e => e.Field == SampleNumbersValidator.ExternalDescription);
    }

    [Fact]
    public void ValidateComments_NullOrWithinLimit_ReturnsNoErrors()
    {
        Assert.Empty(SampleNumbersValidator.ValidateComments(null));
        Assert.Empty(SampleNumbersValidator.ValidateComments(string.Empty));
        Assert.Empty(SampleNumbersValidator.ValidateComments(new string('x', 8000)));
    }

    [Fact]
    public void ValidateComments_OneOverLimit_ReturnsMaxLengthError()
    {
        var errors = SampleNumbersValidator.ValidateComments(new string('x', 8001));

        var error = Assert.Single(errors);
        Assert.Equal(SampleNumbersValidator.Comments, error.Field);
        Assert.Equal("Comments must not exceed 8000 characters", error.Message);
    }

    [Fact]
    public void MaxLengthConstants_MatchLegacyColumnWidths()
    {
        Assert.Equal(50, SampleNumbersValidator.SampleNumberMaxLength);
        Assert.Equal(100, SampleNumbersValidator.InternalDescriptionMaxLength);
        Assert.Equal(500, SampleNumbersValidator.DescriptionMaxLength);
        Assert.Equal(8000, SampleNumbersValidator.CommentsMaxLength);
    }

    // Legacy SampleNumbers.aspx has a RequiredFieldValidator on Sample No. and Internal
    // Description and on nothing else, so the migration must not make any other field mandatory.
    [Fact]
    public void ValidateSample_OnlySampleNumberAndInternalDescriptionAreRequired()
    {
        var errors = SampleNumbersValidator.ValidateSample(Sample(
            externalDescription: string.Empty,
            tabulationDescription: string.Empty,
            testConsultantsDescription: string.Empty));

        Assert.Empty(errors);
    }

    [Theory]
    [InlineData(SampleNumbersValidator.ExternalDescription)]
    [InlineData(SampleNumbersValidator.TabulationDescription)]
    [InlineData(SampleNumbersValidator.TestConsultantsDescription)]
    public void ValidateSample_BlankOptionalField_ProducesNoRequiredError(string field)
    {
        var errors = SampleNumbersValidator.ValidateSample(Sample(
            externalDescription: "   ", tabulationDescription: "   ", testConsultantsDescription: "   "));

        Assert.DoesNotContain(errors, e => e.Field == field);
    }

    [Fact]
    public void ValidateComments_Blank_IsNotRequired()
    {
        Assert.Empty(SampleNumbersValidator.ValidateComments("   "));
    }

    // An omitted JSON member binds as null, which must behave exactly like an empty string.
    [Fact]
    public void ValidateSample_NullOptionalDescriptions_AreTreatedAsEmpty()
    {
        var sample = new SampleNumberRowUpdate(Guid.NewGuid(), "S1", "Internal", null!, null!, null!, []);

        Assert.Empty(SampleNumbersValidator.ValidateSample(sample));
    }

    [Fact]
    public void ValidateSample_NullRequiredFields_ReportTheLegacyRequiredMessages()
    {
        var sample = new SampleNumberRowUpdate(Guid.NewGuid(), null!, null!, string.Empty, string.Empty, string.Empty, []);

        var errors = SampleNumbersValidator.ValidateSample(sample);

        Assert.Equal(2, errors.Count);
        Assert.Contains(errors, e => e.Field == SampleNumbersValidator.SampleNumber && e.Message == "Enter the Sample Number");
        Assert.Contains(errors, e => e.Field == SampleNumbersValidator.InternalDescription && e.Message == "Enter the Internal Description");
    }
}
