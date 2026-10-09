namespace PTL.Core.Distribution;

public sealed record MonthlyDistributionScheduleError(string Field, string Message);

// Ports monthlys.aspx's "Dates" ValidationGroup chronological-ordering CompareValidators
// VERBATIM (message text included) - see docs/analysis/distribution-analysis.md. Required-ness is
// owned by PTL.InternalWeb (DataAnnotations), this validator only enforces cross-field ordering:
//   Deadline Date      >= Distribution Date (UK Posting) and >= Overseas Posting Date
//   Results Issue Date >= Distribution Date, >= Overseas Posting Date, and >= Deadline Date
// Legacy does NOT require Overseas Posting Date to be on/after Distribution Date - that pair has
// no CompareValidator between them. Do not add one.
public static class MonthlyDistributionScheduleValidator
{
    // Field name constants identifying which posted date a given error belongs to.
    public const string DeadlineDate = "DeadlineDate";
    public const string ResultsIssueTargetDate = "ResultsIssueTargetDate";

    public static IReadOnlyList<MonthlyDistributionScheduleError> Validate(
        DateTime distributionDate,
        DateTime overseasPostingDate,
        DateTime deadlineDate,
        DateTime resultsIssueTargetDate)
    {
        var errors = new List<MonthlyDistributionScheduleError>();

        if (deadlineDate < distributionDate)
        {
            errors.Add(new MonthlyDistributionScheduleError(DeadlineDate, "Deadline Date needs to be after the UK Posting date."));
        }

        if (deadlineDate < overseasPostingDate)
        {
            errors.Add(new MonthlyDistributionScheduleError(DeadlineDate, "Deadline Date needs to be after the Overseas Posting date."));
        }

        if (resultsIssueTargetDate < distributionDate)
        {
            errors.Add(new MonthlyDistributionScheduleError(ResultsIssueTargetDate, "Results Issue Target Date needs to be after the UK Posting date."));
        }

        if (resultsIssueTargetDate < overseasPostingDate)
        {
            errors.Add(new MonthlyDistributionScheduleError(ResultsIssueTargetDate, "Results Issue Target Date needs to be after the Overseas Posting date."));
        }

        if (resultsIssueTargetDate < deadlineDate)
        {
            errors.Add(new MonthlyDistributionScheduleError(ResultsIssueTargetDate, "Results Issue Target Date needs to be after the Deadline date."));
        }

        return errors;
    }
}
