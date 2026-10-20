using PTL.Core.Distribution;
using static PTL.Api.Tests.Distribution.TabulationEmailTestData;

namespace PTL.Api.Tests.Distribution;

public class TabulationEmailOptionsTests
{
    [Fact]
    public void TabulationEmailOptions_Defaults_MatchTheAgreedSchedulerSettings()
    {
        var options = new TabulationEmailOptions();

        Assert.Equal("TabulationEmails", TabulationEmailOptions.SectionName);
        Assert.True(options.Enabled);
        Assert.Equal("Europe/London", options.TimeZone);
        Assert.Equal("Test%20Consultant/Tabulation", options.InternalCommentsPath);
        Assert.Equal("CommentsEntry", options.ExternalCommentsPath);
    }

    [Fact]
    public void PublicationNotificationOptions_Defaults_AreDisabled()
    {
        Assert.Equal("PublicationNotifications", PublicationNotificationOptions.SectionName);
        Assert.False(new PublicationNotificationOptions().Enabled);
    }

    [Fact]
    public void TabulationEmailTemplates_AreTheFixedNotifyTemplates()
    {
        Assert.Equal(("tabulation-tc-first-request", "a2da705b-46b7-47cc-9ec7-00d5eec94f2d"), (TabulationEmailTemplates.FirstRequestName, TabulationEmailTemplates.FirstRequestId));
        Assert.Equal(("tabulation-tc-chaser", "ca746cae-e71d-47f9-a263-f246e8d60edc"), (TabulationEmailTemplates.ChaserName, TabulationEmailTemplates.ChaserId));
        Assert.Equal(("tabulation-published-notification", "396b4c10-e36c-462e-9629-ea9d1950ef3e"), (TabulationEmailTemplates.PublishedName, TabulationEmailTemplates.PublishedId));
    }

    [Fact]
    public void TabulationEmailSchedule_Constants_AreTheLegacyDayCounts()
    {
        Assert.Equal(7, TabulationEmailSchedule.FirstEmailLastDay);
        Assert.Equal(8, TabulationEmailSchedule.ChaserFirstDay);
        Assert.Equal(10, TabulationEmailSchedule.OverdueDay);
        Assert.Equal(7, TabulationEmailSchedule.FirstEmailDeadlineDays);
        Assert.Equal(2, TabulationEmailSchedule.ChaserDeadlineDays);
    }

    [Fact]
    public void RunSummaries_Empty_AreAllZero()
    {
        Assert.Equal(new TabulationEmailRunSummary(0, 0, 0, 0, 0), TabulationEmailRunSummary.Empty);
        Assert.Equal(new PublicationNotificationRunSummary(0, 0, 0, 0), PublicationNotificationRunSummary.Empty);
    }

    [Fact]
    public void Candidate_ReferenceFull_JoinsReferenceSuffixAndScheduleCode()
    {
        var candidate = new TabulationEmailCandidateEntity { DistributionReference = "17152", DistributionReferenceSuffix = "A", ScheduleCode = "BA" };

        Assert.Equal("17152A/BA", candidate.ReferenceFull);
        Assert.Equal(string.Empty, new TabulationConsultantEntity().FriendlyName);
        Assert.False(new TabulationConsultantEntity().IsExternal);
        Assert.Equal(Guid.Empty, new NotificationEmailEntity().NotificationEmailId);
    }
}

public class TabulationEmailGuardTests
{
    [Fact]
    public void FirstSent_IsTrackedPerSchemeAndCleared()
    {
        var guard = new TabulationEmailGuard();

        Assert.False(guard.FirstSent(Mds1));
        guard.MarkFirstSent(Mds1);

        Assert.True(guard.FirstSent(Mds1));
        Assert.False(guard.FirstSent(Mds2));

        guard.ClearFirst(Mds1);

        Assert.False(guard.FirstSent(Mds1));
    }

    [Fact]
    public void ChaserSent_IsTrackedPerSchemeAndSlotAndClearedPerScheme()
    {
        var guard = new TabulationEmailGuard();

        Assert.False(guard.ChaserSent(Mds1, 1));
        guard.MarkChaserSent(Mds1, 1);
        guard.MarkChaserSent(Mds1, 2);
        guard.MarkChaserSent(Mds2, 1);

        Assert.True(guard.ChaserSent(Mds1, 1));
        Assert.True(guard.ChaserSent(Mds1, 2));
        Assert.False(guard.ChaserSent(Mds1, 3));
        Assert.True(guard.ChaserSent(Mds2, 1));

        guard.ClearChasers(Mds1);

        Assert.False(guard.ChaserSent(Mds1, 1));
        Assert.False(guard.ChaserSent(Mds1, 2));
        Assert.True(guard.ChaserSent(Mds2, 1));
    }

    [Fact]
    public void FirstWarningToday_IsTrueOncePerIdSlotAndDay()
    {
        var guard = new TabulationEmailGuard();
        var day = new DateTime(2026, 5, 12);

        Assert.True(guard.FirstWarningToday(Mds1, 1, day));
        Assert.False(guard.FirstWarningToday(Mds1, 1, day));
        Assert.True(guard.FirstWarningToday(Mds1, 2, day));
        Assert.True(guard.FirstWarningToday(Mds2, 1, day));
        Assert.True(guard.FirstWarningToday(Mds1, 1, day.AddDays(1)));
        Assert.False(guard.FirstWarningToday(Mds1, 1, day.AddDays(1)));
    }

    [Fact]
    public void FirstWarningToday_AnEarlierDayAfterALaterOne_DoesNotWarnAgain()
    {
        var guard = new TabulationEmailGuard();

        Assert.True(guard.FirstWarningToday(Mds1, 1, new DateTime(2026, 5, 13)));
        Assert.False(guard.FirstWarningToday(Mds1, 1, new DateTime(2026, 5, 12)));
    }

    [Fact]
    public void Lock_AllowsOneHolderAtATime()
    {
        var guard = new TabulationEmailGuard();

        Assert.True(guard.Lock.Wait(0));
        Assert.False(guard.Lock.Wait(0));
        guard.Lock.Release();
        Assert.Equal(1, guard.Lock.CurrentCount);
    }
}
