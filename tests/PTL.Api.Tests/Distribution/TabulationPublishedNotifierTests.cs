using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PTL.Core.Configuration;
using PTL.Core.Distribution;
using static PTL.Api.Tests.Distribution.TabulationEmailTestData;

namespace PTL.Api.Tests.Distribution;

// Legacy email.vb ProcessNotificationEmails: each unsent tblNotificationEmail row of the tabulation just published is sent
// through GOV.UK Notify and marked sent only afterwards, so a failed send stays unsent.
public class TabulationPublishedNotifierTests
{
    private const string PublishedTemplateId = "396b4c10-e36c-462e-9629-ea9d1950ef3e";

    private static readonly Guid Notification1 = Guid.Parse("c3000000-0000-0000-0000-000000000001");
    private static readonly Guid Notification2 = Guid.Parse("c3000000-0000-0000-0000-000000000002");
    private static readonly Guid Notification3 = Guid.Parse("c3000000-0000-0000-0000-000000000003");
    private static readonly Guid Published1 = Guid.Parse("d4000000-0000-0000-0000-000000000001");
    private static readonly Guid Published2 = Guid.Parse("d4000000-0000-0000-0000-000000000002");

    private sealed class Harness
    {
        public FakeDistributionRepository Repository { get; } = new();
        public RecordingNotifyClient Notify { get; } = new();
        public MessageCapturingLogger<TabulationPublishedNotifier> Logger { get; } = new();
        public SettableTimeProvider Clock { get; }
        public TabulationPublishedNotifier Notifier { get; }

        public Harness(bool enabled = true, string externalUrl = "https://external.example.test", DateTimeOffset? now = null)
        {
            Clock = new SettableTimeProvider(now ?? NowUtc);
            Notifier = new TabulationPublishedNotifier(
                Repository,
                Notify,
                Options.Create(new PublicationNotificationOptions { Enabled = enabled }),
                new TabulationEmailContext(
                    Clock,
                    Options.Create(new TabulationEmailOptions()),
                    Options.Create(new InternalOptions()),
                    Options.Create(new ExternalOptions { AppUrl = externalUrl })),
                Logger);
        }

        public void AssertNoAddressesLogged() => Logger.AssertNoneContain("lab1@example.test", "lab2@example.test", "lab3@example.test", "@");
    }

    private static NotificationEmailEntity Row(Guid id, Guid publishedId, string email, string name = "Lab One") =>
        new()
        {
            NotificationEmailId = id,
            PublishedTabulationId = publishedId,
            Email = email,
            Name = name,
            Distribution = "17152A/BA",
            Scheme = "PT0060: Mastitis bovine",
        };

    [Fact]
    public async Task SendForPublishedTabulationAsync_Disabled_ReturnsEmptyWithoutReadingTheRepository()
    {
        var h = new Harness(enabled: false);
        h.Repository.UnsentNotifications.Add(Row(Notification1, Published1, "lab1@example.test"));

        var summary = await h.Notifier.SendForPublishedTabulationAsync(Published1);

        Assert.Same(PublicationNotificationRunSummary.Empty, summary);
        Assert.Equal(0, h.Repository.UnsentLoads);
        Assert.Empty(h.Notify.Sent);
        Assert.Empty(h.Logger.Entries);
    }

    [Fact]
    public async Task SendForPublishedTabulationAsync_NothingUnsent_ReturnsEmptyCounts()
    {
        var h = new Harness();

        var summary = await h.Notifier.SendForPublishedTabulationAsync(Published1);

        Assert.Equal(PublicationNotificationRunSummary.Empty, summary);
        Assert.Equal(1, h.Repository.UnsentLoads);
        Assert.Empty(h.Notify.Sent);
    }

    [Fact]
    public async Task SendForPublishedTabulationAsync_UnsentRows_SendsTheTemplateWithTheLegacyPlaceholdersThenMarksEachSent()
    {
        var h = new Harness(externalUrl: "https://external.example.test/ptl/external/");
        h.Repository.UnsentNotifications.AddRange(
        [
            Row(Notification1, Published1, "lab1@example.test", "Lab One"),
            Row(Notification2, Published1, "lab2@example.test", "Lab Two"),
        ]);
        var sentBeforeEachSend = new List<int>();
        h.Notify.FailFor = _ =>
        {
            sentBeforeEachSend.Add(h.Repository.NotificationsSent.Count);
            return null;
        };

        var summary = await h.Notifier.SendForPublishedTabulationAsync(Published1);

        Assert.Equal(new PublicationNotificationRunSummary(2, 2, 0, 0), summary);
        Assert.Equal([0, 1], sentBeforeEachSend);
        Assert.Equal(["lab1@example.test", "lab2@example.test"], h.Notify.Sent.Select(s => s.Email));
        Assert.All(h.Notify.Sent, s => Assert.Equal(PublishedTemplateId, s.TemplateId));
        Assert.Equal([Notification1.ToString(), Notification2.ToString()], h.Notify.Sent.Select(s => s.Reference));
        AssertPersonalisation(
            h.Notify.Sent[0].Personalisation,
            ("DisplayName", "Lab One"),
            ("SchemeName", "PT0060: Mastitis bovine"),
            ("Distribution", "17152A/BA"),
            ("ExternalServer", "https://external.example.test"),
            ("ExternalAddress", "ptl/external"),
            ("PublishedTabulationId", Published1.ToString()),
            ("DateTimeNow", "12/05/2026"));
        Assert.Equal("Lab Two", h.Notify.Sent[1].Personalisation!["DisplayName"]);
        Assert.Equal([Notification1, Notification2], h.Repository.NotificationsSent.Select(s => s.Id));
        Assert.All(h.Repository.NotificationsSent, s => Assert.Equal(NowUtc.UtcDateTime, s.DateSent));
        var summaryLog = Assert.Single(h.Logger.At(LogLevel.Information), e => e.Message.StartsWith("Publication notification run complete", StringComparison.Ordinal));
        Assert.Equal($"Publication notification run complete. PublishedTabulationId={Published1} Unsent=2 Sent=2 Skipped=0 Failed=0", summaryLog.Message);
        h.AssertNoAddressesLogged();
    }

    [Fact]
    public async Task SendForPublishedTabulationAsync_RowsOfOtherTabulations_AreLeftUnsent()
    {
        var h = new Harness();
        h.Repository.UnsentNotifications.AddRange(
        [
            Row(Notification1, Published1, "lab1@example.test"),
            Row(Notification2, Published2, "lab2@example.test"),
            Row(Notification3, Published2, ""),
        ]);

        var summary = await h.Notifier.SendForPublishedTabulationAsync(Published1);

        Assert.Equal(new PublicationNotificationRunSummary(1, 1, 0, 0), summary);
        Assert.Equal(["lab1@example.test"], h.Notify.Sent.Select(s => s.Email));
        Assert.Equal([Notification1], h.Repository.NotificationsSent.Select(s => s.Id));
        Assert.Empty(h.Logger.At(LogLevel.Warning));
    }

    [Theory]
    [InlineData("https://external.example.test", "https://external.example.test", "")]
    [InlineData("https://external.example.test/", "https://external.example.test", "")]
    [InlineData("https://external.example.test:8443/a/b/", "https://external.example.test:8443", "a/b")]
    [InlineData("external.example.test/app/", "external.example.test/app", "")]
    [InlineData("", "", "")]
    public async Task SendForPublishedTabulationAsync_SplitsTheExternalAppUrlIntoServerAndAddress(string appUrl, string server, string address)
    {
        var h = new Harness(externalUrl: appUrl);
        h.Repository.UnsentNotifications.Add(Row(Notification1, Published1, "lab1@example.test"));

        await h.Notifier.SendForPublishedTabulationAsync(Published1);

        var personalisation = Assert.Single(h.Notify.Sent).Personalisation!;
        Assert.Equal((server, address), (personalisation["ExternalServer"], personalisation["ExternalAddress"]));
    }

    [Fact]
    public async Task SendForPublishedTabulationAsync_NotifyFails_LeavesTheRowUnsentAndContinuesWithTheNext()
    {
        var h = new Harness();
        h.Repository.UnsentNotifications.AddRange(
        [
            Row(Notification1, Published1, "lab1@example.test"),
            Row(Notification2, Published1, "lab2@example.test"),
        ]);
        var failure = new InvalidOperationException("notify down");
        h.Notify.FailFor = email => email == "lab1@example.test" ? failure : null;

        var summary = await h.Notifier.SendForPublishedTabulationAsync(Published1);

        Assert.Equal(new PublicationNotificationRunSummary(2, 1, 0, 1), summary);
        Assert.Equal(["lab2@example.test"], h.Notify.Sent.Select(s => s.Email));
        Assert.Equal([Notification2], h.Repository.NotificationsSent.Select(s => s.Id));
        var error = Assert.Single(h.Logger.At(LogLevel.Error));
        Assert.Same(failure, error.Exception);
        Assert.Contains("stays unsent", error.Message, StringComparison.Ordinal);
        h.AssertNoAddressesLogged();
    }

    [Fact]
    public async Task SendForPublishedTabulationAsync_MarkingSentFails_CountsTheRowFailed()
    {
        var h = new Harness();
        h.Repository.UnsentNotifications.Add(Row(Notification1, Published1, "lab1@example.test"));
        var failure = new InvalidOperationException("database down");
        h.Repository.NotificationSentError = failure;

        var summary = await h.Notifier.SendForPublishedTabulationAsync(Published1);

        Assert.Equal(new PublicationNotificationRunSummary(1, 0, 0, 1), summary);
        Assert.Single(h.Notify.Sent);
        Assert.Empty(h.Repository.NotificationsSent);
        Assert.Same(failure, Assert.Single(h.Logger.At(LogLevel.Error)).Exception);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SendForPublishedTabulationAsync_BlankEmail_SkipsTheRowAndWarns(string email)
    {
        var h = new Harness();
        h.Repository.UnsentNotifications.AddRange(
        [
            Row(Notification1, Published1, email),
            Row(Notification2, Published1, "lab2@example.test"),
        ]);

        var summary = await h.Notifier.SendForPublishedTabulationAsync(Published1);

        Assert.Equal(new PublicationNotificationRunSummary(2, 1, 1, 0), summary);
        Assert.Equal(["lab2@example.test"], h.Notify.Sent.Select(s => s.Email));
        Assert.Equal([Notification2], h.Repository.NotificationsSent.Select(s => s.Id));
        var warning = Assert.Single(h.Logger.At(LogLevel.Warning));
        Assert.Contains("no email address", warning.Message, StringComparison.Ordinal);
        Assert.Contains(warning.State, kv => kv.Key == "NotificationEmailId" && Equals(kv.Value, Notification1));
        h.AssertNoAddressesLogged();
    }

    [Fact]
    public async Task SendForPublishedTabulationAsync_BlankEmailRows_AreWarnedIndependently()
    {
        var h = new Harness();
        h.Repository.UnsentNotifications.AddRange(
        [
            Row(Notification1, Published1, ""),
            Row(Notification3, Published1, " "),
        ]);

        var summary = await h.Notifier.SendForPublishedTabulationAsync(Published1);

        Assert.Equal(new PublicationNotificationRunSummary(2, 0, 2, 0), summary);
        Assert.Equal(2, h.Logger.At(LogLevel.Warning).Count());
        Assert.Empty(h.Notify.Sent);
    }

    [Fact]
    public async Task SendForPublishedTabulationAsync_AroundMidnight_FormatsDateTimeNowInTheLondonZone()
    {
        var h = new Harness(now: new DateTimeOffset(2026, 5, 11, 23, 30, 0, TimeSpan.Zero));
        h.Repository.UnsentNotifications.Add(Row(Notification1, Published1, "lab1@example.test"));

        await h.Notifier.SendForPublishedTabulationAsync(Published1);

        Assert.Equal("12/05/2026", Assert.Single(h.Notify.Sent).Personalisation!["DateTimeNow"]);
    }

    [Fact]
    public async Task SendForPublishedTabulationAsync_CancelledWhileSending_PropagatesWithoutMarkingTheRow()
    {
        var h = new Harness();
        h.Repository.UnsentNotifications.Add(Row(Notification1, Published1, "lab1@example.test"));
        h.Notify.FailFor = _ => new OperationCanceledException();

        await Assert.ThrowsAsync<OperationCanceledException>(() => h.Notifier.SendForPublishedTabulationAsync(Published1));

        Assert.Empty(h.Repository.NotificationsSent);
        Assert.Empty(h.Logger.At(LogLevel.Error));
    }
}
