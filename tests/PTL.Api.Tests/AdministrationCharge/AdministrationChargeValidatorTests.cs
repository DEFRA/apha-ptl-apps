using PTL.Core.AdministrationCharge;

namespace PTL.Api.Tests.AdministrationCharge;

public class AdministrationChargeValidatorTests
{
    [Fact]
    public void ValidatePrice_ZeroOrPositive_ReturnsNoErrors()
    {
        var result = AdministrationChargeValidator.ValidatePrice(0m);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidatePrice_Negative_ReturnsError()
    {
        var result = AdministrationChargeValidator.ValidatePrice(-0.01m);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "Price");
    }
}
