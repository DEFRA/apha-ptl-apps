using PTL.Core.Distribution;

namespace PTL.Api.Tests.Distribution;

public class MonthlyDistributionScheduleValidatorTests
{
    private static readonly DateTime Base = new(2026, 4, 1);

    [Fact]
    public void Validate_AllDatesInOrder_ReturnsNoErrors()
    {
        var errors = MonthlyDistributionScheduleValidator.Validate(Base, Base, Base.AddDays(5), Base.AddDays(10));

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_DeadlineBeforeDistributionDate_ReturnsError()
    {
        var errors = MonthlyDistributionScheduleValidator.Validate(Base, Base.AddDays(-5), Base.AddDays(-1), Base.AddDays(10));

        Assert.Contains(errors, e => e.Field == MonthlyDistributionScheduleValidator.DeadlineDate
            && e.Message == "Deadline Date needs to be after the UK Posting date.");
    }

    [Fact]
    public void Validate_DeadlineBeforeOverseasPostingDate_ReturnsError()
    {
        var errors = MonthlyDistributionScheduleValidator.Validate(Base, Base.AddDays(5), Base.AddDays(2), Base.AddDays(10));

        Assert.Contains(errors, e => e.Field == MonthlyDistributionScheduleValidator.DeadlineDate
            && e.Message == "Deadline Date needs to be after the Overseas Posting date.");
    }

    [Fact]
    public void Validate_ResultsIssueTargetDateBeforeDistributionDate_ReturnsError()
    {
        var errors = MonthlyDistributionScheduleValidator.Validate(Base, Base, Base.AddDays(1), Base.AddDays(-1));

        Assert.Contains(errors, e => e.Field == MonthlyDistributionScheduleValidator.ResultsIssueTargetDate
            && e.Message == "Results Issue Target Date needs to be after the UK Posting date.");
    }

    [Fact]
    public void Validate_ResultsIssueTargetDateBeforeOverseasPostingDate_ReturnsError()
    {
        var errors = MonthlyDistributionScheduleValidator.Validate(Base, Base.AddDays(5), Base.AddDays(6), Base.AddDays(4));

        Assert.Contains(errors, e => e.Field == MonthlyDistributionScheduleValidator.ResultsIssueTargetDate
            && e.Message == "Results Issue Target Date needs to be after the Overseas Posting date.");
    }

    [Fact]
    public void Validate_ResultsIssueTargetDateBeforeDeadlineDate_ReturnsError()
    {
        var errors = MonthlyDistributionScheduleValidator.Validate(Base, Base, Base.AddDays(10), Base.AddDays(5));

        Assert.Contains(errors, e => e.Field == MonthlyDistributionScheduleValidator.ResultsIssueTargetDate
            && e.Message == "Results Issue Target Date needs to be after the Deadline date.");
    }

    [Fact]
    public void Validate_OverseasPostingDateBeforeDistributionDate_DoesNotError()
    {
        // Legacy does NOT compare these two dates - no CompareValidator exists between them.
        var errors = MonthlyDistributionScheduleValidator.Validate(Base, Base.AddDays(-10), Base.AddDays(5), Base.AddDays(10));

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_EqualDates_DoNotError()
    {
        // Legacy's CompareValidators use GreaterThanEqual, not strictly GreaterThan.
        var errors = MonthlyDistributionScheduleValidator.Validate(Base, Base, Base, Base);

        Assert.Empty(errors);
    }
}
