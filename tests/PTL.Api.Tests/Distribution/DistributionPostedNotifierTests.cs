using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PTL.Core.Configuration;
using PTL.Core.Distribution;
using ParticipantEntity = PTL.Core.Participant.Participant;
using static PTL.Api.Tests.Distribution.PackagingTestData;

namespace PTL.Api.Tests.Distribution;

// Pins legacy DistributionPostedEmailCollection.AddNewDistributionPostedEmail (queue, never
// duplicating an unsent email) and the send step that GOV.UK Notify replaces.
public class DistributionPostedNotifierTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 13, 10, 30, 0, TimeSpan.Zero);

    private sealed class Harness
    {
        public FakeDistributionRepository Repository { get; } = new();
        public FakePostedParticipantService Participants { get; } = new();
        public RecordingNotifyClient Notify { get; } = new();
        public CapturingLogger<DistributionPostedNotifier> Logger { get; } = new();
        public DistributionPostedNotifier Notifier { get; }

        public Harness(string externalLink = "https://external.example.test")
        {
            var options = Options.Create(new ExternalOptions { AppUrl = externalLink });
            Notifier = new DistributionPostedNotifier(Repository, Participants, Notify, options, new FixedTimeProvider(Now), Logger);
        }

        public void AddParticipant(Guid id, string email, string email2 = "") =>
            Participants.Participants[id] = new ParticipantEntity { ParticipantId = id, Email = email, Email2 = email2 };
    }

    private static PostedParticipant Posted(Guid participantId, Guid? schemeId = null) =>
        new(participantId, schemeId ?? Scheme1, "17152A/BA", "PT0060: Mastitis bovine");

    private static DistributionPostedEmailEntity Email(Guid id) =>
        new() { DistributionPostedEmailId = id, ParticipantId = Part1, MonthlyDistributionSchemeId = Scheme1, Email = "lab@example.test", Distribution = "17152A/BA", SchemeName = "PT0060: Mastitis bovine" };

    // ---------- BuildEmailsAsync ----------

    [Fact]
    public async Task BuildEmailsAsync_NothingPosted_ReturnsEmptyWithoutAnyLookup()
    {
        var h = new Harness();

        var emails = await h.Notifier.BuildEmailsAsync([]);

        Assert.Empty(emails);
        Assert.Equal(0, h.Repository.UnsentPostedEmailRequests);
        Assert.Empty(h.Participants.Requested);
    }

    [Fact]
    public async Task BuildEmailsAsync_UnknownParticipantIsSkipped()
    {
        var h = new Harness();

        var emails = await h.Notifier.BuildEmailsAsync([Posted(Part1)]);

        Assert.Empty(emails);
        Assert.Equal([Part1], h.Participants.Requested);
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
    }

    [Fact]
    public async Task BuildEmailsAsync_SecondaryAddressOnlyWhenPresent()
    {
        var h = new Harness();
        h.AddParticipant(Part1, "primary@example.test", "  ");

        var emails = await h.Notifier.BuildEmailsAsync([Posted(Part1)]);

        Assert.Equal("primary@example.test", Assert.Single(emails).Email);
    }

    [Fact]
    public async Task BuildEmailsAsync_BlankPrimaryIsSkipped()
    {
        var h = new Harness();
        h.AddParticipant(Part1, "", "secondary@example.test");
        h.AddParticipant(Part2, " ", "");

        var emails = await h.Notifier.BuildEmailsAsync([Posted(Part1), Posted(Part2)]);

        Assert.Equal("secondary@example.test", Assert.Single(emails).Email);
    }

    [Fact]
    public async Task BuildEmailsAsync_AddressAlreadyWaitingInTheQueueIsNotQueuedAgainIgnoringCase()
    {
        var h = new Harness();
        h.AddParticipant(Part1, "Lab@Example.test", "other@example.test");
        h.Repository.UnsentPostedEmails.Add(Email(Guid.NewGuid()));

        var emails = await h.Notifier.BuildEmailsAsync([Posted(Part1)]);

        Assert.Equal("other@example.test", Assert.Single(emails).Email);
    }

    [Fact]
    public async Task BuildEmailsAsync_SameAddressForADifferentSchemeIsStillQueued()
    {
        var h = new Harness();
        h.AddParticipant(Part1, "lab@example.test");
        h.Repository.UnsentPostedEmails.Add(Email(Guid.NewGuid()));

        var emails = await h.Notifier.BuildEmailsAsync([Posted(Part1, Scheme2)]);

        var email = Assert.Single(emails);
        Assert.Equal(Scheme2, email.MonthlyDistributionSchemeId);
    }

    [Fact]
    public async Task BuildEmailsAsync_NoDuplicatesWithinTheSameBatch()
    {
        var h = new Harness();
        h.AddParticipant(Part1, "lab@example.test", "LAB@example.test");

        var emails = await h.Notifier.BuildEmailsAsync([Posted(Part1), Posted(Part1)]);

        Assert.Equal("lab@example.test", Assert.Single(emails).Email);
    }

    // ---------- DispatchAsync ----------

    [Fact]
    public async Task DispatchAsync_NoEmails_DoesNothing()
    {
        var h = new Harness();

        await h.Notifier.DispatchAsync([]);

        Assert.Empty(h.Notify.Sent);
        Assert.Empty(h.Logger.Entries);
    }

    [Fact]
    public async Task DispatchAsync_SendsThroughNotifyWithPersonalisationAndMarksTheRowSent()
    {
        var h = new Harness();
        var id = Guid.NewGuid();

        await h.Notifier.DispatchAsync([Email(id)]);

        var sent = Assert.Single(h.Notify.Sent);
        Assert.Equal("2bb0e2d0-fa23-472e-947c-4524d14ad288", sent.TemplateId);
        Assert.Equal("lab@example.test", sent.Email);
        Assert.Equal(id.ToString(), sent.Reference);
        Assert.Equal("PT0060: Mastitis bovine", sent.Personalisation!["scheme_name"]);
        Assert.Equal("17152A/BA", sent.Personalisation["distribution"]);
        Assert.Equal("https://external.example.test", sent.Personalisation["external_link"]);
        Assert.Equal("13/05/2026", sent.Personalisation["date"]);
        Assert.Equal((id, Now.UtcDateTime), Assert.Single(h.Repository.MarkedSentEmails));
        Assert.Empty(h.Logger.Entries);
    }

    [Fact]
    public async Task DispatchAsync_FailedSendIsLoggedLeftQueuedAndTheNextEmailStillGoes()
    {
        var h = new Harness();
        var failing = Email(Guid.NewGuid());
        failing.Email = "broken@example.test";
        var working = Email(Guid.NewGuid());
        h.Notify.FailFor = address => address == "broken@example.test" ? new InvalidOperationException("Notify down") : null;

        await h.Notifier.DispatchAsync([failing, working]);

        Assert.Equal("lab@example.test", Assert.Single(h.Notify.Sent).Email);
        Assert.Equal(working.DistributionPostedEmailId, Assert.Single(h.Repository.MarkedSentEmails).Id);
        var entry = Assert.Single(h.Logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.IsType<InvalidOperationException>(entry.Exception);
    }

    [Fact]
    public async Task DispatchAsync_CancellationPropagates()
    {
        var h = new Harness();
        h.Notify.FailFor = _ => new OperationCanceledException();

        await Assert.ThrowsAsync<OperationCanceledException>(() => h.Notifier.DispatchAsync([Email(Guid.NewGuid())]));

        Assert.Empty(h.Repository.MarkedSentEmails);
    }
}
