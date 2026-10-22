using PTL.Contracts.Distribution;
using PTL.InternalWeb.Features.TestConsultant;
using static PTL.InternalWeb.Tests.TestSupport.TestConsultantTestData;

namespace PTL.InternalWeb.Tests.Features.TestConsultant;

public class TestConsultantViewModelTests
{
    // ---- Menu ---------------------------------------------------------------------------------

    private static TestConsultantDistributionsResponse Distributions(params TestConsultantDistributionResponse[] distributions) => new(distributions);

    [Fact]
    public void From_NullResponse_Throws() => Assert.Throws<ArgumentNullException>(() => TestConsultantMenuViewModel.From(null!, null));

    [Fact]
    public void From_NoDistributions_HasNoYearsOrMonths()
    {
        var model = TestConsultantMenuViewModel.From(Distributions(), 2026);

        Assert.Empty(model.Years);
        Assert.Empty(model.Months);
        Assert.Equal(0, model.SelectedYear);
    }

    [Fact]
    public void From_ListsYearsOldestFirstAndSelectsTheLatest()
    {
        var model = TestConsultantMenuViewModel.From(
            Distributions(Dist(2026, 6, "A"), Dist(2024, 1, "B"), Dist(2025, 3, "C"), Dist(2026, 5, "D")), null);

        Assert.Equal([2024, 2025, 2026], model.Years);
        Assert.Equal(2026, model.SelectedYear);
    }

    [Fact]
    public void From_RequestedYear_IsSelectedWhenItHasDistributions()
    {
        var model = TestConsultantMenuViewModel.From(Distributions(Dist(2026, 6, "A"), Dist(2025, 3, "B")), 2025);

        Assert.Equal(2025, model.SelectedYear);
        Assert.Equal("B", Assert.Single(Assert.Single(model.Months).Distributions).DistributionTitle);
    }

    [Fact]
    public void From_RequestedYearWithNoDistributions_FallsBackToTheLatest() =>
        Assert.Equal(2026, TestConsultantMenuViewModel.From(Distributions(Dist(2026, 6, "A")), 1999).SelectedYear);

    [Fact]
    public void From_GroupsTheSelectedYearByMonthNewestFirstKeepingTheDistributionOrder()
    {
        var model = TestConsultantMenuViewModel.From(
            Distributions(
                Dist(2026, 1, "Jan"),
                Dist(2026, 6, "June one"),
                Dist(2026, 6, "June two"),
                Dist(2025, 12, "Other year")),
            2026);

        Assert.Equal(["June 2026", "January 2026"], model.Months.Select(m => m.Title));
        Assert.Equal([6, 1], model.Months.Select(m => m.MonthId));
        Assert.Equal(["June one", "June two"], model.Months[0].Distributions.Select(d => d.DistributionTitle));
    }

    // ---- Tabulation ---------------------------------------------------------------------------

    [Fact]
    public void ToFilter_BeforeTheFormHasBeenDrawn_ShowsEverything()
    {
        var model = new TestConsultantTabulationViewModel { AllSamples = ["26/1", "26/2"], ShownSamples = ["26/1"], ShowMethodItems = false };

        Assert.Equal((0, false), (model.ToFilter().HiddenSamples.Count, model.ToFilter().HideMethodItems));
    }

    [Fact]
    public void ToFilter_HidesTheUntickedSamplesAndMethodItems()
    {
        var model = new TestConsultantTabulationViewModel
        {
            IsFiltered = true,
            AllSamples = ["26/1", "26/2", "26/3"],
            ShownSamples = ["26/2"],
            ShowMethodItems = false,
        };

        var filter = model.ToFilter();

        Assert.Equal(["26/1", "26/3"], filter.HiddenSamples);
        Assert.True(filter.HideMethodItems);
    }

    [Fact]
    public void ToFilter_EverythingTicked_HidesNothing()
    {
        var model = new TestConsultantTabulationViewModel { IsFiltered = true, AllSamples = ["26/1"], ShownSamples = ["26/1"] };

        Assert.Empty(model.ToFilter().HiddenSamples);
        Assert.False(model.ToFilter().HideMethodItems);
    }

    [Fact]
    public void Display_NullTabulation_Throws() =>
        Assert.Throws<ArgumentNullException>(() => new TestConsultantTabulationViewModel().Display(SchemeId, null!));

    [Fact]
    public void Display_FirstTime_ShowsEverySampleAndOnlyThenMarksTheFormAsDrawn()
    {
        var model = new TestConsultantTabulationViewModel();

        model.Display(SchemeId, Tabulation());

        Assert.Equal(SchemeId, model.MonthlyDistributionSchemeId);
        Assert.NotNull(model.Tabulation);
        Assert.Equal(["26/1", "26/2"], model.AllSamples);
        Assert.Equal(["26/1", "26/2"], model.ShownSamples);
        Assert.True(model.IsFiltered);
        Assert.True(model.ShowMethodItems);
    }

    [Fact]
    public void Display_AfterDrawing_KeepsOnlyTheTickedSamplesThatStillExist()
    {
        var model = new TestConsultantTabulationViewModel { IsFiltered = true, ShownSamples = ["26/2", "99/9"], ShowMethodItems = false };

        model.Display(SchemeId, Tabulation());

        Assert.Equal(["26/2"], model.ShownSamples);
        Assert.Equal(["26/1", "26/2"], model.AllSamples);
        Assert.False(model.ShowMethodItems);
    }
}
