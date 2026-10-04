using PTL.Core.Participant;

namespace PTL.Api.Tests.Participant;

public class ParticipantSchemeValidatorTests
{
    private static ParticipantSchemeRecord ValidRecord() => new()
    {
        ParticipantSchemeId = Guid.NewGuid(),
        ContractId = Guid.NewGuid(),
        ParticipantId = Guid.NewGuid(),
        SchemeId = Guid.NewGuid(),
        NumberOfSetsRequired = 1,
        DistributionMonthJan = true
    };

    [Fact]
    public void Validate_ValidRecord_ReturnsNoErrors()
    {
        var result = ParticipantSchemeValidator.Validate(ValidRecord());

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData("SchemeId")]
    [InlineData("ParticipantId")]
    [InlineData("ContractId")]
    public void Validate_MissingSelection_ReturnsError(string field)
    {
        var record = ValidRecord();
        typeof(ParticipantSchemeRecord).GetProperty(field)!.SetValue(record, Guid.Empty);

        var result = ParticipantSchemeValidator.Validate(record);

        Assert.Contains(result.Errors, e => e.Field == field);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_NumberOfSetsRequiredBelowOne_ReturnsError(int numberOfSets)
    {
        var record = ValidRecord();
        record.NumberOfSetsRequired = numberOfSets;

        var result = ParticipantSchemeValidator.Validate(record);

        Assert.Contains(result.Errors, e => e.Field == "NumberOfSetsRequired"
            && e.Message.Contains("must be at least 1", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_NoDistributionMonthSelected_ReturnsLegacyError()
    {
        var record = ValidRecord();
        record.DistributionMonthJan = false;

        var result = ParticipantSchemeValidator.Validate(record);

        Assert.Contains(result.Errors, e => e.Field == "DistributionMonthJan"
            && e.Message == "Distribution must have at least one selected month");
    }

    [Theory]
    [InlineData("ExternalReference", 51)]
    [InlineData("Contact", 101)]
    [InlineData("PackingInstructions", 2001)]
    public void Validate_FieldExceedingMaxLength_ReturnsError(string field, int length)
    {
        var record = ValidRecord();
        typeof(ParticipantSchemeRecord).GetProperty(field)!.SetValue(record, new string('x', length));

        var result = ParticipantSchemeValidator.Validate(record);

        Assert.Contains(result.Errors, e => e.Field == field
            && e.Message.Contains("must not exceed", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_AnyDistributionMonthSelected_IsAccepted()
    {
        var record = ValidRecord();
        record.DistributionMonthJan = false;
        record.DistributionMonthDec = true;

        var result = ParticipantSchemeValidator.Validate(record);

        Assert.True(result.IsValid);
    }
}
