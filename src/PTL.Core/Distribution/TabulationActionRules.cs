using System.Globalization;

namespace PTL.Core.Distribution;

// Legacy Scheduling/Tabulations.aspx GridViewMDS_RowDataBound, ported rule for rule: one chain
// decides which Actions-column state applies (the first matching branch wins) and every label,
// enabled flag and hint is the legacy text. Branch order and the oddities below are deliberate.
public static class TabulationActionRules
{
    private const string SampleNumbersNotDefined = "Sample Numbers not defined";
    private const string IntendedResultsNotDefined = "Intended Results not defined";
    private const string UnaccountedSamples = "Unaccounted samples on the distribution";
    private const string AssessmentNotSubmitted = "Assessment not submitted";
    private const string ViewTabulationComments = "Click to view the Tabulation Comments";
    private const string WaitingForSignOff = "This Distribution has been made available for Results Sign-Off and is waiting to be published";

    public static TabulationSchemeActions Compute(TabulationsSchemeEntity scheme, DateTime now)
    {
        var today = now.Date;
        var amend = new TabulationAction("Amend Results", true, "Click to Amend the Results for this Distribution.", TabulationCommand.None);
        var toTestConsultant = new TabulationAction("Send To Test Consultant", true, string.Empty, TabulationCommand.SendToTestConsultant);
        var forAssessment = new TabulationAction("Send For Assessment", true, string.Empty, TabulationCommand.SendForAssessment);
        var toSignOff = new TabulationAction("Send For Results Sign-Off", true, string.Empty, TabulationCommand.SendToResultsSignOff);

        var requiresAssessment = scheme.RequiresAssessment;
        var isAssessmentComplete = scheme.IsAssessmentComplete;

        if (scheme.Tabulations.Count == 0)
        {
            // The Sample Numbers have not been defined yet, so there is nothing to act on.
            amend = amend.Disabled(SampleNumbersNotDefined);
            toTestConsultant = toTestConsultant.Disabled(SampleNumbersNotDefined);
            forAssessment = forAssessment.Disabled(SampleNumbersNotDefined);
            toSignOff = toSignOff.Disabled(SampleNumbersNotDefined);
        }
        else if (!scheme.HasIntendedResults)
        {
            amend = amend.Disabled(IntendedResultsNotDefined);
            toTestConsultant = toTestConsultant.Disabled(IntendedResultsNotDefined);
            forAssessment = forAssessment.Disabled(IntendedResultsNotDefined);
            toSignOff = toSignOff.Disabled(IntendedResultsNotDefined);
        }
        else if (requiresAssessment && scheme.HasUnaccountedSamples)
        {
            forAssessment = forAssessment.Disabled(UnaccountedSamples) with { Text = "Unaccounted Samples" };
            toSignOff = toSignOff.Disabled(UnaccountedSamples);
        }
        else if (scheme.DeadlineDate.Date.AddHours(23) > today)
        {
            // Legacy compares against today's midnight, so the deadline only counts as passed from the day after it.
            var hint = $"Deadline Date not Passed ({scheme.DeadlineDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)})";
            toTestConsultant = toTestConsultant.Disabled(hint);
            forAssessment = forAssessment.Disabled(hint);
            toSignOff = toSignOff.Disabled(hint);

            if (requiresAssessment && IsSet(scheme.IsReadyForAssessor))
            {
                forAssessment = forAssessment with { Text = isAssessmentComplete ? "View/Enter Comments" : "Awaiting Assessment" };
            }
        }
        else
        {
            var hasBeenPublishedBefore = scheme.Tabulations.Any(t => t.PublishCount > 0);
            var hasDecisionMadeBefore = scheme.Tabulations.Any(t => t.PublishState > 0);

            toTestConsultant = toTestConsultant with { Hint = "Send these results to the Test Consultant for comments" };
            forAssessment = forAssessment with { Hint = "Send this distribution for assessment" };

            toSignOff = requiresAssessment && !isAssessmentComplete
                ? toSignOff.Disabled(AssessmentNotSubmitted)
                : toSignOff with { Hint = "Send this Distribution's Tabulation(s) for Results Sign-Off" };

            if (scheme.IsCommentSubmitted || hasBeenPublishedBefore)
            {
                toTestConsultant = ViewComments(toTestConsultant, "View Comments", "Click to view the Test Consultant's Comments");
                forAssessment = ViewComments(forAssessment, "View/Enter Comments", ViewTabulationComments);
            }
            else if (IsSet(scheme.IsReadyForResultsSignOff))
            {
                toTestConsultant = ViewComments(
                    toTestConsultant,
                    "Comments not submitted",
                    "The Comments Provided by the Test Consultant for this Distribution were not Submitted or no Comments were provided");
                forAssessment = ViewComments(forAssessment, "View/Enter Comments", ViewTabulationComments);
            }
            else if (IsSet(scheme.IsReadyForTestConsultant))
            {
                toTestConsultant = ApplyEmailState(toTestConsultant, scheme, today);
            }
            else if (IsSet(scheme.IsReadyForAssessor))
            {
                forAssessment = isAssessmentComplete
                    ? ViewComments(forAssessment, "View/Enter Comments", ViewTabulationComments)
                    : forAssessment.Disabled(AssessmentNotSubmitted) with { Text = "Awaiting Assessment" };
            }

            toSignOff = ApplySignOffState(toSignOff, scheme, today, hasBeenPublishedBefore, hasDecisionMadeBefore);
        }

        return new TabulationSchemeActions(amend, toTestConsultant, forAssessment, toSignOff);
    }

    // The Test Consultant's comments are outstanding: the Email Service's progress is the state, except that
    // OVERDUE depends on the date alone, so a missed email can never hide it.
    private static TabulationAction ApplyEmailState(TabulationAction action, TabulationsSchemeEntity scheme, DateTime today)
    {
        var readyDate = scheme.IsReadyForTestConsultant!.Value.Date;

        if (readyDate.AddDays(TabulationEmailSchedule.OverdueDay) <= today)
        {
            return action with
            {
                Text = "OVERDUE",
                Hint = "Test Consultant Comments are now Overdue",
                Command = TabulationCommand.ViewComments,
                IsOverdue = true,
            };
        }

        switch ((TabulationEmailState)scheme.EmailState)
        {
            case TabulationEmailState.None:
                return action.Disabled("Waiting for Email Service to send 1st email to Test Consultant")
                    with { Text = $"Waiting to send Email {readyDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}" };

            case TabulationEmailState.FirstEmailSent:
                return action.Disabled("1st Email has been sent to Test Consultant") with { Text = "First Email Sent" };

            case TabulationEmailState.ChaserEmailSent:
                return action.Disabled("Chaser Email has been sent to all Test Consultants") with { Text = "Chaser Email Sent" };

            default:
                return action;
        }
    }

    private static TabulationAction ApplySignOffState(
        TabulationAction action, TabulationsSchemeEntity scheme, DateTime today, bool hasBeenPublishedBefore, bool hasDecisionMadeBefore)
    {
        if (IsSet(scheme.IsReadyForResultsSignOff))
        {
            // Past the Results Issue Target date with nothing published, legacy swaps the link for a red label.
            return today <= scheme.ResultsIssueTargetDate || hasBeenPublishedBefore
                ? action.Disabled(WaitingForSignOff) with { Text = "Waiting for Sign-Off" }
                : action with { Text = "OVERDUE", Hint = WaitingForSignOff, IsEnabled = false, IsOverdue = true, IsLabel = true };
        }

        if (hasBeenPublishedBefore || hasDecisionMadeBefore)
        {
            return action with { Text = "Re-Send For Results Sign-Off" };
        }

        return today > scheme.ResultsIssueTargetDate
            ? action with { IsOverdue = true, Hint = "The Results Issue Target date for this Distribution has passed." }
            : action;
    }

    private static TabulationAction ViewComments(TabulationAction action, string text, string hint) =>
        action with { Text = text, Hint = hint, Command = TabulationCommand.ViewComments };

    private static TabulationAction Disabled(this TabulationAction action, string hint) =>
        action with { IsEnabled = false, Hint = hint };

    private static bool IsSet(DateTime? date) => date is { } value && value != DateTime.MinValue;
}
