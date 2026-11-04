using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PTL.Core.Configuration;
using PTL.Core.Distribution;
using PTL.Core.Notifications;
using static PTL.Api.Tests.Distribution.PackagingTestData;
using ParticipantEntity = PTL.Core.Participant.Participant;

namespace PTL.Api.Tests.Distribution;

// Pins legacy DistributionPostedEmailCollection.AddNewDistributionPostedEmail (one email per participant address) and the
// GOV.UK Notify send that replaces the polled SMTP service (legacy email.vb ProcessDistributionPostedEmails): an email is marked
// sent only after Notify accepted it, a failed one stays unsent, and nothing is retried.
public class DistributionPostedNotifierTests
{
    private const string TemplateId = "2bb0e2d0-fa23-472e-947c-4524d14ad288";

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
    private static readonly DateTimeOffset Now = new(2026, 5, 13, 10, 30, 0, TimeSpan.Zero);

    private static readonly Guid Email1 = Guid.Parse("e5000000-0000-0000-0000-000000000001");
    private static readonly Guid Email2 = Guid.Parse("e5000000-0000-0000-0000-000000000002");
    private static readonly Guid Email3 = Guid.Parse("e5000000-0000-0000-0000-000000000003");
    private static readonly Guid Email4 = Guid.Parse("e5000000-0000-0000-0000-000000000004");

    private sealed class Harness
    {
        public FakeDistributionRepository Repository { get; } = new();
        public FakePostedParticipantService Participants { get; } = new();
        public RecordingNotifyClient Notify { get; } = new();
        public MessageCapturingLogger<DistributionPostedNotifier> Logger { get; } = new();
        public DistributionPostedNotifier Notifier { get; }

        public Harness(
            bool enabled = true, string externalLink = "https://external.example.test", DateTimeOffset? now = null, INotifyClient? client = null)
        {
            Notifier = new DistributionPostedNotifier(
                Repository,
                Participants,
                client ?? Notify,
                Options.Create(new DistributionPostedEmailOptions { Enabled = enabled }),
                Options.Create(new ExternalOptions { AppUrl = externalLink }),
                new FixedTimeProvider(now ?? Now),
                Logger);
        }

        public void AddParticipant(Guid id, string email, string email2 = "") =>
            Participants.Participants[id] = new ParticipantEntity { ParticipantId = id, Email = email, Email2 = email2 };

        // Every address used in this file contains an "@", so this also catches an address no test thought of.
        public void AssertNoAddressesLogged() =>
            Logger.AssertNoneContain("primary@example.test", "secondary@example.test", "lab@example.test", "broken@example.test", "old@example.test", "@");

        public void AssertLogs(params (LogLevel Level, int EventId, string Message)[] expected) =>
            Assert.Equal(expected, Logger.Entries.Select(e => (e.Level, e.EventId.Id, e.Message)));
    }

    // Holds Notify inside the call until released, so a test can look at what happened meanwhile.
    private sealed class GatedNotifyClient : INotifyClient
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task SendEmailAsync(
            string templateId,
            string emailAddress,
            IReadOnlyDictionary<string, string>? personalisation = null,
            string? reference = null,
            CancellationToken cancellationToken = default)
        {
            Entered.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
        }
    }

    private static PostedParticipant Posted(Guid participantId, Guid? schemeId = null) =>
        new(participantId, schemeId ?? Scheme1, "17152A/BA", "PT0060: Mastitis bovine");

    private static DistributionPostedEmailEntity Email(Guid id, string address = "lab@example.test") =>
        new() { DistributionPostedEmailId = id, ParticipantId = Part1, MonthlyDistributionSchemeId = Scheme1, Email = address, Distribution = "17152A/BA", SchemeName = "PT0060: Mastitis bovine" };

    private static string SentLog(Guid emailId) =>
        $"Distribution Posted email sent. DistributionPostedEmailId={emailId} ParticipantId={Part1} MonthlyDistributionSchemeId={Scheme1} TemplateName=distribution-posted TemplateId={TemplateId}";

    private static string FailedLog(Guid emailId) =>
        $"Distribution Posted email could not be sent; it stays unsent. DistributionPostedEmailId={emailId} ParticipantId={Part1} MonthlyDistributionSchemeId={Scheme1} TemplateName=distribution-posted TemplateId={TemplateId}";

    private static string MarkFailedLog(Guid emailId) =>
        $"Distribution Posted email was sent but could not be marked sent; it must not be sent again. DistributionPostedEmailId={emailId} ParticipantId={Part1} MonthlyDistributionSchemeId={Scheme1}";

    private static string SkippedLog(string reason, Guid participantId) =>
        $"Distribution Posted email skipped: {reason}. ParticipantId={participantId} MonthlyDistributionSchemeId={Scheme1}";

    private static string SummaryLog(int queued, int sent, int skipped, int failed) =>
        $"Distribution Posted email run complete. Queued={queued} Sent={sent} Skipped={skipped} Failed={failed}";

    // ---------- BuildEmailsAsync ----------

    [Fact]
    public async Task BuildEmailsAsync_NothingPosted_ReturnsEmptyWithoutAnyLookup()
    {
        var h = new Harness();

        var emails = await h.Notifier.BuildEmailsAsync([]);

        Assert.Empty(emails);
        Assert.Empty(h.Participants.Requested);
        Assert.Empty(h.Logger.Entries);
    }

    [Fact]
    public async Task BuildEmailsAsync_UnknownParticipantIsSkipped()
    {
        var h = new Harness();

        var emails = await h.Notifier.BuildEmailsAsync([Posted(Part1)]);

        Assert.Empty(emails);
        Assert.Equal([Part1], h.Participants.Requested);
        h.AssertLogs((LogLevel.Warning, 113, SkippedLog("participant not found", Part1)));
    }

    [Fact]
    public async Task BuildEmailsAsync_UnknownParticipantDoesNotStopTheOthers()
    {
        var h = new Harness();
        h.AddParticipant(Part2, "primary@example.test");

        var emails = await h.Notifier.BuildEmailsAsync([Posted(Part1), Posted(Part2)]);

        var email = Assert.Single(emails);
        Assert.Equal((Part2, "primary@example.test"), (email.ParticipantId, email.Email));
        h.AssertLogs((LogLevel.Warning, 113, SkippedLog("participant not found", Part1)));
        h.AssertNoAddressesLogged();
    }

    [Fact]
    public async Task BuildEmailsAsync_QueuesOneEmailForEachOfPrimaryAndSecondaryAddress()
    {
        var h = new Harness();
        h.AddParticipant(Part1, "primary@example.test", "secondary@example.test");

        var emails = await h.Notifier.BuildEmailsAsync([Posted(Part1)]);

        Assert.Equal(["primary@example.test", "secondary@example.test"], emails.Select(e => e.Email));
        Assert.All(emails, e =>
        {
            Assert.NotEqual(Guid.Empty, e.DistributionPostedEmailId);
            Assert.Equal(Part1, e.ParticipantId);
            Assert.Equal(Scheme1, e.MonthlyDistributionSchemeId);
            Assert.Equal("17152A/BA", e.Distribution);
            Assert.Equal("PT0060: Mastitis bovine", e.SchemeName);
        });
        Assert.NotEqual(emails[0].DistributionPostedEmailId, emails[1].DistributionPostedEmailId);
        Assert.Empty(h.Logger.Entries);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public async Task BuildEmailsAsync_SecondaryAddressOnlyWhenPresent(string? secondary)
    {
        var h = new Harness();
        h.AddParticipant(Part1, "primary@example.test", secondary!);

        var emails = await h.Notifier.BuildEmailsAsync([Posted(Part1)]);

        Assert.Equal("primary@example.test", Assert.Single(emails).Email);
        Assert.Empty(h.Logger.Entries);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public async Task BuildEmailsAsync_BlankPrimaryWithASecondary_QueuesTheSecondaryOnly(string? primary)
    {
        var h = new Harness();
        h.AddParticipant(Part1, primary!, "secondary@example.test");

        var emails = await h.Notifier.BuildEmailsAsync([Posted(Part1)]);

        Assert.Equal("secondary@example.test", Assert.Single(emails).Email);
        Assert.Empty(h.Logger.Entries);
    }

    [Fact]
    public async Task BuildEmailsAsync_BlankPrimaryIsSkipped()
    {
        var h = new Harness();
        h.AddParticipant(Part1, "", "secondary@example.test");
        h.AddParticipant(Part2, " ", "");

        var emails = await h.Notifier.BuildEmailsAsync([Posted(Part1), Posted(Part2)]);

        Assert.Equal("secondary@example.test", Assert.Single(emails).Email);
        h.AssertLogs((LogLevel.Warning, 113, SkippedLog("no email address", Part2)));
        h.AssertNoAddressesLogged();
    }

    [Fact]
    public async Task BuildEmailsAsync_NoAddressAtAll_QueuesNothingAndWarnsForEachParticipant()
    {
        var h = new Harness();
        h.AddParticipant(Part1, "", "");
        h.AddParticipant(Part2, "  ", " ");

        var emails = await h.Notifier.BuildEmailsAsync([Posted(Part1), Posted(Part2)]);

        Assert.Empty(emails);
        h.AssertLogs(
            (LogLevel.Warning, 113, SkippedLog("no email address", Part1)),
            (LogLevel.Warning, 113, SkippedLog("no email address", Part2)));
    }

    [Fact]
    public async Task BuildEmailsAsync_UnsentRowsFromEarlierFailuresDoNotBlockANewEmail()
    {
        var h = new Harness();
        h.AddParticipant(Part1, "Lab@Example.test", "other@example.test");
        h.Repository.UnsentPostedEmails.Add(Email(Guid.NewGuid()));

        var emails = await h.Notifier.BuildEmailsAsync([Posted(Part1)]);

        Assert.Equal(["Lab@Example.test", "other@example.test"], emails.Select(e => e.Email));
    }

    [Fact]
    public async Task BuildEmailsAsync_NoDuplicatesWithinTheSameBatch()
    {
        var h = new Harness();
        h.AddParticipant(Part1, "lab@example.test", "LAB@example.test");

        var emails = await h.Notifier.BuildEmailsAsync([Posted(Part1), Posted(Part1)]);

        Assert.Equal("lab@example.test", Assert.Single(emails).Email);
    }

    [Fact]
    public async Task BuildEmailsAsync_PrimaryAndSecondaryTheSameAddress_QueuesOneEmail()
    {
        var h = new Harness();
        h.AddParticipant(Part1, "lab@example.test", "Lab@Example.test");

        var emails = await h.Notifier.BuildEmailsAsync([Posted(Part1)]);

        Assert.Equal("lab@example.test", Assert.Single(emails).Email);
    }

    [Fact]
    public async Task BuildEmailsAsync_SameParticipantPostedTwice_StillQueuesOneEmailPerAddress()
    {
        var h = new Harness();
        h.AddParticipant(Part1, "primary@example.test", "secondary@example.test");

        var emails = await h.Notifier.BuildEmailsAsync([Posted(Part1), Posted(Part1)]);

        Assert.Equal(["primary@example.test", "secondary@example.test"], emails.Select(e => e.Email));
    }

    [Fact]
    public async Task BuildEmailsAsync_TheSameAddressForADifferentSchemeOrParticipant_IsADifferentEmail()
    {
        var h = new Harness();
        h.AddParticipant(Part1, "lab@example.test");
        h.AddParticipant(Part2, "LAB@example.test");

        var emails = await h.Notifier.BuildEmailsAsync([Posted(Part1, Scheme1), Posted(Part1, Scheme2), Posted(Part2, Scheme1)]);

        Assert.Equal(
            [(Part1, Scheme1), (Part1, Scheme2), (Part2, Scheme1)],
            emails.Select(e => (e.ParticipantId, e.MonthlyDistributionSchemeId)));
        Assert.Equal(3, emails.Select(e => e.DistributionPostedEmailId).Distinct().Count());
    }

    // ---------- SendAsync ----------

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SendAsync_NoEmails_ReturnsEmptyAndDoesNothing(bool enabled)
    {
        var h = new Harness(enabled);

        var summary = await h.Notifier.SendAsync([]);

        Assert.Same(DistributionPostedRunSummary.Empty, summary);
        Assert.Empty(h.Notify.Sent);
        Assert.Empty(h.Repository.MarkedSentEmails);
        Assert.Empty(h.Logger.Entries);
    }

    [Fact]
    public async Task SendAsync_Disabled_SendsNothingAndLeavesEveryEmailQueued()
    {
        var h = new Harness(enabled: false);

        var summary = await h.Notifier.SendAsync([Email(Email1, "primary@example.test"), Email(Email2, "secondary@example.test")]);

        Assert.Equal(new DistributionPostedRunSummary(2, 0, 2, 0), summary);
        Assert.Empty(h.Notify.Tokens);
        Assert.Empty(h.Repository.MarkedSentEmails);
        Assert.Equal(0, h.Repository.UnsentPostedEmailRequests);
        h.AssertLogs((LogLevel.Information, 115, "Distribution Posted emails are switched off; they stay unsent. Queued=2"));
        h.AssertNoAddressesLogged();
    }

    [Fact]
    public async Task SendAsync_PrimaryAndSecondary_SendsTheTemplateWithTheLegacyPlaceholdersThenMarksEachSent()
    {
        var h = new Harness(externalLink: "https://external.example.test/ptl/external/");
        // Left unsent by an earlier failure: nothing retries it.
        h.Repository.UnsentPostedEmails.Add(Email(Email4, "old@example.test"));
        var markedBeforeEachSend = new List<int>();
        h.Notify.FailFor = _ =>
        {
            markedBeforeEachSend.Add(h.Repository.MarkedSentEmails.Count);
            return null;
        };

        var summary = await h.Notifier.SendAsync([Email(Email1, "primary@example.test"), Email(Email2, "secondary@example.test")]);

        Assert.Equal(new DistributionPostedRunSummary(2, 2, 0, 0), summary);
        Assert.Equal([0, 1], markedBeforeEachSend);
        Assert.Equal(["primary@example.test", "secondary@example.test"], h.Notify.Sent.Select(s => s.Email));
        Assert.All(h.Notify.Sent, s => Assert.Equal(TemplateId, s.TemplateId));
        Assert.Equal([Email1.ToString(), Email2.ToString()], h.Notify.Sent.Select(s => s.Reference));
        Assert.All(h.Notify.Sent, s => TabulationEmailTestData.AssertPersonalisation(
            s.Personalisation,
            ("scheme_name", "PT0060: Mastitis bovine"),
            ("distribution", "17152A/BA"),
            ("external_link", "https://external.example.test/ptl/external/"),
            ("date", "13/05/2026")));
        Assert.Equal([Email1, Email2], h.Repository.MarkedSentEmails.Select(m => m.Id));
        Assert.All(h.Repository.MarkedSentEmails, m => Assert.Equal(Now.UtcDateTime, m.DateSent));
        Assert.Equal(0, h.Repository.UnsentPostedEmailRequests);
        Assert.Empty(h.Participants.Requested);
        h.AssertLogs(
            (LogLevel.Information, 111, SentLog(Email1)),
            (LogLevel.Information, 111, SentLog(Email2)),
            (LogLevel.Information, 114, SummaryLog(2, 2, 0, 0)));
        h.AssertNoAddressesLogged();
    }

    [Fact]
    public async Task SendAsync_MarkingSentWaitsUntilNotifyHasReturned()
    {
        var gate = new GatedNotifyClient();
        var h = new Harness(client: gate);

        var run = h.Notifier.SendAsync([Email(Email1)]);
        await gate.Entered.Task.WaitAsync(Timeout);

        Assert.Empty(h.Repository.MarkedSentEmails);
        Assert.False(run.IsCompleted);

        gate.Release.SetResult();
        var summary = await run.WaitAsync(Timeout);

        Assert.Equal(new DistributionPostedRunSummary(1, 1, 0, 0), summary);
        Assert.Equal((Email1, Now.UtcDateTime), Assert.Single(h.Repository.MarkedSentEmails));
    }

    [Theory]
    [InlineData("2026-05-13T10:30:00+00:00", "13/05/2026")]
    [InlineData("2026-01-05T00:00:00+00:00", "05/01/2026")]
    [InlineData("2026-12-31T23:59:59+00:00", "31/12/2026")]
    public async Task SendAsync_DateIsTheDaySentInDayMonthYearFromTheInjectedClock(string utcNow, string expected)
    {
        var now = DateTimeOffset.Parse(utcNow, CultureInfo.InvariantCulture);
        var h = new Harness(now: now);

        await h.Notifier.SendAsync([Email(Email1)]);

        Assert.Equal(expected, Assert.Single(h.Notify.Sent).Personalisation!["date"]);
        Assert.Equal(now.UtcDateTime, Assert.Single(h.Repository.MarkedSentEmails).DateSent);
    }

    [Fact]
    public async Task SendAsync_DateFormatIgnoresTheCurrentCulture()
    {
        var original = CultureInfo.CurrentCulture;
        // The Thai calendar would write 2569.
        CultureInfo.CurrentCulture = new CultureInfo("th-TH");
        try
        {
            var h = new Harness();

            await h.Notifier.SendAsync([Email(Email1)]);

            Assert.Equal("13/05/2026", Assert.Single(h.Notify.Sent).Personalisation!["date"]);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public async Task SendAsync_PassesTheCallersTokenToNotify()
    {
        var h = new Harness();
        using var host = new CancellationTokenSource();

        await h.Notifier.SendAsync([Email(Email1), Email(Email2)], host.Token);

        Assert.Equal(2, h.Notify.Tokens.Count);
        Assert.All(h.Notify.Tokens, token => Assert.Equal(host.Token, token));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SendAsync_NotifyFails_LeavesTheEmailUnsentLogsAnErrorAndTheNextEmailStillGoes(bool timesOut)
    {
        var h = new Harness();
        using var host = new CancellationTokenSource();
        Exception failure = timesOut ? new TaskCanceledException("http timeout") : new InvalidOperationException("notify down");
        h.Notify.FailFor = address => address == "broken@example.test" ? failure : null;

        var summary = await h.Notifier.SendAsync([Email(Email1, "broken@example.test"), Email(Email2)], host.Token);

        Assert.Equal(new DistributionPostedRunSummary(2, 1, 0, 1), summary);
        Assert.Equal(["lab@example.test"], h.Notify.Sent.Select(s => s.Email));
        Assert.Equal([Email2], h.Repository.MarkedSentEmails.Select(m => m.Id));
        Assert.Same(failure, Assert.Single(h.Logger.At(LogLevel.Error)).Exception);
        h.AssertLogs(
            (LogLevel.Error, 112, FailedLog(Email1)),
            (LogLevel.Information, 111, SentLog(Email2)),
            (LogLevel.Information, 114, SummaryLog(2, 1, 0, 1)));
        h.AssertNoAddressesLogged();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SendAsync_HostStopsWhileSending_PropagatesWithoutMarkingOrLoggingAnything(bool taskCanceled)
    {
        var h = new Harness();
        using var host = new CancellationTokenSource();
        h.Notify.FailFor = _ =>
        {
            host.Cancel();
            return taskCanceled ? new TaskCanceledException("stopping", null, host.Token) : new OperationCanceledException(host.Token);
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => h.Notifier.SendAsync([Email(Email1), Email(Email2)], host.Token));

        // The second email is never attempted.
        Assert.Single(h.Notify.Tokens);
        Assert.Empty(h.Notify.Sent);
        Assert.Empty(h.Repository.MarkedSentEmails);
        Assert.Empty(h.Logger.Entries);
    }

    [Fact]
    public async Task SendAsync_HostStopsRightAfterNotifyAcceptedTheEmail_StillMarksItSent()
    {
        var h = new Harness();
        using var host = new CancellationTokenSource();
        h.Notify.FailFor = _ =>
        {
            host.Cancel();
            return null;
        };

        var summary = await h.Notifier.SendAsync([Email(Email1)], host.Token);

        Assert.Equal(new DistributionPostedRunSummary(1, 1, 0, 0), summary);
        Assert.Equal(Email1, Assert.Single(h.Repository.MarkedSentEmails).Id);
    }

    [Fact]
    public async Task SendAsync_MarkingSentFails_StillCountsTheEmailSentAndLogsTheError()
    {
        var h = new Harness();
        var failure = new InvalidOperationException("database down");
        h.Repository.PostedEmailSentError = failure;

        var summary = await h.Notifier.SendAsync([Email(Email1, "primary@example.test"), Email(Email2, "secondary@example.test")]);

        Assert.Equal(new DistributionPostedRunSummary(2, 2, 0, 0), summary);
        Assert.Equal(["primary@example.test", "secondary@example.test"], h.Notify.Sent.Select(s => s.Email));
        Assert.Empty(h.Repository.MarkedSentEmails);
        Assert.All(h.Logger.At(LogLevel.Error), e => Assert.Same(failure, e.Exception));
        h.AssertLogs(
            (LogLevel.Error, 116, MarkFailedLog(Email1)),
            (LogLevel.Information, 111, SentLog(Email1)),
            (LogLevel.Error, 116, MarkFailedLog(Email2)),
            (LogLevel.Information, 111, SentLog(Email2)),
            (LogLevel.Information, 114, SummaryLog(2, 2, 0, 0)));
        h.AssertNoAddressesLogged();
    }

    [Fact]
    public async Task SendAsync_MarkingSentIsCancelled_PropagatesInsteadOfBeingLoggedAsAMarkFailure()
    {
        var h = new Harness();
        h.Repository.PostedEmailSentError = new OperationCanceledException();

        await Assert.ThrowsAsync<OperationCanceledException>(() => h.Notifier.SendAsync([Email(Email1)]));

        Assert.Empty(h.Logger.Entries);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task SendAsync_BlankAddress_SkipsTheEmailWarnsAndTheNextEmailStillGoes(string? address)
    {
        var h = new Harness();

        var summary = await h.Notifier.SendAsync([Email(Email1, address!), Email(Email2)]);

        Assert.Equal(new DistributionPostedRunSummary(2, 1, 1, 0), summary);
        Assert.Equal(["lab@example.test"], h.Notify.Sent.Select(s => s.Email));
        Assert.Equal([Email2], h.Repository.MarkedSentEmails.Select(m => m.Id));
        h.AssertLogs(
            (LogLevel.Warning, 113, SkippedLog("no email address", Part1)),
            (LogLevel.Information, 111, SentLog(Email2)),
            (LogLevel.Information, 114, SummaryLog(2, 1, 1, 0)));
        h.AssertNoAddressesLogged();
    }

    [Fact]
    public async Task SendAsync_MixedOutcomes_CountsEachOfThemInTheSummaryAndTheRunLog()
    {
        var h = new Harness();
        h.Notify.FailFor = address => address == "broken@example.test" ? new InvalidOperationException("notify down") : null;

        var summary = await h.Notifier.SendAsync(
        [
            Email(Email1, "primary@example.test"),
            Email(Email2, "broken@example.test"),
            Email(Email3, ""),
            Email(Email4, "secondary@example.test"),
        ]);

        Assert.Equal(new DistributionPostedRunSummary(4, 2, 1, 1), summary);
        Assert.Equal([Email1, Email4], h.Repository.MarkedSentEmails.Select(m => m.Id));
        var last = h.Logger.Entries[^1];
        Assert.Equal((LogLevel.Information, 114, SummaryLog(4, 2, 1, 1)), (last.Level, last.EventId.Id, last.Message));
        h.AssertNoAddressesLogged();
    }
}

public class DistributionPostedEmailOptionsTests
{
    [Fact]
    public void DistributionPostedEmailOptions_Defaults_AreEnabledUnderTheLegacySwitchSection()
    {
        Assert.Equal("DistributionPostedEmails", DistributionPostedEmailOptions.SectionName);
        Assert.True(new DistributionPostedEmailOptions().Enabled);
    }

    [Fact]
    public void DistributionPostedEmailTemplates_AreTheFixedNotifyTemplate()
    {
        Assert.Equal(("distribution-posted", "2bb0e2d0-fa23-472e-947c-4524d14ad288"), (DistributionPostedEmailTemplates.Name, DistributionPostedEmailTemplates.Id));
    }

    [Fact]
    public void DistributionPostedRunSummary_Empty_IsAllZero()
    {
        Assert.Equal(new DistributionPostedRunSummary(0, 0, 0, 0), DistributionPostedRunSummary.Empty);
    }
}
