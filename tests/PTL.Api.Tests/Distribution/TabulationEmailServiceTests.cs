using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PTL.Core.Configuration;
using PTL.Core.Distribution;
using PTL.Core.Notifications;
using static PTL.Api.Tests.Distribution.TabulationEmailTestData;

namespace PTL.Api.Tests.Distribution;

// Legacy email.vb ProcessTestConsultantEmails: the first email to Test Consultant 1 and the chaser to every
// allocated Test Consultant, sent through GOV.UK Notify; EmailState moves only after Notify accepts the email.
public class TabulationEmailServiceTests
{
    private const string WebAddressPrefix = "You can access the tabulation at the following address: ";

    private sealed class Harness
    {
        public FakeDistributionRepository Repository { get; } = new();
        public RecordingNotifyClient Notify { get; } = new();
        public TabulationEmailGuard Guard { get; } = new();
        public MessageCapturingLogger<TabulationEmailService> Logger { get; } = new();
        public TabulationEmailOptions EmailOptions { get; } = new();
        public SettableTimeProvider Clock { get; }
        public TabulationEmailService Service { get; }

        public Harness(
            Action<TabulationEmailOptions>? configure = null,
            DateTimeOffset? now = null,
            INotifyClient? notify = null,
            string internalUrl = "https://internal.example.test",
            string externalUrl = "https://external.example.test")
        {
            configure?.Invoke(EmailOptions);
            Clock = new SettableTimeProvider(now ?? NowUtc);
            Service = new TabulationEmailService(
                Repository,
                notify ?? Notify,
                Guard,
                Options.Create(EmailOptions),
                new TabulationEmailContext(
                    Clock,
                    Options.Create(EmailOptions),
                    Options.Create(new InternalOptions { AppUrl = internalUrl }),
                    Options.Create(new ExternalOptions { AppUrl = externalUrl })),
                Logger);
        }

        public void AddStandardConsultants() =>
            Repository.EmailConsultants.AddRange(
            [
                Consultant(Tc1, "Alice Internal", "Epidemiology", Tc1Email),
                Consultant(Tc2, "Bob External", null, Tc2Email, external: true),
                Consultant(Tc3, "Carol Internal", "Pathology", Tc3Email),
            ]);

        public void AssertNoAddressesLogged() => Logger.AssertNoneContain(Tc1Email, Tc2Email, Tc3Email, "@");

        public void AssertLockReleased() => Assert.Equal(1, Guard.Lock.CurrentCount);
    }

    // ---- DueStage ----

    [Theory]
    [InlineData(0, -1, null)]
    [InlineData(0, 0, TabulationEmailType.FirstRequest)]
    [InlineData(0, 3, TabulationEmailType.FirstRequest)]
    [InlineData(0, 7, TabulationEmailType.FirstRequest)]
    [InlineData(0, 8, TabulationEmailType.Chaser)]
    [InlineData(0, 9, TabulationEmailType.Chaser)]
    [InlineData(0, 10, null)]
    [InlineData(0, 30, null)]
    [InlineData(1, -1, null)]
    [InlineData(1, 0, null)]
    [InlineData(1, 7, null)]
    [InlineData(1, 8, TabulationEmailType.Chaser)]
    [InlineData(1, 9, TabulationEmailType.Chaser)]
    [InlineData(1, 10, null)]
    [InlineData(2, 0, null)]
    [InlineData(2, 7, null)]
    [InlineData(2, 8, null)]
    [InlineData(2, 9, null)]
    [InlineData(2, 10, null)]
    public void DueStage_ByEmailStateAndDaysSinceReady_ReturnsTheStageDue(int emailState, int daysSinceReady, TabulationEmailType? expected)
    {
        var candidate = Candidate(Mds1, daysSinceReady, emailState);

        Assert.Equal(expected, TabulationEmailService.DueStage(candidate, Today));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 8)]
    [InlineData(1, 8)]
    [InlineData(2, 9)]
    public void DueStage_CommentsSubmitted_IsNeverDue(int emailState, int daysSinceReady)
    {
        var candidate = Candidate(Mds1, daysSinceReady, emailState, commentSubmitted: true);

        Assert.Null(TabulationEmailService.DueStage(candidate, Today));
    }

    [Fact]
    public void DueStage_UsesTheCalendarDateOfTheReadyDateNotTheTime()
    {
        var candidate = Candidate(Mds1, daysSinceReady: 8);
        candidate.IsReadyForTestConsultant = Today.AddDays(-8).AddHours(23).AddMinutes(59);

        Assert.Equal(TabulationEmailType.Chaser, TabulationEmailService.DueStage(candidate, Today));
    }

    [Fact]
    public void DueStage_NullCandidate_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => TabulationEmailService.DueStage(null!, Today));
    }

    // ---- First email ----

    [Fact]
    public async Task ProcessScheduledEmailsAsync_FirstRequest_SendsTheTemplateToTestConsultantOneOnly()
    {
        var h = new Harness();
        h.AddStandardConsultants();
        h.Repository.EmailCandidates.Add(Candidate(Mds1, daysSinceReady: 3, tc1: Tc1, tc2: Tc2, tc3: Tc3));

        var summary = await h.Service.ProcessScheduledEmailsAsync();

        Assert.Equal(new TabulationEmailRunSummary(1, 1, 0, 0, 0), summary);
        var sent = Assert.Single(h.Notify.Sent);
        Assert.Equal(("a2da705b-46b7-47cc-9ec7-00d5eec94f2d", Tc1Email, $"{Mds1}:first"), (sent.TemplateId, sent.Email, sent.Reference));
        AssertPersonalisation(
            sent.Personalisation,
            ("DisplayName", "Alice Internal, Epidemiology"),
            ("Commenter", "Alice Internal"),
            ("SchemeName", "PT0060: Mastitis bovine"),
            ("MonthlyDistributionReferenceFull", "17152A/BA"),
            ("CommentDeadlineDays", "7"),
            ("WebAddress", $"{WebAddressPrefix}https://internal.example.test/Test%20Consultant/Tabulation?MonthlyDistributionSchemeId={Mds1}"),
            ("DateTimeNow", "12/05/2026"));
        Assert.Equal([(Mds1, 1)], h.Repository.EmailStates);
        var information = Assert.Single(h.Logger.At(LogLevel.Information), e => e.Message.StartsWith("Tabulation email sent", StringComparison.Ordinal));
        Assert.Contains("FirstRequest", information.Message, StringComparison.Ordinal);
        h.AssertNoAddressesLogged();
        h.AssertLockReleased();
    }

    [Fact]
    public async Task ProcessScheduledEmailsAsync_FirstRequestToAnExternalConsultant_LinksTheExternalCommentsPage()
    {
        var h = new Harness(o => o.ExternalCommentsPath = "/CommentsEntry/", externalUrl: "https://external.example.test/");
        h.AddStandardConsultants();
        h.Repository.EmailCandidates.Add(Candidate(Mds1, daysSinceReady: 0, tc1: Tc2));

        await h.Service.ProcessScheduledEmailsAsync();

        var sent = Assert.Single(h.Notify.Sent);
        Assert.Equal(Tc2Email, sent.Email);
        Assert.Equal("Bob External", sent.Personalisation!["DisplayName"]);
        Assert.Equal($"{WebAddressPrefix}https://external.example.test/CommentsEntry?ref={Mds1}", sent.Personalisation["WebAddress"]);
    }

    [Fact]
    public async Task ProcessScheduledEmailsAsync_FirstRequest_TrimsSlashesFromTheInternalLink()
    {
        var h = new Harness(o => o.InternalCommentsPath = "/Test%20Consultant/Tabulation/", internalUrl: "https://internal.example.test/");
        h.AddStandardConsultants();
        h.Repository.EmailCandidates.Add(Candidate(Mds1, daysSinceReady: 0));

        await h.Service.ProcessScheduledEmailsAsync();

        Assert.Equal(
            $"{WebAddressPrefix}https://internal.example.test/Test%20Consultant/Tabulation?MonthlyDistributionSchemeId={Mds1}",
            Assert.Single(h.Notify.Sent).Personalisation!["WebAddress"]);
    }

    [Theory]
    [InlineData("Epidemiology", "Alice Internal, Epidemiology")]
    [InlineData("", "Alice Internal")]
    [InlineData(null, "Alice Internal")]
    public async Task ProcessScheduledEmailsAsync_FirstRequest_DisplayNameAppendsTheDepartmentOnlyWhenPresent(string? department, string expected)
    {
        var h = new Harness();
        h.Repository.EmailConsultants.Add(Consultant(Tc1, "Alice Internal", department, Tc1Email));
        h.Repository.EmailCandidates.Add(Candidate(Mds1, daysSinceReady: 1));

        await h.Service.ProcessScheduledEmailsAsync();

        Assert.Equal(expected, Assert.Single(h.Notify.Sent).Personalisation!["DisplayName"]);
    }

    [Fact]
    public async Task ProcessScheduledEmailsAsync_FirstRequestAroundMidnight_FormatsDateTimeNowInTheConfiguredTimeZone()
    {
        // 23:30 UTC on 11 May is already 00:30 on 12 May in London (BST).
        var midnight = new DateTimeOffset(2026, 5, 11, 23, 30, 0, TimeSpan.Zero);

        var london = new Harness(now: midnight);
        london.AddStandardConsultants();
        london.Repository.EmailCandidates.Add(Candidate(Mds1, daysSinceReady: 0));
        await london.Service.ProcessScheduledEmailsAsync();

        var utc = new Harness(o => o.TimeZone = "UTC", now: midnight);
        utc.AddStandardConsultants();
        var utcCandidate = Candidate(Mds1, daysSinceReady: 0);
        utcCandidate.IsReadyForTestConsultant = new DateTime(2026, 5, 11, 9, 0, 0);
        utc.Repository.EmailCandidates.Add(utcCandidate);
        await utc.Service.ProcessScheduledEmailsAsync();

        Assert.Equal("12/05/2026", Assert.Single(london.Notify.Sent).Personalisation!["DateTimeNow"]);
        Assert.Equal("11/05/2026", Assert.Single(utc.Notify.Sent).Personalisation!["DateTimeNow"]);
    }

    [Fact]
    public async Task ProcessScheduledEmailsAsync_FirstRequestNotifyFails_DoesNotAdvanceTheStateAndRetriesOnTheNextRun()
    {
        var h = new Harness();
        h.AddStandardConsultants();
        h.Repository.EmailCandidates.Add(Candidate(Mds1, daysSinceReady: 2));
        h.Notify.FailFor = _ => new InvalidOperationException("notify down");

        var failed = await h.Service.ProcessScheduledEmailsAsync();

        Assert.Equal(new TabulationEmailRunSummary(1, 0, 0, 0, 1), failed);
        Assert.Empty(h.Repository.EmailStates);
        var error = Assert.Single(h.Logger.At(LogLevel.Error));
        Assert.IsType<InvalidOperationException>(error.Exception);
        Assert.Contains("a2da705b-46b7-47cc-9ec7-00d5eec94f2d", error.Message, StringComparison.Ordinal);

        h.Notify.FailFor = null;
        var retried = await h.Service.ProcessScheduledEmailsAsync();

        Assert.Equal(new TabulationEmailRunSummary(1, 1, 0, 0, 0), retried);
        Assert.Equal([(Mds1, 1)], h.Repository.EmailStates);
        h.AssertNoAddressesLogged();
    }

    [Theory]
    [InlineData("unassigned")]
    [InlineData("empty-guid")]
    [InlineData("not-in-list")]
    [InlineData("null-email")]
    [InlineData("empty-email")]
    [InlineData("blank-email")]
    public async Task ProcessScheduledEmailsAsync_FirstRequestWithoutRecipient_SkipsAndWarnsOncePerSchemePerDay(string scenario)
    {
        var h = new Harness();
        h.AddStandardConsultants();
        var candidate = Candidate(Mds1, daysSinceReady: 1);
        switch (scenario)
        {
            case "unassigned":
                candidate.TestConsultant1Id = null;
                break;
            case "empty-guid":
                candidate.TestConsultant1Id = Guid.Empty;
                break;
            case "not-in-list":
                candidate.TestConsultant1Id = Guid.NewGuid();
                break;
            case "null-email":
                h.Repository.EmailConsultants[0].Email = null;
                break;
            case "empty-email":
                h.Repository.EmailConsultants[0].Email = string.Empty;
                break;
            default:
                h.Repository.EmailConsultants[0].Email = "   ";
                break;
        }

        h.Repository.EmailCandidates.Add(candidate);

        var first = await h.Service.ProcessScheduledEmailsAsync();
        var second = await h.Service.ProcessScheduledEmailsAsync();

        Assert.Equal(new TabulationEmailRunSummary(1, 0, 0, 1, 0), first);
        Assert.Equal(first, second);
        Assert.Empty(h.Notify.Sent);
        Assert.Empty(h.Repository.EmailStates);
        var warning = Assert.Single(h.Logger.At(LogLevel.Warning));
        Assert.Contains(warning.State, kv => kv.Key == "Slot" && Equals(kv.Value, 1));
        Assert.Contains("no recipient", warning.Message, StringComparison.Ordinal);

        // A new day is a new warning.
        h.Clock.Now = h.Clock.Now.AddDays(1);
        await h.Service.ProcessScheduledEmailsAsync();
        Assert.Equal(2, h.Logger.At(LogLevel.Warning).Count());
        h.AssertNoAddressesLogged();
    }

    [Fact]
    public async Task ProcessScheduledEmailsAsync_DuplicateConsultantIds_FirstRowWins()
    {
        var h = new Harness();
        h.Repository.EmailConsultants.Add(Consultant(Tc1, "Alice Internal", null, Tc1Email));
        h.Repository.EmailConsultants.Add(Consultant(Tc1, "Alice External", null, Tc2Email, external: true));
        h.Repository.EmailCandidates.Add(Candidate(Mds1, daysSinceReady: 0));

        await h.Service.ProcessScheduledEmailsAsync();

        Assert.Equal(Tc1Email, Assert.Single(h.Notify.Sent).Email);
    }

    // ---- Chaser ----

    private static Harness ChaserHarness(Action<TabulationEmailCandidateEntity>? configure = null, int state = 1, int days = 8)
    {
        var h = new Harness();
        h.AddStandardConsultants();
        var candidate = Candidate(Mds1, days, state, tc1: Tc1, tc2: Tc2, tc3: Tc3);
        configure?.Invoke(candidate);
        h.Repository.EmailCandidates.Add(candidate);
        return h;
    }

    [Fact]
    public async Task ProcessScheduledEmailsAsync_Chaser_EmailsEveryAllocatedConsultantThenAdvancesTheState()
    {
        var h = ChaserHarness();

        var summary = await h.Service.ProcessScheduledEmailsAsync();

        Assert.Equal(new TabulationEmailRunSummary(1, 0, 3, 0, 0), summary);
        Assert.Equal([Tc1Email, Tc2Email, Tc3Email], h.Notify.Sent.Select(s => s.Email));
        Assert.All(h.Notify.Sent, s => Assert.Equal("ca746cae-e71d-47f9-a263-f246e8d60edc", s.TemplateId));
        Assert.Equal([$"{Mds1}:chaser:1", $"{Mds1}:chaser:2", $"{Mds1}:chaser:3"], h.Notify.Sent.Select(s => s.Reference));
        Assert.All(h.Notify.Sent, s => Assert.Equal("Alice Internal", s.Personalisation!["Commenter"]));
        Assert.Equal(["Alice Internal, Epidemiology", "Bob External", "Carol Internal, Pathology"], h.Notify.Sent.Select(s => s.Personalisation!["DisplayName"]));
        AssertPersonalisation(
            h.Notify.Sent[1].Personalisation,
            ("DisplayName", "Bob External"),
            ("Commenter", "Alice Internal"),
            ("SchemeName", "PT0060: Mastitis bovine"),
            ("MonthlyDistributionReferenceFull", "17152A/BA"),
            ("CommentDeadlineDays", "2"),
            ("WebAddress", $"{WebAddressPrefix}https://external.example.test/CommentsEntry?ref={Mds1}"),
            ("DateTimeNow", "12/05/2026"));
        Assert.Equal(
            $"{WebAddressPrefix}https://internal.example.test/Test%20Consultant/Tabulation?MonthlyDistributionSchemeId={Mds1}",
            h.Notify.Sent[2].Personalisation!["WebAddress"]);
        Assert.Equal([(Mds1, 2)], h.Repository.EmailStates);
        Assert.All([1, 2, 3], slot => Assert.False(h.Guard.ChaserSent(Mds1, slot)));
        h.AssertNoAddressesLogged();
    }

    [Fact]
    public async Task ProcessScheduledEmailsAsync_ChaserWhenFirstEmailWasNeverSent_GoesStraightToTheChaser()
    {
        var h = ChaserHarness(state: 0, days: 9);

        var summary = await h.Service.ProcessScheduledEmailsAsync();

        Assert.Equal(new TabulationEmailRunSummary(1, 0, 3, 0, 0), summary);
        Assert.DoesNotContain(h.Notify.Sent, s => s.TemplateId == "a2da705b-46b7-47cc-9ec7-00d5eec94f2d");
        Assert.Equal([(Mds1, 2)], h.Repository.EmailStates);
    }

    [Fact]
    public async Task ProcessScheduledEmailsAsync_ChaserWithUnallocatedSlots_SkipsThemSilently()
    {
        var h = ChaserHarness(c =>
        {
            c.TestConsultant2Id = null;
            c.TestConsultant3Id = Guid.Empty;
        });

        var summary = await h.Service.ProcessScheduledEmailsAsync();

        Assert.Equal(new TabulationEmailRunSummary(1, 0, 1, 0, 0), summary);
        Assert.Equal([Tc1Email], h.Notify.Sent.Select(s => s.Email));
        Assert.Equal([(Mds1, 2)], h.Repository.EmailStates);
        Assert.Empty(h.Logger.At(LogLevel.Warning));
    }

    [Fact]
    public async Task ProcessScheduledEmailsAsync_ChaserNotifyFailsForOneConsultant_RetriesOnlyThatConsultantNextRun()
    {
        var h = ChaserHarness();
        h.Notify.FailFor = email => email == Tc2Email ? new InvalidOperationException("notify down") : null;

        var first = await h.Service.ProcessScheduledEmailsAsync();

        Assert.Equal(new TabulationEmailRunSummary(1, 0, 2, 0, 1), first);
        Assert.Equal([Tc1Email, Tc3Email], h.Notify.Sent.Select(s => s.Email));
        Assert.Empty(h.Repository.EmailStates);
        Assert.True(h.Guard.ChaserSent(Mds1, 1));
        Assert.False(h.Guard.ChaserSent(Mds1, 2));
        Assert.True(h.Guard.ChaserSent(Mds1, 3));
        Assert.Single(h.Logger.At(LogLevel.Error));

        h.Notify.FailFor = null;
        h.Notify.Sent.Clear();
        var second = await h.Service.ProcessScheduledEmailsAsync();

        Assert.Equal(new TabulationEmailRunSummary(1, 0, 1, 0, 0), second);
        var retried = Assert.Single(h.Notify.Sent);
        Assert.Equal((Tc2Email, $"{Mds1}:chaser:2"), (retried.Email, retried.Reference));
        Assert.Equal([(Mds1, 2)], h.Repository.EmailStates);
        Assert.All([1, 2, 3], slot => Assert.False(h.Guard.ChaserSent(Mds1, slot)));

        h.Notify.Sent.Clear();
        Assert.Equal(TabulationEmailRunSummary.Empty, await h.Service.ProcessScheduledEmailsAsync());
        Assert.Empty(h.Notify.Sent);
        h.AssertNoAddressesLogged();
    }

    [Fact]
    public async Task ProcessScheduledEmailsAsync_ChaserConsultantWithoutEmail_DoesNotAdvanceUntilTheyCanBeEmailed()
    {
        var h = ChaserHarness();
        h.Repository.EmailConsultants[1].Email = " ";

        var first = await h.Service.ProcessScheduledEmailsAsync();

        Assert.Equal(new TabulationEmailRunSummary(1, 0, 2, 1, 0), first);
        Assert.Equal([Tc1Email, Tc3Email], h.Notify.Sent.Select(s => s.Email));
        Assert.Empty(h.Repository.EmailStates);
        var warning = Assert.Single(h.Logger.At(LogLevel.Warning));
        Assert.Contains(warning.State, kv => kv.Key == "Slot" && Equals(kv.Value, 2));

        h.Notify.Sent.Clear();
        var second = await h.Service.ProcessScheduledEmailsAsync();

        Assert.Equal(new TabulationEmailRunSummary(1, 0, 0, 1, 0), second);
        Assert.Empty(h.Notify.Sent);
        Assert.Empty(h.Repository.EmailStates);
        Assert.Single(h.Logger.At(LogLevel.Warning));

        h.Repository.EmailConsultants[1].Email = Tc2Email;
        var third = await h.Service.ProcessScheduledEmailsAsync();

        Assert.Equal(new TabulationEmailRunSummary(1, 0, 1, 0, 0), third);
        Assert.Equal([Tc2Email], h.Notify.Sent.Select(s => s.Email));
        Assert.Equal([(Mds1, 2)], h.Repository.EmailStates);
        h.AssertNoAddressesLogged();
    }

    [Fact]
    public async Task ProcessScheduledEmailsAsync_ChaserAllocatedConsultantNotInTheList_IsSkippedAndTheStateStays()
    {
        var h = ChaserHarness(c => c.TestConsultant3Id = Guid.NewGuid());

        var summary = await h.Service.ProcessScheduledEmailsAsync();

        Assert.Equal(new TabulationEmailRunSummary(1, 0, 2, 1, 0), summary);
        Assert.Empty(h.Repository.EmailStates);
    }

    [Fact]
    public async Task ProcessScheduledEmailsAsync_ChaserWithoutTestConsultantOne_SkipsTheWholeChaserWithAWarning()
    {
        var h = ChaserHarness(c => c.TestConsultant1Id = Guid.NewGuid());

        var summary = await h.Service.ProcessScheduledEmailsAsync();
        await h.Service.ProcessScheduledEmailsAsync();

        Assert.Equal(new TabulationEmailRunSummary(1, 0, 0, 1, 0), summary);
        Assert.Empty(h.Notify.Sent);
        Assert.Empty(h.Repository.EmailStates);
        Assert.Single(h.Logger.At(LogLevel.Warning));
    }

    [Fact]
    public async Task ProcessScheduledEmailsAsync_ChaserWithTestConsultantOneUnassigned_SkipsTheWholeChaser()
    {
        var h = ChaserHarness(c => c.TestConsultant1Id = null);

        var summary = await h.Service.ProcessScheduledEmailsAsync();

        Assert.Equal(new TabulationEmailRunSummary(1, 0, 0, 1, 0), summary);
        Assert.Empty(h.Notify.Sent);
    }

    // ---- SendFirstEmailsAsync (immediate path) ----

    [Fact]
    public async Task SendFirstEmailsAsync_NullList_Throws()
    {
        var h = new Harness();

        await Assert.ThrowsAsync<ArgumentNullException>(() => h.Service.SendFirstEmailsAsync(null!));
    }

    [Fact]
    public async Task SendFirstEmailsAsync_Disabled_DoesNothing()
    {
        var h = new Harness(o => o.Enabled = false);
        h.AddStandardConsultants();
        h.Repository.EmailCandidates.Add(Candidate(Mds1));

        await h.Service.SendFirstEmailsAsync([Mds1]);

        Assert.Empty(h.Repository.EmailCandidateRequests);
        Assert.Empty(h.Notify.Sent);
    }

    [Fact]
    public async Task SendFirstEmailsAsync_EmptyList_DoesNothing()
    {
        var h = new Harness();
        h.AddStandardConsultants();
        h.Repository.EmailCandidates.Add(Candidate(Mds1));

        await h.Service.SendFirstEmailsAsync([]);

        Assert.Empty(h.Repository.EmailCandidateRequests);
        Assert.Empty(h.Notify.Sent);
    }

    [Fact]
    public async Task SendFirstEmailsAsync_SendsOnlyTheListedSchemesAndNeverAChaser()
    {
        var h = new Harness();
        h.AddStandardConsultants();
        h.Repository.EmailCandidates.AddRange(
        [
            Candidate(Mds1, daysSinceReady: 0),
            Candidate(Mds2, daysSinceReady: 0),
            Candidate(Mds3, daysSinceReady: 8, emailState: 1, tc2: Tc2),
            Candidate(Mds4, daysSinceReady: 0, commentSubmitted: true),
        ]);

        await h.Service.SendFirstEmailsAsync([Mds1, Mds3, Mds4, Guid.NewGuid()]);

        var sent = Assert.Single(h.Notify.Sent);
        Assert.Equal(("a2da705b-46b7-47cc-9ec7-00d5eec94f2d", Tc1Email, $"{Mds1}:first"), (sent.TemplateId, sent.Email, sent.Reference));
        Assert.Equal([(Mds1, 1)], h.Repository.EmailStates);
        Assert.Equal(1, h.Repository.ConsultantLoads);
        Assert.Equal([(new DateTime(2026, 5, 3), new DateTime(2026, 5, 13))], h.Repository.EmailCandidateRequests);
        h.AssertLockReleased();
    }

    [Fact]
    public async Task SendFirstEmailsAsync_ListedSchemeAlreadyEmailed_SendsNothing()
    {
        var h = new Harness();
        h.AddStandardConsultants();
        h.Repository.EmailCandidates.Add(Candidate(Mds1, daysSinceReady: 0, emailState: 1));

        await h.Service.SendFirstEmailsAsync([Mds1]);

        Assert.Empty(h.Notify.Sent);
        Assert.Equal(0, h.Repository.ConsultantLoads);
    }

    [Fact]
    public async Task SendFirstEmailsAsync_RepositoryFails_LogsAndDoesNotThrow()
    {
        var h = new Harness();
        var failure = new InvalidOperationException("database down");
        h.Repository.EmailCandidatesError = failure;

        await h.Service.SendFirstEmailsAsync([Mds1]);

        var error = Assert.Single(h.Logger.At(LogLevel.Error));
        Assert.Same(failure, error.Exception);
        Assert.Contains("scheduler will retry", error.Message, StringComparison.Ordinal);
        h.AssertLockReleased();
    }

    [Fact]
    public async Task SendFirstEmailsAsync_NotifyFails_LogsLeavesTheStateAndDoesNotThrow()
    {
        var h = new Harness();
        h.AddStandardConsultants();
        h.Repository.EmailCandidates.Add(Candidate(Mds1));
        h.Notify.FailFor = _ => new InvalidOperationException("notify down");

        await h.Service.SendFirstEmailsAsync([Mds1]);

        Assert.Empty(h.Repository.EmailStates);
        Assert.Single(h.Logger.At(LogLevel.Error));
        h.AssertNoAddressesLogged();
    }

    [Fact]
    public async Task SendFirstEmailsAsync_StateWriteFails_LogsAndDoesNotThrow()
    {
        var h = new Harness();
        h.AddStandardConsultants();
        h.Repository.EmailCandidates.Add(Candidate(Mds1));
        h.Repository.EmailStateError = new InvalidOperationException("database down");

        await h.Service.SendFirstEmailsAsync([Mds1]);

        Assert.Equal(Tc1Email, Assert.Single(h.Notify.Sent).Email);
        Assert.Equal(1, h.Logger.At(LogLevel.Error).Count(e => e.Exception is InvalidOperationException));
        h.AssertLockReleased();
    }

    [Fact]
    public async Task SendFirstEmailsAsync_AlreadyCancelled_PropagatesWithoutTakingTheLock()
    {
        var h = new Harness();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => h.Service.SendFirstEmailsAsync([Mds1], cts.Token));

        Assert.Empty(h.Repository.EmailCandidateRequests);
        h.AssertLockReleased();
    }

    [Fact]
    public async Task SendFirstEmailsAsync_CancelledWhileLoadingCandidates_PropagatesAndReleasesTheLock()
    {
        var h = new Harness();
        h.Repository.EmailCandidatesError = new OperationCanceledException();

        await Assert.ThrowsAsync<OperationCanceledException>(() => h.Service.SendFirstEmailsAsync([Mds1]));

        Assert.Empty(h.Logger.At(LogLevel.Error));
        h.AssertLockReleased();
    }

    [Fact]
    public async Task SendFirstEmailsAsync_CancelledWhileSending_PropagatesAndReleasesTheLock()
    {
        var h = new Harness();
        h.AddStandardConsultants();
        h.Repository.EmailCandidates.Add(Candidate(Mds1));
        h.Notify.FailFor = _ => new OperationCanceledException();

        await Assert.ThrowsAsync<OperationCanceledException>(() => h.Service.SendFirstEmailsAsync([Mds1]));

        Assert.Empty(h.Repository.EmailStates);
        Assert.Empty(h.Logger.At(LogLevel.Error));
        h.AssertLockReleased();
    }

    // ---- shared lock ----

    [Fact]
    public async Task ProcessScheduledEmailsAsync_WhileTheGuardLockIsHeld_WaitsForIt()
    {
        var h = new Harness();
        await h.Guard.Lock.WaitAsync();

        var run = h.Service.ProcessScheduledEmailsAsync();
        await Task.Delay(50);

        Assert.False(run.IsCompleted);
        Assert.Empty(h.Repository.EmailCandidateRequests);

        h.Guard.Lock.Release();
        await run;

        Assert.Single(h.Repository.EmailCandidateRequests);
        h.AssertLockReleased();
    }

    [Fact]
    public async Task SendFirstEmailsAsync_WhileTheGuardLockIsHeld_WaitsForIt()
    {
        var h = new Harness();
        await h.Guard.Lock.WaitAsync();

        var run = h.Service.SendFirstEmailsAsync([Mds1]);
        await Task.Delay(50);

        Assert.False(run.IsCompleted);
        Assert.Empty(h.Repository.EmailCandidateRequests);

        h.Guard.Lock.Release();
        await run;

        Assert.Single(h.Repository.EmailCandidateRequests);
    }

    [Fact]
    public async Task ConcurrentImmediateAndScheduledRuns_NeverSendTheSameEmailTwice()
    {
        var slow = new SlowNotifyClient();
        var h = new Harness(notify: slow);
        h.AddStandardConsultants();
        h.Repository.EmailCandidates.AddRange([Candidate(Mds1, daysSinceReady: 0), Candidate(Mds2, daysSinceReady: 1)]);

        await Task.WhenAll(
            h.Service.SendFirstEmailsAsync([Mds1, Mds2]),
            h.Service.ProcessScheduledEmailsAsync(),
            h.Service.SendFirstEmailsAsync([Mds1, Mds2]),
            h.Service.ProcessScheduledEmailsAsync());

        Assert.Equal(2, slow.Sent.Count);
        Assert.Equal([$"{Mds1}:first", $"{Mds2}:first"], slow.Sent.Select(s => s.Reference).Order(StringComparer.Ordinal));
        Assert.Equal(2, h.Repository.EmailStates.Count);
        h.AssertLockReleased();
    }

    // ---- ProcessScheduledEmailsAsync ----

    [Fact]
    public async Task ProcessScheduledEmailsAsync_Disabled_ReturnsEmptyWithoutTouchingTheRepository()
    {
        var h = new Harness(o => o.Enabled = false);
        h.AddStandardConsultants();
        h.Repository.EmailCandidates.Add(Candidate(Mds1));

        var summary = await h.Service.ProcessScheduledEmailsAsync();

        Assert.Same(TabulationEmailRunSummary.Empty, summary);
        Assert.Empty(h.Repository.EmailCandidateRequests);
        Assert.Equal(0, h.Repository.ConsultantLoads);
        Assert.Empty(h.Notify.Sent);
        Assert.Empty(h.Logger.Entries);
    }

    [Fact]
    public async Task ProcessScheduledEmailsAsync_QueriesTheNineDayWindowEndingTomorrowInTheConfiguredZone()
    {
        // 23:30 UTC on 11 May is 12 May in London but still 11 May in UTC.
        var midnight = new DateTimeOffset(2026, 5, 11, 23, 30, 0, TimeSpan.Zero);
        var london = new Harness(now: midnight);
        var utc = new Harness(o => o.TimeZone = "UTC", now: midnight);

        await london.Service.ProcessScheduledEmailsAsync();
        await utc.Service.ProcessScheduledEmailsAsync();

        Assert.Equal([(new DateTime(2026, 5, 3), new DateTime(2026, 5, 13))], london.Repository.EmailCandidateRequests);
        Assert.Equal([(new DateTime(2026, 5, 2), new DateTime(2026, 5, 12))], utc.Repository.EmailCandidateRequests);
    }

    [Fact]
    public async Task ProcessScheduledEmailsAsync_NothingDue_NeverLoadsTheConsultants()
    {
        var h = new Harness();
        h.AddStandardConsultants();
        h.Repository.EmailCandidates.AddRange(
        [
            Candidate(Mds1, daysSinceReady: 0, commentSubmitted: true),
            Candidate(Mds2, daysSinceReady: 2, emailState: 1),
            Candidate(Mds3, daysSinceReady: 9, emailState: 2),
        ]);

        var summary = await h.Service.ProcessScheduledEmailsAsync();

        Assert.Equal(TabulationEmailRunSummary.Empty, summary);
        Assert.Equal(0, h.Repository.ConsultantLoads);
        Assert.Empty(h.Notify.Sent);
        var log = Assert.Single(h.Logger.At(LogLevel.Information));
        Assert.Contains("Candidates=3 Due=0", log.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProcessScheduledEmailsAsync_SeveralDue_LoadsTheConsultantsOnce()
    {
        var h = new Harness();
        h.AddStandardConsultants();
        h.Repository.EmailCandidates.AddRange([Candidate(Mds1, daysSinceReady: 0), Candidate(Mds2, daysSinceReady: 1), Candidate(Mds3, daysSinceReady: 8, emailState: 1)]);

        await h.Service.ProcessScheduledEmailsAsync();

        Assert.Equal(1, h.Repository.ConsultantLoads);
    }

    [Fact]
    public async Task ProcessScheduledEmailsAsync_MixedRun_CountsEachOutcomeAndLogsTheSummary()
    {
        var h = new Harness();
        h.AddStandardConsultants();
        var noRecipient = Candidate(Mds2, daysSinceReady: 1);
        noRecipient.TestConsultant1Id = Guid.NewGuid();
        h.Repository.EmailCandidates.AddRange(
        [
            Candidate(Mds1, daysSinceReady: 0),
            noRecipient,
            Candidate(Mds3, daysSinceReady: 8, emailState: 1, tc2: Tc2),
            Candidate(Mds4, daysSinceReady: 3, commentSubmitted: true),
        ]);
        h.Notify.FailFor = email => email == Tc2Email ? new InvalidOperationException("notify down") : null;

        var summary = await h.Service.ProcessScheduledEmailsAsync();

        Assert.Equal(new TabulationEmailRunSummary(3, 1, 1, 1, 1), summary);
        var log = Assert.Single(h.Logger.At(LogLevel.Information), e => e.Message.StartsWith("Tabulation email run complete", StringComparison.Ordinal));
        Assert.Equal("Tabulation email run complete. Candidates=4 Due=3 FirstEmailsSent=1 ChaserEmailsSent=1 Skipped=1 Failed=1", log.Message);
        h.AssertNoAddressesLogged();
    }

    [Fact]
    public async Task ProcessScheduledEmailsAsync_OneSchemeThrows_IsLoggedAndTheNextSchemeIsStillProcessed()
    {
        var h = new Harness();
        h.AddStandardConsultants();
        h.Repository.EmailCandidates.AddRange([Candidate(Mds1, daysSinceReady: 0), Candidate(Mds2, daysSinceReady: 1)]);
        var failure = new InvalidOperationException("database down");
        h.Repository.EmailStateError = failure;
        h.Repository.EmailStateErrorOnlyFor = Mds1;

        var summary = await h.Service.ProcessScheduledEmailsAsync();

        Assert.Equal(new TabulationEmailRunSummary(2, 1, 0, 0, 1), summary);
        Assert.Equal([$"{Mds1}:first", $"{Mds2}:first"], h.Notify.Sent.Select(s => s.Reference));
        Assert.Equal([(Mds2, 1)], h.Repository.EmailStates);
        var error = Assert.Single(h.Logger.At(LogLevel.Error));
        Assert.Same(failure, error.Exception);
        Assert.Contains(error.State, kv => kv.Key == "MonthlyDistributionSchemeId" && Equals(kv.Value, Mds1));
        h.AssertNoAddressesLogged();
        h.AssertLockReleased();
    }

    [Fact]
    public async Task ProcessScheduledEmailsAsync_FirstEmailSentButStateWriteFailed_RetryWritesTheStateWithoutSendingAgain()
    {
        var h = new Harness();
        h.AddStandardConsultants();
        h.Repository.EmailCandidates.Add(Candidate(Mds1, daysSinceReady: 0));
        h.Repository.EmailStateError = new InvalidOperationException("database down");

        var failed = await h.Service.ProcessScheduledEmailsAsync();

        Assert.Equal(new TabulationEmailRunSummary(1, 0, 0, 0, 1), failed);
        Assert.Single(h.Notify.Sent);
        Assert.Empty(h.Repository.EmailStates);

        h.Repository.EmailStateError = null;
        var retried = await h.Service.ProcessScheduledEmailsAsync();

        Assert.Equal(new TabulationEmailRunSummary(1, 0, 0, 0, 0), retried);
        Assert.Single(h.Notify.Sent);
        Assert.Equal([(Mds1, 1)], h.Repository.EmailStates);
        Assert.False(h.Guard.FirstSent(Mds1));
    }

    [Fact]
    public async Task ProcessScheduledEmailsAsync_CandidateQueryFails_PropagatesAndReleasesTheLock()
    {
        var h = new Harness();
        h.Repository.EmailCandidatesError = new InvalidOperationException("database down");

        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Service.ProcessScheduledEmailsAsync());

        h.AssertLockReleased();
    }

    [Fact]
    public async Task ProcessScheduledEmailsAsync_CancelledWhileSending_Propagates()
    {
        var h = new Harness();
        h.AddStandardConsultants();
        h.Repository.EmailCandidates.Add(Candidate(Mds1));
        h.Notify.FailFor = _ => new OperationCanceledException();

        await Assert.ThrowsAsync<OperationCanceledException>(() => h.Service.ProcessScheduledEmailsAsync());

        h.AssertLockReleased();
    }

    [Fact]
    public async Task ProcessScheduledEmailsAsync_AlreadyCancelled_Propagates()
    {
        var h = new Harness();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => h.Service.ProcessScheduledEmailsAsync(cts.Token));
    }
}
