using PTL.Core.PostagePricingPlan;

namespace PTL.Api.Tests.PostagePricingPlan;

public class PostagePricingPlanValidatorTests
{
    [Fact]
    public void Validate_AllNonNegative_ReturnsNoErrors()
    {
        var result = PostagePricingPlanValidator.Validate(0m, 0m, 0m);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_NegativeUKPrice_ReturnsUKPriceError()
    {
        var result = PostagePricingPlanValidator.Validate(-0.01m, 1m, 1m);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "UKPrice");
    }

    [Fact]
    public void Validate_NegativeEUPrice_ReturnsEUPriceError()
    {
        var result = PostagePricingPlanValidator.Validate(1m, -0.01m, 1m);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "EUPrice");
    }

    [Fact]
    public void Validate_NegativeNonEUPrice_ReturnsNonEUPriceError()
    {
        var result = PostagePricingPlanValidator.Validate(1m, 1m, -0.01m);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "NonEUPrice");
    }

    [Fact]
    public void Validate_AllNegative_ReturnsAllThreeErrors()
    {
        var result = PostagePricingPlanValidator.Validate(-1m, -1m, -1m);

        Assert.False(result.IsValid);
        Assert.Equal(3, result.Errors.Count);
    }
}
