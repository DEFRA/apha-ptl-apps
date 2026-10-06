using PTL.Core.Lookup;
using PTL.Core.Scheme;
using CoreScheme = PTL.Core.Scheme.Scheme;

namespace PTL.Api.Tests.Scheme;

// Legacy Scheme.SetEditPermissions / dbo.fnIsDistributionNotDefined: a scheme year starts at the
// contract start month, so months before it belong to the following calendar year.
public class SchemeDistributionMonthsTests
{
    private const int AprilContractStart = 4;

    [Fact]
    public void CalculateEditability_NoDistributions_LeavesEveryMonthEditable()
    {
        var editability = SchemeDistributionMonths.CalculateEditability(2026, AprilContractStart, []);

        Assert.Equal(SchemeMonthEditability.AllEditable, editability);
    }

    [Fact]
    public void CalculateEditability_DistributionInSchemeYear_LocksThatMonth()
    {
        var editability = SchemeDistributionMonths.CalculateEditability(
            2026,
            AprilContractStart,
            [new MonthlyDistributionEntity { YearId = 2026, MonthId = 6 }]);

        Assert.False(editability.Jun);
        Assert.True(editability.May);
        Assert.True(editability.Jul);
    }

    [Fact]
    public void CalculateEditability_DistributionBeforeContractStartMonth_BelongsToTheFollowingYear()
    {
        var editability = SchemeDistributionMonths.CalculateEditability(
            2026,
            AprilContractStart,
            [new MonthlyDistributionEntity { YearId = 2027, MonthId = 2 }]);

        Assert.False(editability.Feb);
    }

    [Theory]
    [InlineData(2026, 2)] // February of the scheme year itself is outside its financial year.
    [InlineData(2027, 6)] // June of the following year is outside it too.
    [InlineData(2025, 6)] // So is anything in an earlier year.
    public void CalculateEditability_DistributionOutsideTheSchemeYear_LeavesMonthsEditable(int distributionYearId, int monthId)
    {
        var editability = SchemeDistributionMonths.CalculateEditability(
            2026,
            AprilContractStart,
            [new MonthlyDistributionEntity { YearId = distributionYearId, MonthId = monthId }]);

        Assert.Equal(SchemeMonthEditability.AllEditable, editability);
    }

    [Fact]
    public void RestoreLockedMonths_OnUpdate_KeepsTheExistingValueForALockedMonth()
    {
        var editability = SchemeMonthEditability.AllEditable with { Jun = false };
        var posted = new CoreScheme { DistributionMonthJun = true, DistributionMonthJul = true };
        var existing = new CoreScheme { DistributionMonthJun = false, DistributionMonthJul = false };

        SchemeDistributionMonths.RestoreLockedMonths(posted, editability, existing);

        Assert.False(posted.DistributionMonthJun);
        Assert.True(posted.DistributionMonthJul);
    }

    [Fact]
    public void RestoreLockedMonths_OnCreate_ClearsALockedMonth()
    {
        var editability = SchemeMonthEditability.AllEditable with { Jun = false };
        var posted = new CoreScheme { DistributionMonthJun = true };

        SchemeDistributionMonths.RestoreLockedMonths(posted, editability, existing: null);

        Assert.False(posted.DistributionMonthJun);
    }

    [Fact]
    public void ApplyEditability_CopiesEveryFlagOntoTheScheme()
    {
        var scheme = new CoreScheme();

        SchemeDistributionMonths.ApplyEditability(scheme, SchemeMonthEditability.AllEditable with { Jan = false, Dec = false });

        Assert.False(scheme.CanEditJan);
        Assert.False(scheme.CanEditDec);
        Assert.True(scheme.CanEditJun);
    }
}
