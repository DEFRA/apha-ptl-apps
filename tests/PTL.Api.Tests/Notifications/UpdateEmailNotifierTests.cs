using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PTL.Api.Tests.Customer;
using PTL.Api.Tests.Distribution;
using PTL.Api.Tests.Participant;
using PTL.Core.Customer;
using PTL.Core.Notifications;
using PTL.Core.Participant;

namespace PTL.Api.Tests.Notifications;

// Legacy email.vb ProcessUpdateCustomerDetailsEmails / ProcessUpdateParticipantDetailsEmails: each unsent row is sent
// through GOV.UK Notify and marked sent only afterwards, so a failed send stays unsent for the next run.
public class UpdateEmailNotifierTests
{
    private const string CustomerAccepted = CustomerUpdateEmailTemplates.AcceptedId;
    private const string CustomerDeclined = CustomerUpdateEmailTemplates.DeclinedId;
    private const string ParticipantAccepted = ParticipantUpdateEmailTemplates.AcceptedId;
    private const string ParticipantDeclined = ParticipantUpdateEmailTemplates.DeclinedId;

    private static readonly DateTimeOffset Now = new(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);

    private sealed class CustomerHarness
    {
        public FakePendingCustomerUpdateRepository Repository { get; } = new();
        public RecordingNotifyClient Notify { get; } = new();
        public EmailSendGuard Guard { get; } = new();
        public MessageCapturingLogger<CustomerUpdateNotifier> Logger { get; } = new();
        public CustomerUpdateNotifier Notifier { get; }
        public Guid PendingId { get; } = Guid.NewGuid();

        public CustomerHarness(bool enabled = true, PTL.Core.Notifications.INotifyClient? client = null) =>
            Notifier = new CustomerUpdateNotifier(
                Repository,
                client ?? Notify,
                Guard,
                Options.Create(new CustomerUpdateEmailOptions { Enabled = enabled }),
                new SettableTimeProvider(Now),
                Logger);

        public CustomerUpdateEmailEntity Queue(string? email, bool isApproved = true, string notes = "Thanks", Guid? pendingId = null)
        {
            var row = new CustomerUpdateEmailEntity
            {
                Id = Guid.NewGuid(),
                Email = email,
                PendingCustomerUpdateId = pendingId ?? PendingId,
                CustomerName = "Sample Laboratories Ltd",
                Notes = notes,
                IsApproved = isApproved,
                QalNumber = "QAL/00007",
                LabCode = "042"
            };
            Repository.Unsent.Add(row);
            return row;
        }
    }

    private sealed class ParticipantHarness
    {
        public FakePendingParticipantUpdateRepository Repository { get; } = new();
        public RecordingNotifyClient Notify { get; } = new();
        public EmailSendGuard Guard { get; } = new();
        public MessageCapturingLogger<ParticipantUpdateNotifier> Logger { get; } = new();
        public ParticipantUpdateNotifier Notifier { get; }
        public Guid PendingId { get; } = Guid.NewGuid();

        public ParticipantHarness(bool enabled = true) =>
            Notifier = new ParticipantUpdateNotifier(
                Repository,
                Notify,
                Guard,
                Options.Create(new ParticipantUpdateEmailOptions { Enabled = enabled }),
                new SettableTimeProvider(Now),
                Logger);

        public ParticipantUpdateEmailEntity Queue(string? email, bool isApproved = true, string notes = "Thanks", Guid? pendingId = null)
        {
            var row = new ParticipantUpdateEmailEntity
            {
                Id = Guid.NewGuid(),
                Email = email,
                LabCode = "042",
                PendingParticipantUpdateId = pendingId ?? PendingId,
                ParticipantName = "042: Alpha Lab",
                Notes = notes,
                IsApproved = isApproved,
                QalNumber = "QAL/00007"
            };
            Repository.Unsent.Add(row);
            return row;
        }
    }

    // ---- Customer ----

    [Fact]
    public async Task Customer_Accepted_SendsTheAcceptedTemplateWithTheLegacyTokensAndMarksItSent()
    {
        var h = new CustomerHarness();
        var row = h.Queue("customer@example.com");

        await h.Notifier.SendUnsentAsync();

        var sent = Assert.Single(h.Notify.Sent);
        Assert.Equal(CustomerAccepted, sent.TemplateId);
        Assert.Equal("customer@example.com", sent.Email);
        Assert.Equal(row.Id.ToString(), sent.Reference);
        Assert.Equal("042", sent.Personalisation!["ParticipantName"]);
        Assert.Equal("QAL/00007", sent.Personalisation["CustomerName"]);
        Assert.Equal("Accepted", sent.Personalisation["status"]);
        Assert.Equal("Thanks", sent.Personalisation["Notes"]);
        Assert.Equal(Now.UtcDateTime, h.Repository.MarkedSent[row.Id]);
        Assert.Empty(h.Repository.Unsent);
    }

    [Fact]
    public async Task Customer_Declined_SendsTheDeclinedTemplateWithTheDeclinedStatus()
    {
        var h = new CustomerHarness();
        h.Queue("customer@example.com", isApproved: false, notes: "Not eligible");

        await h.Notifier.SendUnsentAsync();

        var sent = Assert.Single(h.Notify.Sent);
        Assert.Equal(CustomerDeclined, sent.TemplateId);
        Assert.Equal("Declined", sent.Personalisation!["status"]);
        Assert.Equal("Not eligible", sent.Personalisation["Notes"]);
    }

    [Fact]
    public async Task Customer_MissingLookups_AreSentAsBlankValues()
    {
        var h = new CustomerHarness();
        var row = h.Queue("customer@example.com");
        row.QalNumber = null;
        row.LabCode = null;

        await h.Notifier.SendUnsentAsync();

        var sent = Assert.Single(h.Notify.Sent);
        Assert.Equal(string.Empty, sent.Personalisation!["CustomerName"]);
        Assert.Equal(string.Empty, sent.Personalisation["ParticipantName"]);
    }

    [Fact]
    public async Task Customer_Disabled_SendsNothingAndLeavesTheRowsUnsent()
    {
        var h = new CustomerHarness(enabled: false);
        h.Queue("customer@example.com");

        await h.Notifier.SendUnsentAsync();

        Assert.Equal(0, h.Repository.GetUnsentCalls);
        Assert.Empty(h.Notify.Sent);
        Assert.Single(h.Repository.Unsent);
    }

    [Fact]
    public async Task Customer_NoRows_DoesNothing()
    {
        var h = new CustomerHarness();

        await h.Notifier.SendUnsentAsync();

        Assert.Empty(h.Notify.Sent);
        Assert.Empty(h.Logger.Entries);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task Customer_BlankAddress_StaysUnsentAndWarnsOnlyOncePerDay(string? address)
    {
        var h = new CustomerHarness();
        h.Queue(address);

        await h.Notifier.SendUnsentAsync();
        await h.Notifier.SendUnsentAsync();

        Assert.Empty(h.Notify.Sent);
        Assert.Single(h.Repository.Unsent);
        Assert.Single(h.Logger.At(LogLevel.Warning));
    }

    [Fact]
    public async Task Customer_NotifyFailure_StaysUnsentAndTheNextRunSendsItExactlyOnce()
    {
        var h = new CustomerHarness();
        h.Queue("bad@example.com");
        h.Queue("good@example.com");
        h.Notify.FailFor = address => address == "bad@example.com" ? new InvalidOperationException("Notify is down") : null;

        await h.Notifier.SendUnsentAsync();

        Assert.Equal("good@example.com", Assert.Single(h.Notify.Sent).Email);
        Assert.Equal("bad@example.com", Assert.Single(h.Repository.Unsent).Email);

        h.Notify.FailFor = null;
        await h.Notifier.SendUnsentAsync();

        Assert.Equal(["good@example.com", "bad@example.com"], h.Notify.Sent.Select(s => s.Email));
        Assert.Empty(h.Repository.Unsent);
    }

    [Fact]
    public async Task Customer_RepeatedFailure_LogsOncePerDayWithoutTheAddressOrNotes()
    {
        var h = new CustomerHarness();
        h.Queue("bad@example.com", notes: "private comment");
        h.Notify.FailFor = _ => new InvalidOperationException("Notify is down");

        await h.Notifier.SendUnsentAsync();
        await h.Notifier.SendUnsentAsync();

        Assert.Single(h.Logger.At(LogLevel.Error));
        h.Logger.AssertNoneContain("bad@example.com", "private comment", "@");
    }

    [Fact]
    public async Task Customer_SendForUpdate_SendsOnlyThatUpdatesRows()
    {
        var h = new CustomerHarness();
        var other = Guid.NewGuid();
        h.Queue("customer@example.com");
        h.Queue("someone-else@example.com", pendingId: other);

        await h.Notifier.SendForUpdateAsync(h.PendingId);

        Assert.Equal("customer@example.com", Assert.Single(h.Notify.Sent).Email);
        Assert.Equal(other, Assert.Single(h.Repository.Unsent).PendingCustomerUpdateId);
    }

    [Fact]
    public async Task Customer_SendForUpdate_AnythingFails_NeverThrowsAndLeavesTheRowUnsent()
    {
        var h = new CustomerHarness();
        h.Queue("customer@example.com");
        h.Repository.GetUnsentFailure = new InvalidOperationException("database is down");

        await h.Notifier.SendForUpdateAsync(h.PendingId);

        Assert.Empty(h.Notify.Sent);
        Assert.Single(h.Repository.Unsent);
        Assert.Single(h.Logger.At(LogLevel.Error));
    }

    [Fact]
    public async Task Customer_SendForUpdate_Cancelled_Propagates()
    {
        var h = new CustomerHarness();
        h.Queue("customer@example.com");
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => h.Notifier.SendForUpdateAsync(h.PendingId, cts.Token));
    }

    [Fact]
    public async Task Customer_ImmediateSendAndScheduler_RunningTogether_SendEachRowOnce()
    {
        var slow = new SlowNotifyClient();
        var h = new CustomerHarness(client: slow);
        h.Queue("a@example.com");
        h.Queue("b@example.com");

        await Task.WhenAll(h.Notifier.SendForUpdateAsync(h.PendingId), h.Notifier.SendUnsentAsync());

        Assert.Equal(2, slow.Sent.Count);
        Assert.Equal(2, slow.Sent.Select(s => s.Email).Distinct().Count());
        Assert.Empty(h.Repository.Unsent);
    }

    // ---- Participant ----

    [Fact]
    public async Task Participant_Accepted_SendsTheAcceptedTemplateWithTheLegacyTokensAndMarksItSent()
    {
        var h = new ParticipantHarness();
        var row = h.Queue("lab@example.com");

        await h.Notifier.SendUnsentAsync();

        var sent = Assert.Single(h.Notify.Sent);
        Assert.Equal(ParticipantAccepted, sent.TemplateId);
        Assert.Equal("lab@example.com", sent.Email);
        Assert.Equal(row.Id.ToString(), sent.Reference);
        Assert.Equal("042", sent.Personalisation!["ParticipantName"]);
        Assert.Equal("QAL/00007", sent.Personalisation["CustomerName"]);
        Assert.Equal("Accepted", sent.Personalisation["status"]);
        Assert.Equal("Thanks", sent.Personalisation["Notes"]);
        Assert.Equal(Now.UtcDateTime, h.Repository.MarkedSent[row.Id]);
        Assert.Empty(h.Repository.Unsent);
    }

    [Fact]
    public async Task Participant_Declined_SendsTheDeclinedTemplateWithTheDeclinedStatus()
    {
        var h = new ParticipantHarness();
        h.Queue("lab@example.com", isApproved: false, notes: "Not eligible");

        await h.Notifier.SendUnsentAsync();

        var sent = Assert.Single(h.Notify.Sent);
        Assert.Equal(ParticipantDeclined, sent.TemplateId);
        Assert.Equal("Declined", sent.Personalisation!["status"]);
        Assert.Equal("Not eligible", sent.Personalisation["Notes"]);
    }

    [Fact]
    public async Task Participant_MissingQalNumber_IsSentAsABlankValue()
    {
        var h = new ParticipantHarness();
        h.Queue("lab@example.com").QalNumber = null;

        await h.Notifier.SendUnsentAsync();

        Assert.Equal(string.Empty, Assert.Single(h.Notify.Sent).Personalisation!["CustomerName"]);
    }

    [Fact]
    public async Task Participant_Disabled_SendsNothingAndLeavesTheRowsUnsent()
    {
        var h = new ParticipantHarness(enabled: false);
        h.Queue("lab@example.com");

        await h.Notifier.SendUnsentAsync();

        Assert.Equal(0, h.Repository.GetUnsentCalls);
        Assert.Empty(h.Notify.Sent);
        Assert.Single(h.Repository.Unsent);
    }

    [Fact]
    public async Task Participant_NoRows_DoesNothing()
    {
        var h = new ParticipantHarness();

        await h.Notifier.SendUnsentAsync();

        Assert.Empty(h.Notify.Sent);
        Assert.Empty(h.Logger.Entries);
    }

    [Fact]
    public async Task Participant_BlankAddress_StaysUnsentAndWarnsOnlyOncePerDay()
    {
        var h = new ParticipantHarness();
        h.Queue(" ");
        h.Queue("", isApproved: false);

        await h.Notifier.SendUnsentAsync();
        await h.Notifier.SendUnsentAsync();

        Assert.Empty(h.Notify.Sent);
        Assert.Equal(2, h.Repository.Unsent.Count);
        Assert.Equal(2, h.Logger.At(LogLevel.Warning).Count());
    }

    [Fact]
    public async Task Participant_NotifyFailure_StaysUnsentAndTheNextRunSendsItExactlyOnce()
    {
        var h = new ParticipantHarness();
        h.Queue("bad@example.com");
        h.Queue("good@example.com");
        h.Notify.FailFor = address => address == "bad@example.com" ? new InvalidOperationException("Notify is down") : null;

        await h.Notifier.SendUnsentAsync();
        await h.Notifier.SendUnsentAsync();

        Assert.Equal("good@example.com", Assert.Single(h.Notify.Sent).Email);
        Assert.Equal("bad@example.com", Assert.Single(h.Repository.Unsent).Email);
        Assert.Single(h.Logger.At(LogLevel.Error));
        h.Logger.AssertNoneContain("bad@example.com", "@");

        h.Notify.FailFor = null;
        await h.Notifier.SendUnsentAsync();

        Assert.Empty(h.Repository.Unsent);
        Assert.Equal(2, h.Notify.Sent.Count);
    }

    [Fact]
    public async Task Participant_SendForUpdate_SendsOnlyThatUpdatesRowsAndNeverThrows()
    {
        var h = new ParticipantHarness();
        var other = Guid.NewGuid();
        h.Queue("lab@example.com");
        h.Queue("someone-else@example.com", pendingId: other);

        await h.Notifier.SendForUpdateAsync(h.PendingId);

        Assert.Equal("lab@example.com", Assert.Single(h.Notify.Sent).Email);
        Assert.Equal(other, Assert.Single(h.Repository.Unsent).PendingParticipantUpdateId);

        h.Repository.GetUnsentFailure = new InvalidOperationException("database is down");
        await h.Notifier.SendForUpdateAsync(other);

        Assert.Single(h.Repository.Unsent);
        Assert.Single(h.Logger.At(LogLevel.Error));
    }

    [Fact]
    public async Task Participant_SendForUpdate_Cancelled_Propagates()
    {
        var h = new ParticipantHarness();
        h.Queue("lab@example.com");
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => h.Notifier.SendForUpdateAsync(h.PendingId, cts.Token));
    }
}
