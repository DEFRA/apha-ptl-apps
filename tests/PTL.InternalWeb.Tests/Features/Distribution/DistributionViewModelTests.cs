using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using PTL.InternalWeb.Features.Distribution;

namespace PTL.InternalWeb.Tests.Features.Distribution;

// Exercises every computed display property and validation branch on the Distribution view models.
// These are consumed only by the Razor views, which unit tests never render, so each getter needs a
// direct read to be counted as covered.
public class DistributionViewModelTests
{
    private static MonthlyDistributionScheduleRowViewModel Row() => new()
    {
        MonthlyDistributionSchemeId = Guid.NewGuid(),
        SchemeIdentifier = "PT0060",
        SchemeName = "Mastitis bovine",
        DistributionReferenceFull = "17152/BA",
        DistributionDate = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
        OverseasPostingDate = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
        DeadlineDate = new DateTime(2026, 4, 5, 0, 0, 0, DateTimeKind.Utc),
        ResultsIssueTargetDate = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc),
    };

    [Fact]
    public void SchemeHeading_NotCancelled_HasNoPrefix()
    {
        var row = Row();

        Assert.Equal("PT0060: Mastitis bovine", row.SchemeHeading);
    }

    [Fact]
    public void SchemeHeading_Cancelled_UsesCancelledPrefix()
    {
        var row = Row();
        row.IsCancelled = true;

        Assert.Equal("[Cancelled] PT0060: Mastitis bovine", row.SchemeHeading);
    }

    [Fact]
    public void SchemeHeading_CancelledAsAvailable_UsesNotCommencedPrefix()
    {
        var row = Row();
        row.IsCancelled = true;
        row.IsAsAvailable = true;

        Assert.Equal("[Not Commenced] PT0060: Mastitis bovine", row.SchemeHeading);
    }

    [Fact]
    public void CancelCommenceLink_NotCancelled_OffersCancel()
    {
        var row = Row();

        Assert.True(row.ShowCancelCommenceLink);
        Assert.Equal("Cancel this Distribution", row.CancelCommenceLinkText);
        Assert.Equal("Are you sure you wish to Cancel this Distribution?", row.CancelCommenceConfirmText);
    }

    [Fact]
    public void CancelCommenceLink_CancelledAsAvailable_OffersCommence()
    {
        var row = Row();
        row.IsCancelled = true;
        row.IsAsAvailable = true;

        Assert.True(row.ShowCancelCommenceLink);
        Assert.Equal("Commence Distribution", row.CancelCommenceLinkText);
        Assert.Equal("Are you sure you wish to Commence this Distribution?", row.CancelCommenceConfirmText);
    }

    [Fact]
    public void CancelCommenceLink_CancelledNotAsAvailable_IsHidden()
    {
        var row = Row();
        row.IsCancelled = true;

        Assert.False(row.ShowCancelCommenceLink);
    }

    [Theory]
    [InlineData(0, "0 participants")]
    [InlineData(1, "1 participant")]
    [InlineData(4, "4 participants")]
    public void ParticipantCountLabel_PluralisesOnlyForOne(int count, string expected)
    {
        var row = Row();
        row.ParticipantCount = count;

        Assert.Equal(expected, row.ParticipantCountLabel);
    }

    [Fact]
    public void SampleNumbersLink_NoParticipants_IsDisabledWithReason()
    {
        var row = Row();

        Assert.False(row.SampleNumbersLinkEnabled);
        Assert.Equal("Define Sample Numbers", row.SampleNumbersLinkText);
        Assert.Equal("Sample numbers not required as this Scheme has no participants for this month.", row.SampleNumbersDisabledTooltip);
        Assert.Equal("Click to define the Sample Numbers for this Scheme.", MonthlyDistributionScheduleRowViewModel.SampleNumbersTooltip);
    }

    [Fact]
    public void SampleNumbersLink_Defined_SwitchesToViewAndClearsDisabledReason()
    {
        var row = Row();
        row.ParticipantCount = 2;
        row.HasSampleNumbersDefined = true;

        Assert.True(row.SampleNumbersLinkEnabled);
        Assert.Equal("View Sample Numbers", row.SampleNumbersLinkText);
        Assert.Null(row.SampleNumbersDisabledTooltip);
    }

    [Fact]
    public void IntendedResultsLink_RequiresSampleNumbersFirst()
    {
        var row = Row();

        Assert.False(row.IntendedResultsLinkEnabled);
        Assert.Equal("Must define Sample Numbers first.", row.IntendedResultsTooltip);

        row.HasSampleNumbersDefined = true;

        Assert.True(row.IntendedResultsLinkEnabled);
        Assert.Equal("Click to enter the Intended Results for this Scheme.", row.IntendedResultsTooltip);
    }

    [Fact]
    public void Validate_DatesInOrder_ReturnsNoErrors()
    {
        var model = new MonthlyDistributionScheduleViewModel { YearId = 2026, MonthId = 4, Schemes = [Row()] };

        Assert.Empty(model.Validate(new ValidationContext(model)));
    }

    [Fact]
    public void Validate_MissingDate_SkipsOrderingRules()
    {
        var row = Row();
        row.DeadlineDate = null;
        var model = new MonthlyDistributionScheduleViewModel { Schemes = [row] };

        Assert.Empty(model.Validate(new ValidationContext(model)));
    }

    [Fact]
    public void Validate_DeadlineBeforeDistributionDate_ReportsRowScopedError()
    {
        var row = Row();
        row.DeadlineDate = new DateTime(2026, 3, 30, 0, 0, 0, DateTimeKind.Utc);
        var model = new MonthlyDistributionScheduleViewModel { Schemes = [row] };

        var results = model.Validate(new ValidationContext(model)).ToList();

        var result = Assert.Single(results, r => r.ErrorMessage == "Deadline Date needs to be after the UK Posting date.");
        Assert.Equal("Schemes[0].DeadlineDate", Assert.Single(result.MemberNames));
    }

    [Fact]
    public void DashboardViewModel_ExposesEveryProperty()
    {
        var model = new DistributionDashboardViewModel
        {
            SelectedYearId = 2026,
            YearOptions = [new SelectListItem("2026/27", "2026", true)],
            Months = [new DistributionDashboardMonthRowViewModel(2026, 4, "Apr", true, false, 50, 25, 10, 5, 1)],
        };

        Assert.Equal(2026, model.SelectedYearId);
        Assert.Equal("2026/27", Assert.Single(model.YearOptions).Text);

        var month = Assert.Single(model.Months);
        Assert.Equal(2026, month.YearId);
        Assert.Equal(4, month.MonthId);
        Assert.Equal("Apr", month.MonthDescription);
        Assert.True(month.IsInitialised);
        Assert.False(month.IsInitialisable);
        Assert.Equal(50, month.SchedulingCompletePercentage);
        Assert.Equal(25, month.PreparationCompletePercentage);
        Assert.Equal(10, month.PackagingCompletePercentage);
        Assert.Equal(5, month.ResultsCompletePercentage);
        Assert.Equal(1, month.TabulationsCompletePercentage);
    }
}
