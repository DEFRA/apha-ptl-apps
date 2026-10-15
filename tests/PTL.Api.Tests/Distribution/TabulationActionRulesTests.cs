using PTL.Core.Distribution;
using static PTL.Api.Tests.Distribution.TabulationsTestData;

namespace PTL.Api.Tests.Distribution;

// Legacy Scheduling/Tabulations.aspx GridViewMDS_RowDataBound - the first matching branch wins.
// "Today" is 12/05/2026.
public class TabulationActionRulesTests
{
    private const string AmendHint = "Click to Amend the Results for this Distribution.";
    private const string WaitingForSignOffHint = "This Distribution has been made available for Results Sign-Off and is waiting to be published";

    private static void AssertDefaultAmend(TabulationSchemeActions actions) =>
        Assert.Equal(new TabulationAction("Amend Results", true, AmendHint, TabulationCommand.None), actions.AmendResults);

    // ---- A / B: nothing to act on --------------------------------------------------------------

    [Fact]
    public void NoTabulations_DisablesEveryActionWithSampleNumbersHint()
    {
        var actions = Compute(NewScheme(s => s.Tabulations = []));

        foreach (var action in new[] { actions.AmendResults, actions.SendToTestConsultant, actions.SendForAssessment, actions.SendToResultsSignOff })
        {
            Assert.False(action.IsEnabled);
            Assert.Equal("Sample Numbers not defined", action.Hint);
        }

        Assert.Equal("Amend Results", actions.AmendResults.Text);
        Assert.Equal("Send To Test Consultant", actions.SendToTestConsultant.Text);
        Assert.Equal("Send For Assessment", actions.SendForAssessment.Text);
        Assert.Equal("Send For Results Sign-Off", actions.SendToResultsSignOff.Text);
    }

    [Fact]
    public void NoTabulations_TakesPrecedenceOverMissingIntendedResults()
    {
        var actions = Compute(NewScheme(s =>
        {
            s.Tabulations = [];
            s.HasIntendedResults = false;
        }));

        Assert.All(
            new[] { actions.AmendResults, actions.SendToTestConsultant, actions.SendForAssessment, actions.SendToResultsSignOff },
            a => Assert.Equal("Sample Numbers not defined", a.Hint));
    }

    [Fact]
    public void NoIntendedResults_DisablesEveryActionWithIntendedResultsHint()
    {
        var actions = Compute(NewScheme(s => s.HasIntendedResults = false));

        Assert.All(
            new[] { actions.AmendResults, actions.SendToTestConsultant, actions.SendForAssessment, actions.SendToResultsSignOff },
            a =>
            {
                Assert.False(a.IsEnabled);
                Assert.Equal("Intended Results not defined", a.Hint);
            });
    }

    // ---- C: unaccounted samples ----------------------------------------------------------------

    [Fact]
    public void AssessmentScheme_WithUnaccountedSamples_DisablesAssessmentAndSignOff()
    {
        var actions = Compute(NewScheme(s =>
        {
            s.RequiresAssessment = true;
            s.AssessmentStatuses = [Status(accounted: false)];
        }));

        Assert.False(actions.SendForAssessment.IsEnabled);
        Assert.Equal("Unaccounted Samples", actions.SendForAssessment.Text);
        Assert.Equal("Unaccounted samples on the distribution", actions.SendForAssessment.Hint);
        Assert.False(actions.SendToResultsSignOff.IsEnabled);
        Assert.Equal("Send For Results Sign-Off", actions.SendToResultsSignOff.Text);
        Assert.Equal("Unaccounted samples on the distribution", actions.SendToResultsSignOff.Hint);
        AssertDefaultAmend(actions);
        Assert.True(actions.SendToTestConsultant.IsEnabled);
        Assert.Equal(string.Empty, actions.SendToTestConsultant.Hint);
    }

    [Fact]
    public void UnaccountedSamples_TakePrecedenceOverTheDeadlineNotPassedRule()
    {
        var actions = Compute(NewScheme(s =>
        {
            s.RequiresAssessment = true;
            s.DeadlineDate = Today.AddDays(5);
            s.AssessmentStatuses = [Status(accounted: false)];
        }));

        Assert.Equal("Unaccounted Samples", actions.SendForAssessment.Text);
        Assert.True(actions.SendToTestConsultant.IsEnabled);
    }

    [Fact]
    public void SchemeWithoutAssessment_IgnoresUnaccountedSamples()
    {
        var actions = Compute(NewScheme(s => s.AssessmentStatuses = [Status(accounted: false)]));

        Assert.True(actions.SendForAssessment.IsEnabled);
        Assert.Equal("Send For Assessment", actions.SendForAssessment.Text);
        Assert.True(actions.SendToResultsSignOff.IsEnabled);
    }

    [Theory]
    [InlineData(false, false, false, true)]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, false, true, false)]
    public void HasUnaccountedSamples_IsFalseWhenAnyOfTheThreeDatesIsSet(bool submitted, bool notSubmitted, bool notCapable, bool expected)
    {
        var scheme = NewScheme(s => s.AssessmentStatuses =
        [
            new TabulationAssessmentStatusEntity
            {
                SubmissionDate = submitted ? Today : null,
                NonSubmissionDate = notSubmitted ? Today : null,
                NonCapableDate = notCapable ? Today : null,
            },
        ]);

        Assert.Equal(expected, scheme.HasUnaccountedSamples);
    }

    [Fact]
    public void MinValueDatesAreTreatedAsUnset_ForUnaccountedSamplesAndAssessmentSubmitted()
    {
        var scheme = NewScheme(s => s.AssessmentStatuses =
        [
            new TabulationAssessmentStatusEntity
            {
                AssessmentSubmitted = DateTime.MinValue,
                SubmissionDate = DateTime.MinValue,
                NonSubmissionDate = DateTime.MinValue,
                NonCapableDate = DateTime.MinValue,
            },
        ]);

        Assert.True(scheme.HasUnaccountedSamples);
        Assert.False(scheme.IsAssessmentComplete);
    }

    [Fact]
    public void AnyUnaccountedRowMakesTheSchemeUnaccounted()
    {
        var scheme = NewScheme(s => s.AssessmentStatuses = [Status(), Status(accounted: false)]);

        Assert.True(scheme.HasUnaccountedSamples);
    }

    [Fact]
    public void NoAssessmentStatusRows_IsAssessmentCompleteAndHasNoUnaccountedSamples()
    {
        var scheme = NewScheme();

        Assert.True(scheme.IsAssessmentComplete);
        Assert.False(scheme.HasUnaccountedSamples);
    }

    [Fact]
    public void IsAssessmentComplete_RequiresEveryRowSubmitted()
    {
        Assert.True(NewScheme(s => s.AssessmentStatuses = [Status(), Status()]).IsAssessmentComplete);
        Assert.False(NewScheme(s => s.AssessmentStatuses = [Status(), Status(assessmentSubmitted: false)]).IsAssessmentComplete);
    }

    // ---- D: deadline not passed ----------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(30)]
    public void DeadlineTodayOrLater_DisablesTestConsultantAssessmentAndSignOff(int daysFromToday)
    {
        var deadline = Today.AddDays(daysFromToday);

        var actions = Compute(NewScheme(s => s.DeadlineDate = deadline.AddHours(14)));

        var hint = $"Deadline Date not Passed ({deadline:dd/MM/yyyy})";
        Assert.False(actions.SendToTestConsultant.IsEnabled);
        Assert.Equal(hint, actions.SendToTestConsultant.Hint);
        Assert.False(actions.SendForAssessment.IsEnabled);
        Assert.Equal(hint, actions.SendForAssessment.Hint);
        Assert.False(actions.SendToResultsSignOff.IsEnabled);
        Assert.Equal(hint, actions.SendToResultsSignOff.Hint);
        AssertDefaultAmend(actions);
        Assert.Equal("Send For Assessment", actions.SendForAssessment.Text);
    }

    [Fact]
    public void DeadlineYesterday_HasPassed_SoTheDeadlineHintIsNotShown()
    {
        var actions = Compute(NewScheme(s => s.DeadlineDate = Today.AddDays(-1)));

        Assert.True(actions.SendToTestConsultant.IsEnabled);
        Assert.Equal("Send these results to the Test Consultant for comments", actions.SendToTestConsultant.Hint);
    }

    [Fact]
    public void DeadlineDay_StaysOpenUntilTheNextDay_EvenLateInTheEvening()
    {
        var scheme = NewScheme(s => s.DeadlineDate = new DateTime(2026, 5, 12));

        Assert.False(TabulationActionRules.Compute(scheme, new DateTime(2026, 5, 12, 23, 59, 59)).SendToTestConsultant.IsEnabled);
        Assert.True(TabulationActionRules.Compute(scheme, new DateTime(2026, 5, 13, 0, 0, 1)).SendToTestConsultant.IsEnabled);
    }

    [Theory]
    [InlineData(true, "View/Enter Comments")]
    [InlineData(false, "Awaiting Assessment")]
    public void DeadlineNotPassed_AssessmentSchemeReadyForAssessor_ShowsAssessmentProgressButStaysDisabled(bool assessmentComplete, string expectedText)
    {
        var actions = Compute(NewScheme(s =>
        {
            s.RequiresAssessment = true;
            s.DeadlineDate = Today.AddDays(3);
            s.IsReadyForAssessor = Today.AddDays(-2);
            s.AssessmentStatuses = [Status(assessmentSubmitted: assessmentComplete)];
        }));

        Assert.Equal(expectedText, actions.SendForAssessment.Text);
        Assert.False(actions.SendForAssessment.IsEnabled);
        Assert.Equal($"Deadline Date not Passed ({Today.AddDays(3):dd/MM/yyyy})", actions.SendForAssessment.Hint);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void DeadlineNotPassed_AssessmentTextUnchangedUnlessAssessmentSchemeIsReadyForAssessor(bool requiresAssessment, bool readyIsMinValue)
    {
        DateTime? ready = !requiresAssessment ? Today : readyIsMinValue ? DateTime.MinValue : null;

        var actions = Compute(NewScheme(s =>
        {
            s.RequiresAssessment = requiresAssessment;
            s.DeadlineDate = Today.AddDays(3);
            s.IsReadyForAssessor = ready;
        }));

        Assert.Equal("Send For Assessment", actions.SendForAssessment.Text);
    }

    // ---- E: deadline passed - defaults --------------------------------------------------------

    [Fact]
    public void DeadlinePassed_NothingSent_LeavesActionsEnabledWithSendHints()
    {
        var actions = Compute(NewScheme());

        Assert.Equal(new TabulationAction("Send To Test Consultant", true, "Send these results to the Test Consultant for comments", TabulationCommand.SendToTestConsultant), actions.SendToTestConsultant);
        Assert.Equal(new TabulationAction("Send For Assessment", true, "Send this distribution for assessment", TabulationCommand.SendForAssessment), actions.SendForAssessment);
        Assert.Equal(new TabulationAction("Send For Results Sign-Off", true, "Send this Distribution's Tabulation(s) for Results Sign-Off", TabulationCommand.SendToResultsSignOff), actions.SendToResultsSignOff);
        AssertDefaultAmend(actions);
    }

    [Fact]
    public void DeadlinePassed_AssessmentSchemeNotYetAssessed_DisablesSignOff()
    {
        var actions = Compute(NewScheme(s =>
        {
            s.RequiresAssessment = true;
            s.AssessmentStatuses = [Status(assessmentSubmitted: false)];
        }));

        Assert.False(actions.SendToResultsSignOff.IsEnabled);
        Assert.Equal("Assessment not submitted", actions.SendToResultsSignOff.Hint);
        Assert.True(actions.SendForAssessment.IsEnabled);
    }

    [Fact]
    public void DeadlinePassed_AssessmentSchemeAssessed_EnablesSignOff()
    {
        var actions = Compute(NewScheme(s =>
        {
            s.RequiresAssessment = true;
            s.AssessmentStatuses = [Status()];
        }));

        Assert.True(actions.SendToResultsSignOff.IsEnabled);
        Assert.Equal("Send this Distribution's Tabulation(s) for Results Sign-Off", actions.SendToResultsSignOff.Hint);
    }

    // ---- E1: comments submitted / published before --------------------------------------------

    [Theory]
    [InlineData(true, 0)]
    [InlineData(false, 1)]
    [InlineData(true, 3)]
    public void CommentsSubmittedOrPublishedBefore_ShowsViewCommentsLinks(bool commentSubmitted, int publishCount)
    {
        var actions = Compute(NewScheme(s =>
        {
            s.IsCommentSubmitted = commentSubmitted;
            s.Tabulations = [Tabulation(publishCount: 0), Tabulation(publishCount: publishCount)];
        }));

        Assert.Equal(new TabulationAction("View Comments", true, "Click to view the Test Consultant's Comments", TabulationCommand.ViewComments), actions.SendToTestConsultant);
        Assert.Equal(new TabulationAction("View/Enter Comments", true, "Click to view the Tabulation Comments", TabulationCommand.ViewComments), actions.SendForAssessment);
    }

    [Fact]
    public void CommentsSubmitted_TakesPrecedenceOverReadyForResultsSignOffAndEmailState()
    {
        var actions = Compute(NewScheme(s =>
        {
            s.IsCommentSubmitted = true;
            s.IsReadyForResultsSignOff = Today;
            s.IsReadyForTestConsultant = Today;
            s.EmailState = (int)TabulationEmailState.FirstEmailSent;
        }));

        Assert.Equal("View Comments", actions.SendToTestConsultant.Text);
    }

    // ---- E2: ready for results sign-off -------------------------------------------------------

    [Fact]
    public void ReadyForResultsSignOff_WithoutComments_ShowsCommentsNotSubmitted()
    {
        var actions = Compute(NewScheme(s =>
        {
            s.IsReadyForResultsSignOff = Today.AddDays(-1);
            s.IsReadyForTestConsultant = Today.AddDays(-5);
        }));

        Assert.Equal(
            new TabulationAction(
                "Comments not submitted",
                true,
                "The Comments Provided by the Test Consultant for this Distribution were not Submitted or no Comments were provided",
                TabulationCommand.ViewComments),
            actions.SendToTestConsultant);
        Assert.Equal(new TabulationAction("View/Enter Comments", true, "Click to view the Tabulation Comments", TabulationCommand.ViewComments), actions.SendForAssessment);
    }

    [Fact]
    public void ReadyForResultsSignOffMinValue_IsTreatedAsNotReady()
    {
        var actions = Compute(NewScheme(s => s.IsReadyForResultsSignOff = DateTime.MinValue));

        Assert.Equal("Send To Test Consultant", actions.SendToTestConsultant.Text);
        Assert.Equal("Send For Results Sign-Off", actions.SendToResultsSignOff.Text);
    }

    // ---- E3: ready for the Test Consultant (email progress) ------------------------------------

    [Fact]
    public void ReadyForTestConsultant_NoEmailSent_ShowsWaitingToSendWithTheReadyDate()
    {
        var actions = Compute(NewScheme(s =>
        {
            s.IsReadyForTestConsultant = new DateTime(2026, 5, 14, 9, 30, 0);
            s.EmailState = (int)TabulationEmailState.None;
        }));

        Assert.False(actions.SendToTestConsultant.IsEnabled);
        Assert.Equal("Waiting to send Email 14/05/2026", actions.SendToTestConsultant.Text);
        Assert.Equal("Waiting for Email Service to send 1st email to Test Consultant", actions.SendToTestConsultant.Hint);
        Assert.Equal(TabulationCommand.SendToTestConsultant, actions.SendToTestConsultant.Command);
        Assert.False(actions.SendToTestConsultant.IsOverdue);
    }

    [Fact]
    public void ReadyForTestConsultant_FirstEmailSent_ShowsFirstEmailSent()
    {
        var actions = Compute(NewScheme(s =>
        {
            s.IsReadyForTestConsultant = Today.AddDays(-3);
            s.EmailState = (int)TabulationEmailState.FirstEmailSent;
        }));

        Assert.False(actions.SendToTestConsultant.IsEnabled);
        Assert.Equal("First Email Sent", actions.SendToTestConsultant.Text);
        Assert.Equal("1st Email has been sent to Test Consultant", actions.SendToTestConsultant.Hint);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    [InlineData(9)]
    public void ReadyForTestConsultant_ChaserSentWithinNineDays_ShowsChaserEmailSent(int daysSinceReady)
    {
        var actions = Compute(NewScheme(s =>
        {
            s.IsReadyForTestConsultant = Today.AddDays(-daysSinceReady).AddHours(16);
            s.EmailState = (int)TabulationEmailState.ChaserEmailSent;
        }));

        Assert.False(actions.SendToTestConsultant.IsEnabled);
        Assert.Equal("Chaser Email Sent", actions.SendToTestConsultant.Text);
        Assert.Equal("Chaser Email has been sent to all Test Consultants", actions.SendToTestConsultant.Hint);
        Assert.False(actions.SendToTestConsultant.IsOverdue);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(40)]
    public void ReadyForTestConsultant_ChaserSentMoreThanNineDaysAgo_ShowsOverdueLink(int daysSinceReady)
    {
        var actions = Compute(NewScheme(s =>
        {
            s.IsReadyForTestConsultant = Today.AddDays(-daysSinceReady);
            s.EmailState = (int)TabulationEmailState.ChaserEmailSent;
        }));

        Assert.Equal(
            new TabulationAction("OVERDUE", true, "Test Consultant Comments are now Overdue", TabulationCommand.ViewComments, IsOverdue: true),
            actions.SendToTestConsultant);
    }

    [Theory]
    [InlineData((int)TabulationEmailState.None, 10)]
    [InlineData((int)TabulationEmailState.None, 11)]
    [InlineData((int)TabulationEmailState.FirstEmailSent, 10)]
    [InlineData((int)TabulationEmailState.FirstEmailSent, 25)]
    [InlineData((int)TabulationEmailState.ChaserEmailSent, 10)]
    [InlineData(99, 10)]
    [InlineData(99, 40)]
    public void ReadyForTestConsultant_TenOrMoreDaysAgo_IsOverdueForAnyEmailState(int emailState, int daysSinceReady)
    {
        var actions = Compute(NewScheme(s =>
        {
            s.IsReadyForTestConsultant = Today.AddDays(-daysSinceReady).AddHours(23);
            s.EmailState = emailState;
        }));

        Assert.Equal(
            new TabulationAction("OVERDUE", true, "Test Consultant Comments are now Overdue", TabulationCommand.ViewComments, IsOverdue: true),
            actions.SendToTestConsultant);
    }

    [Theory]
    [InlineData((int)TabulationEmailState.None, "Waiting to send Email 03/05/2026", false)]
    [InlineData((int)TabulationEmailState.FirstEmailSent, "First Email Sent", false)]
    [InlineData((int)TabulationEmailState.ChaserEmailSent, "Chaser Email Sent", false)]
    [InlineData(99, "Send To Test Consultant", true)]
    public void ReadyForTestConsultant_NineDaysAgo_StillShowsTheEmailStateLabel(int emailState, string text, bool enabled)
    {
        var actions = Compute(NewScheme(s =>
        {
            s.IsReadyForTestConsultant = Today.AddDays(-9);
            s.EmailState = emailState;
        }));

        Assert.Equal((text, enabled, false), (actions.SendToTestConsultant.Text, actions.SendToTestConsultant.IsEnabled, actions.SendToTestConsultant.IsOverdue));
        Assert.NotEqual(TabulationCommand.ViewComments, actions.SendToTestConsultant.Command);
    }

    [Fact]
    public void ReadyForTestConsultant_UnknownEmailState_LeavesTheActionUnchanged()
    {
        var actions = Compute(NewScheme(s =>
        {
            s.IsReadyForTestConsultant = Today.AddDays(-3);
            s.EmailState = 99;
        }));

        Assert.Equal(
            new TabulationAction("Send To Test Consultant", true, "Send these results to the Test Consultant for comments", TabulationCommand.SendToTestConsultant),
            actions.SendToTestConsultant);
    }

    [Fact]
    public void ReadyForTestConsultant_TakesPrecedenceOverReadyForAssessor()
    {
        var actions = Compute(NewScheme(s =>
        {
            s.RequiresAssessment = true;
            s.AssessmentStatuses = [Status(assessmentSubmitted: false)];
            s.IsReadyForTestConsultant = Today.AddDays(-1);
            s.IsReadyForAssessor = Today.AddDays(-1);
        }));

        Assert.Equal("Waiting to send Email 11/05/2026", actions.SendToTestConsultant.Text);
        Assert.Equal("Send For Assessment", actions.SendForAssessment.Text);
        Assert.True(actions.SendForAssessment.IsEnabled);
    }

    // ---- E4: ready for the assessor -----------------------------------------------------------

    [Fact]
    public void ReadyForAssessor_AssessmentComplete_ShowsViewEnterComments()
    {
        var actions = Compute(NewScheme(s =>
        {
            s.RequiresAssessment = true;
            s.AssessmentStatuses = [Status()];
            s.IsReadyForAssessor = Today.AddDays(-2);
        }));

        Assert.Equal(new TabulationAction("View/Enter Comments", true, "Click to view the Tabulation Comments", TabulationCommand.ViewComments), actions.SendForAssessment);
    }

    [Fact]
    public void ReadyForAssessor_AssessmentIncomplete_ShowsAwaitingAssessmentDisabled()
    {
        var actions = Compute(NewScheme(s =>
        {
            s.RequiresAssessment = true;
            s.AssessmentStatuses = [Status(assessmentSubmitted: false)];
            s.IsReadyForAssessor = Today.AddDays(-2);
        }));

        Assert.False(actions.SendForAssessment.IsEnabled);
        Assert.Equal("Awaiting Assessment", actions.SendForAssessment.Text);
        Assert.Equal("Assessment not submitted", actions.SendForAssessment.Hint);
        Assert.False(actions.SendToResultsSignOff.IsEnabled);
    }

    // ---- sign-off state -----------------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public void ReadyForSignOff_OnOrBeforeTheResultsIssueTargetDate_ShowsWaitingForSignOff(int daysUntilTarget)
    {
        var actions = Compute(NewScheme(s =>
        {
            s.IsReadyForResultsSignOff = Today.AddDays(-1);
            s.ResultsIssueTargetDate = Today.AddDays(daysUntilTarget);
        }));

        Assert.Equal(
            new TabulationAction("Waiting for Sign-Off", false, WaitingForSignOffHint, TabulationCommand.SendToResultsSignOff),
            actions.SendToResultsSignOff);
    }

    [Fact]
    public void ReadyForSignOff_TheDayAfterTheTargetDate_ShowsRedOverdueLabel()
    {
        var actions = Compute(NewScheme(s =>
        {
            s.IsReadyForResultsSignOff = Today.AddDays(-3);
            s.ResultsIssueTargetDate = Today.AddDays(-1);
        }));

        Assert.Equal(
            new TabulationAction("OVERDUE", false, WaitingForSignOffHint, TabulationCommand.SendToResultsSignOff, IsOverdue: true, IsLabel: true),
            actions.SendToResultsSignOff);
    }

    [Fact]
    public void ReadyForSignOff_PastTargetButPublishedBefore_StillShowsWaitingForSignOff()
    {
        var actions = Compute(NewScheme(s =>
        {
            s.IsReadyForResultsSignOff = Today.AddDays(-3);
            s.ResultsIssueTargetDate = Today.AddDays(-1);
            s.Tabulations = [Tabulation(publishCount: 1)];
        }));

        Assert.Equal("Waiting for Sign-Off", actions.SendToResultsSignOff.Text);
        Assert.False(actions.SendToResultsSignOff.IsOverdue);
        Assert.False(actions.SendToResultsSignOff.IsLabel);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(2, 0)]
    public void NotReadyForSignOff_PublishedOrDecidedBefore_ShowsReSend(int publishState, int publishCount)
    {
        var actions = Compute(NewScheme(s => s.Tabulations = [Tabulation(publishState: publishState, publishCount: publishCount)]));

        Assert.Equal("Re-Send For Results Sign-Off", actions.SendToResultsSignOff.Text);
        Assert.True(actions.SendToResultsSignOff.IsEnabled);
        Assert.Equal("Send this Distribution's Tabulation(s) for Results Sign-Off", actions.SendToResultsSignOff.Hint);
        Assert.False(actions.SendToResultsSignOff.IsOverdue);
    }

    [Fact]
    public void ReSend_TakesPrecedenceOverTheTargetDateOverdueFlag()
    {
        var actions = Compute(NewScheme(s =>
        {
            s.ResultsIssueTargetDate = Today.AddDays(-4);
            s.Tabulations = [Tabulation(publishState: 1)];
        }));

        Assert.Equal("Re-Send For Results Sign-Off", actions.SendToResultsSignOff.Text);
        Assert.False(actions.SendToResultsSignOff.IsOverdue);
    }

    [Fact]
    public void AwaitingSignOffWithNothingPublished_IsNotReSend()
    {
        var actions = Compute(NewScheme(s => s.Tabulations = [Tabulation(publishState: (int)TabulationPublishState.AwaitingSignOff)]));

        Assert.Equal("Send For Results Sign-Off", actions.SendToResultsSignOff.Text);
    }

    [Fact]
    public void NeverSignedOff_TargetDatePassed_FlagsOverdueAndReplacesTheHint()
    {
        var actions = Compute(NewScheme(s => s.ResultsIssueTargetDate = Today.AddDays(-1)));

        Assert.True(actions.SendToResultsSignOff.IsOverdue);
        Assert.True(actions.SendToResultsSignOff.IsEnabled);
        Assert.Equal("Send For Results Sign-Off", actions.SendToResultsSignOff.Text);
        Assert.Equal("The Results Issue Target date for this Distribution has passed.", actions.SendToResultsSignOff.Hint);
    }

    [Fact]
    public void NeverSignedOff_TargetDateIsToday_IsNotOverdue()
    {
        var actions = Compute(NewScheme(s => s.ResultsIssueTargetDate = Today));

        Assert.False(actions.SendToResultsSignOff.IsOverdue);
        Assert.Equal("Send this Distribution's Tabulation(s) for Results Sign-Off", actions.SendToResultsSignOff.Hint);
    }

    [Fact]
    public void NeverSignedOff_TargetDatePassedButAssessmentOutstanding_StaysDisabledWithTheOverdueHint()
    {
        var actions = Compute(NewScheme(s =>
        {
            s.RequiresAssessment = true;
            s.AssessmentStatuses = [Status(assessmentSubmitted: false)];
            s.ResultsIssueTargetDate = Today.AddDays(-1);
        }));

        Assert.False(actions.SendToResultsSignOff.IsEnabled);
        Assert.True(actions.SendToResultsSignOff.IsOverdue);
        Assert.Equal("The Results Issue Target date for this Distribution has passed.", actions.SendToResultsSignOff.Hint);
    }
}
