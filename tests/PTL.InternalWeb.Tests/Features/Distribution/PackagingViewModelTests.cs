using PTL.Contracts.Distribution;
using PTL.InternalWeb.Features.Distribution;

namespace PTL.InternalWeb.Tests.Features.Distribution;

public class PackagingViewModelTests
{
    private static readonly Guid Dp1 = Guid.Parse("d0000000-0000-0000-0000-0000000000d1");
    private static readonly Guid Dp2 = Guid.Parse("d0000000-0000-0000-0000-0000000000d2");
    private static readonly Guid Dp3 = Guid.Parse("d0000000-0000-0000-0000-0000000000d3");

    private static PackagingParticipantResponse Participant(Guid id) => new(id, "Lab", null, null, "N/A");

    private static PackagingSchemeResponse Scheme(Guid? ownRow, params Guid[] participants) =>
        new(
            Guid.NewGuid(), "Scheme", true, string.Empty, string.Empty, string.Empty, "N/A", ownRow, null, null, 0, 0,
            [.. participants.Select(Participant)]);

    private static PackagingSheetResponse SheetOf(int mode, int week, params PackagingSchemeResponse[] schemes) =>
        new(mode, week, 2026, 5, [new PackagingBlockResponse("key", new DateTime(2026, 5, 13), "Heading", string.Empty, false, false, false, schemes)]);

    [Fact]
    public void Defaults_AreAnEmptyUnselectedPage()
    {
        var model = new PackagingViewModel();

        Assert.Null(model.Mode);
        Assert.Equal(0, model.Week);
        Assert.Empty(model.PackedTicks);
        Assert.Empty(model.PostedTicks);
        Assert.Empty(model.PrintKeys);
        Assert.Null(model.Sheet);
        Assert.Null(model.Message);
        Assert.Null(model.MessageDetail);
        Assert.Equal(string.Empty, model.PrintTitle);
        Assert.Equal(string.Empty, model.MonthDescription);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(3, false)]
    public void ShowWeek_OnlyForGroupAddressAndParticipantCombined(int? mode, bool expected) =>
        Assert.Equal(expected, new PackagingViewModel { Mode = mode }.ShowWeek);

    [Theory]
    [InlineData(null, 0, false)]
    [InlineData(null, 2, false)]
    [InlineData(0, 0, false)]
    [InlineData(0, 3, false)]
    [InlineData(1, 0, false)]
    [InlineData(1, 1, true)]
    [InlineData(1, 4, true)]
    [InlineData(2, 0, true)]
    [InlineData(2, 3, true)]
    [InlineData(3, 1, false)]
    public void ShowCustoms_ParticipantCombinedNeedsAWeekAndDailyAlwaysQualifies(int? mode, int week, bool expected) =>
        Assert.Equal(expected, new PackagingViewModel { Mode = mode, Week = week }.ShowCustoms);

    [Fact]
    public void TickKey_IsModeWeekAndParticipantRowSeparatedByColons() =>
        Assert.Equal($"1:3:{Dp1}", PackagingViewModel.TickKey(1, 3, Dp1));

    [Fact]
    public void RowTickKey_UsesTheModeAndWeekOfTheSheetOnScreen()
    {
        var model = new PackagingViewModel { Mode = 0, Week = 1, Sheet = SheetOf(1, 4) };

        Assert.Equal($"1:4:{Dp1}", model.RowTickKey(Dp1));
    }

    [Fact]
    public void RowTickKey_WithNoSheetFallsBackToModeZeroWeekZero()
    {
        Assert.Equal($"0:0:{Dp1}", new PackagingViewModel().RowTickKey(Dp1));
    }

    [Fact]
    public void IsPacked_IsTrueOnlyForATickOnTheSheetOnScreen()
    {
        var model = new PackagingViewModel
        {
            Sheet = SheetOf(1, 2),
            PackedTicks = [$"1:2:{Dp1}", $"0:2:{Dp2}"],
        };

        Assert.True(model.IsPacked(Dp1));
        Assert.False(model.IsPacked(Dp2));
        Assert.False(model.IsPacked(Dp3));
    }

    [Fact]
    public void IsPosted_IsTrueOnlyForATickOnTheSheetOnScreenAndIsIndependentOfPacked()
    {
        var model = new PackagingViewModel
        {
            Sheet = SheetOf(2, 0),
            PostedTicks = [$"2:0:{Dp1}"],
            PackedTicks = [$"2:0:{Dp2}"],
        };

        Assert.True(model.IsPosted(Dp1));
        Assert.False(model.IsPosted(Dp2));
        Assert.True(model.IsPacked(Dp2));
        Assert.False(model.IsPacked(Dp1));
    }

    [Fact]
    public void CarriedTicks_KeepsOnlyTicksForRowsNotOnTheSheetOnScreen()
    {
        var model = new PackagingViewModel
        {
            Sheet = SheetOf(0, 1, Scheme(null, Dp1, Dp2)),
        };

        var carried = model.CarriedTicks([$"0:1:{Dp1}", $"0:1:{Dp2}", $"1:2:{Dp3}", $"0:2:{Dp1}"]).ToList();

        Assert.Equal([$"1:2:{Dp3}", $"0:2:{Dp1}"], carried);
    }

    [Fact]
    public void CarriedTicks_ParticipantCombinedRowsAreMatchedThroughTheSchemesOwnRow()
    {
        var model = new PackagingViewModel
        {
            Sheet = SheetOf(1, 3, Scheme(Dp1), Scheme(Dp2)),
        };

        var carried = model.CarriedTicks([$"1:3:{Dp1}", $"1:3:{Dp2}", $"1:3:{Dp3}"]).ToList();

        Assert.Equal([$"1:3:{Dp3}"], carried);
    }

    [Fact]
    public void CarriedTicks_RemovesDuplicates()
    {
        var model = new PackagingViewModel { Sheet = SheetOf(0, 1) };

        var carried = model.CarriedTicks([$"0:2:{Dp1}", $"0:2:{Dp1}", $"0:3:{Dp1}"]).ToList();

        Assert.Equal([$"0:2:{Dp1}", $"0:3:{Dp1}"], carried);
    }

    [Fact]
    public void CarriedTicks_WithNoSheetCarriesEverything()
    {
        var model = new PackagingViewModel();

        var carried = model.CarriedTicks([$"0:1:{Dp1}", $"2:0:{Dp2}"]).ToList();

        Assert.Equal([$"0:1:{Dp1}", $"2:0:{Dp2}"], carried);
    }

    [Fact]
    public void CarriedTicks_WithNoTicksIsEmpty() =>
        Assert.Empty(new PackagingViewModel { Sheet = SheetOf(0, 1, Scheme(Dp1)) }.CarriedTicks([]));

    [Fact]
    public void PackagingLabelsViewModel_Defaults()
    {
        var model = new PackagingLabelsViewModel();

        Assert.Equal(0, model.YearId);
        Assert.Equal(0, model.MonthId);
        Assert.Equal(0, model.Mode);
        Assert.Equal(0, model.Week);
        Assert.Equal(string.Empty, model.MonthDescription);
        Assert.Empty(model.Labels);
    }
}
