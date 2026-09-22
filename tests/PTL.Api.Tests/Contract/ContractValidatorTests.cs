using PTL.Core.Contract;

namespace PTL.Api.Tests.Contract;

public class ContractValidatorTests
{
    private static PTL.Core.Contract.Contract ValidContract() => new()
    {
        CustomerId = Guid.NewGuid(),
        YearId = 2027,
        UTNumber = "UT12345",
        ContractSignatory = "Alice Example",
        AcknowledgementPostedDate = new DateTime(2026, 1, 1),
        AcknowledgementReturnedDate = new DateTime(2026, 1, 5),
        JobSheetPostedDate = new DateTime(2026, 1, 10),
        DateOfLeaving = new DateTime(2026, 12, 31),
        IsActive = true,
        Suffix = "A"
    };

    [Fact]
    public void Validate_ValidContract_ReturnsNoErrors()
    {
        var result = ContractValidator.Validate(ValidContract());

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_EmptyCustomerId_ReturnsError()
    {
        var contract = ValidContract();
        contract.CustomerId = Guid.Empty;

        var result = ContractValidator.Validate(contract);

        Assert.Contains(result.Errors, e => e.Field == "CustomerId");
    }

    [Fact]
    public void Validate_ZeroYearId_ReturnsError()
    {
        var contract = ValidContract();
        contract.YearId = 0;

        var result = ContractValidator.Validate(contract);

        Assert.Contains(result.Errors, e => e.Field == "YearId");
    }

    [Theory]
    [InlineData("UT12345", "FT12345")]
    [InlineData("", "")]
    public void Validate_UtAndFtNotMutuallyExclusive_ReturnsError(string ut, string ft)
    {
        var contract = ValidContract();
        contract.UTNumber = ut;
        contract.FTNumber = ft;

        var result = ContractValidator.Validate(contract);

        Assert.Contains(result.Errors, e => e.Field == "UTNumber");
    }

    [Fact]
    public void Validate_NegativeDiscountRate_ReturnsError()
    {
        var contract = ValidContract();
        contract.DiscountRate = -0.1m;

        var result = ContractValidator.Validate(contract);

        Assert.Contains(result.Errors, e => e.Field == "DiscountRate");
    }

    [Fact]
    public void Validate_SentinelAcknowledgementPostedDate_ReturnsError()
    {
        var contract = ValidContract();
        contract.AcknowledgementPostedDate = new DateTime(9999, 1, 1);

        var result = ContractValidator.Validate(contract);

        Assert.Contains(result.Errors, e => e.Field == "AcknowledgementPostedDate");
    }

    [Fact]
    public void Validate_SuffixTooLong_ReturnsError()
    {
        var contract = ValidContract();
        contract.Suffix = "ABC";

        var result = ContractValidator.Validate(contract);

        Assert.Contains(result.Errors, e => e.Field == "Suffix");
    }
}
