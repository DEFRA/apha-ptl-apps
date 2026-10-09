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

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    public void RestoreLockedMonths_EachLockedMonth_KeepsTheExistingValue(int monthNumber)
    {
        var editability = LockMonth(monthNumber);
        var posted = CoreSchemeWithMonth(monthNumber, true);
        var existing = CoreSchemeWithMonth(monthNumber, false);

        SchemeDistributionMonths.RestoreLockedMonths(posted, editability, existing);

        Assert.False(MonthValue(posted, monthNumber));
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    [InlineData(4, true)]
    [InlineData(5, true)]
    [InlineData(6, true)]
    [InlineData(7, true)]
    [InlineData(8, true)]
    [InlineData(9, true)]
    [InlineData(10, true)]
    [InlineData(11, true)]
    [InlineData(12, true)]
    [InlineData(0, true)]
    public void CanEdit_EveryMonth_ReadsItsOwnFlag(int monthNumber, bool expected)
    {
        var editability = SchemeMonthEditability.AllEditable;

        Assert.Equal(expected, editability.CanEdit(monthNumber));
    }

    [Fact]
    public void CanEdit_LockedMonth_ReturnsFalse()
    {
        var editability = SchemeMonthEditability.AllEditable with { Jun = false };

        Assert.False(editability.CanEdit(6));
    }

    private static SchemeMonthEditability LockMonth(int monthNumber) => monthNumber switch
    {
        1 => SchemeMonthEditability.AllEditable with { Jan = false },
        2 => SchemeMonthEditability.AllEditable with { Feb = false },
        3 => SchemeMonthEditability.AllEditable with { Mar = false },
        4 => SchemeMonthEditability.AllEditable with { Apr = false },
        5 => SchemeMonthEditability.AllEditable with { May = false },
        6 => SchemeMonthEditability.AllEditable with { Jun = false },
        7 => SchemeMonthEditability.AllEditable with { Jul = false },
        8 => SchemeMonthEditability.AllEditable with { Aug = false },
        9 => SchemeMonthEditability.AllEditable with { Sep = false },
        10 => SchemeMonthEditability.AllEditable with { Oct = false },
        11 => SchemeMonthEditability.AllEditable with { Nov = false },
        12 => SchemeMonthEditability.AllEditable with { Dec = false },
        _ => throw new ArgumentOutOfRangeException(nameof(monthNumber)),
    };

    private static CoreScheme CoreSchemeWithMonth(int monthNumber, bool value) => monthNumber switch
    {
        1 => new CoreScheme { DistributionMonthJan = value },
        2 => new CoreScheme { DistributionMonthFeb = value },
        3 => new CoreScheme { DistributionMonthMar = value },
        4 => new CoreScheme { DistributionMonthApr = value },
        5 => new CoreScheme { DistributionMonthMay = value },
        6 => new CoreScheme { DistributionMonthJun = value },
        7 => new CoreScheme { DistributionMonthJul = value },
        8 => new CoreScheme { DistributionMonthAug = value },
        9 => new CoreScheme { DistributionMonthSep = value },
        10 => new CoreScheme { DistributionMonthOct = value },
        11 => new CoreScheme { DistributionMonthNov = value },
        12 => new CoreScheme { DistributionMonthDec = value },
        _ => throw new ArgumentOutOfRangeException(nameof(monthNumber)),
    };

    private static bool MonthValue(CoreScheme scheme, int monthNumber) => monthNumber switch
    {
        1 => scheme.DistributionMonthJan,
        2 => scheme.DistributionMonthFeb,
        3 => scheme.DistributionMonthMar,
        4 => scheme.DistributionMonthApr,
        5 => scheme.DistributionMonthMay,
        6 => scheme.DistributionMonthJun,
        7 => scheme.DistributionMonthJul,
        8 => scheme.DistributionMonthAug,
        9 => scheme.DistributionMonthSep,
        10 => scheme.DistributionMonthOct,
        11 => scheme.DistributionMonthNov,
        12 => scheme.DistributionMonthDec,
        _ => throw new ArgumentOutOfRangeException(nameof(monthNumber)),
    };

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
