using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PTL.Api.Tests.Customer;
using PTL.Api.Tests.Distribution;
using PTL.Api.Tests.Lookup;
using PTL.Api.Tests.Scheme;
using PTL.Core.Contract;
using PTL.Core.Contract.PendingOrder;
using PTL.Core.Lookup;
using PTL.Core.Notifications;
using CoreScheme = PTL.Core.Scheme.Scheme;

namespace PTL.Api.Tests.Contract;

// Legacy email.vb ProcessOnlineContractOrderEmails: each unsent tblOnlineContractOrderEmail row is sent through
// GOV.UK Notify and marked sent only afterwards, so a failed send stays unsent for the next run.
public class PendingOrderNotifierTests
{
    private const string ApprovedTemplate = "approved-template-id";
    private const string DeclinedTemplate = "declined-template-id";
    private const int YearId = 2026;

    private static readonly DateTimeOffset Now = new(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);

    private sealed class Harness
    {
        public FakePendingOrderRepository PendingOrders { get; } = new();
        public FakeContractRepository Contracts { get; } = new();
        public RecordingNotifyClient Notify { get; } = new();
        public PendingOrderEmailGuard Guard { get; } = new();
        public MessageCapturingLogger<PendingOrderNotifier> Logger { get; } = new();
        public PendingOrderNotifier Notifier { get; private set; } = null!;
        public Guid PendingContractId { get; } = Guid.NewGuid();
        public Guid ContractId { get; } = Guid.NewGuid();
        public string QalNumber { get; private set; } = string.Empty;

        public static async Task<Harness> CreateAsync(bool enabled = true, string approvedTemplate = ApprovedTemplate, string declinedTemplate = DeclinedTemplate, INotifyClient? client = null)
        {
            var h = new Harness();

            var customers = new FakeCustomerRepository();
            var customer = await customers.CreateAsync(new PTL.Core.Customer.Customer { CustomerId = Guid.NewGuid(), Name = "Sample Laboratories Ltd", Email = "customer@example.com", IsActive = true });
            h.QalNumber = customer.QalNumber;

            var participants = new PTL.Api.Tests.Participant.FakeParticipantRepository();
            await participants.CreateAsync(new PTL.Core.Participant.Participant { CustomerId = customer.CustomerId, LabCode = "001", Email = "participant@example.com", IsActive = true });
            await participants.CreateAsync(new PTL.Core.Participant.Participant { CustomerId = customer.CustomerId, LabCode = "002", Email = "other@example.com", IsActive = true });

            var schemes = new FakeSchemeRepository();
            var scheme = await schemes.CreateAsync(new CoreScheme
            {
                SchemeId = Guid.NewGuid(),
                YearId = YearId,
                Identifier = "S1",
                Name = "Sample Scheme",
                Postage = Guid.NewGuid(),
                DayOfWeekId = Guid.NewGuid(),
                WeekNumber = 2
            });

            var first = new bool[12];
            first[3] = first[4] = true;
            var second = new bool[12];
            second[4] = second[0] = true;
            h.Contracts.ContractItems = new ContractItemsAggregate
            {
                ContractId = h.ContractId,
                Schemes =
                [
                    new ContractItemSchemeGroup
                    {
                        SchemeId = scheme.SchemeId,
                        SchemeIdentifier = "S1",
                        SchemeName = "Sample Scheme",
                        Participants = [new ContractItemLine { Months = first }, new ContractItemLine { Months = second }]
                    }
                ]
            };

            h.PendingOrders.Seed(
                new PendingOrderEntity { PendingContractId = h.PendingContractId, CustomerId = customer.CustomerId, YearId = YearId, IsSubmitted = true, IsDeleted = true, CustomerName = customer.Name },
                new PendingOrderSummaryEntity { PendingContractId = h.PendingContractId, CustomerId = customer.CustomerId, YearId = YearId, IsSubmitted = true, IsDeleted = true });

            var lookups = new FakeLookupRepository
            {
                AllYears = [new YearEntity { YearId = YearId, Year = "2026/27" }]
            };

            h.Notifier = new PendingOrderNotifier(
                h.PendingOrders,
                h.Contracts,
                schemes,
                customers,
                participants,
                lookups,
                client ?? h.Notify,
                h.Guard,
                Options.Create(new OnlineOrderEmailOptions { Enabled = enabled, ApprovedTemplateId = approvedTemplate, DeclinedTemplateId = declinedTemplate }),
                new SettableTimeProvider(Now),
                h.Logger);

            return h;
        }

        public OnlineOrderEmailEntity Queue(string? email, bool isApproved = true, string notes = "Thanks for your order", Guid? pendingContractId = null)
        {
            var row = new OnlineOrderEmailEntity
            {
                Id = Guid.NewGuid(),
                Email = email,
                ContractId = isApproved ? ContractId : Guid.Empty,
                PendingContractId = pendingContractId ?? PendingContractId,
                CustomerName = "Sample Laboratories Ltd",
                YearId = YearId,
                Notes = notes,
                IsApproved = isApproved
            };
            PendingOrders.Unsent.Add(row);
            return row;
        }
    }

    [Fact]
    public async Task SendUnsentAsync_Approved_SendsTheApprovedTemplateWithTheLegacyTokensAndMarksItSent()
    {
        var h = await Harness.CreateAsync();
        var row = h.Queue("customer@example.com");

        var summary = await h.Notifier.SendUnsentAsync();

        Assert.Equal(new OnlineOrderEmailRunSummary(1, 1, 0, 0), summary);
        var sent = Assert.Single(h.Notify.Sent);
        Assert.Equal(ApprovedTemplate, sent.TemplateId);
        Assert.Equal("customer@example.com", sent.Email);
        Assert.Equal(row.Id.ToString(), sent.Reference);
        Assert.Equal("001", sent.Personalisation!["ParticipantName"]);
        Assert.Equal(h.QalNumber, sent.Personalisation["CustomerName"]);
        Assert.Equal("Approved", sent.Personalisation["status"]);
        Assert.Equal("2026/27", sent.Personalisation["ContractYear"]);
        Assert.Equal("Thanks for your order", sent.Personalisation["Notes"]);
        Assert.Equal("* S1:Sample Scheme - Month: Apr May Jan - Week: 2", sent.Personalisation["ApprovedSchemeOrders"]);
        Assert.Equal(Now.UtcDateTime, h.PendingOrders.MarkedSent[row.Id]);
        Assert.Empty(h.PendingOrders.Unsent);
    }

    [Fact]
    public async Task SendUnsentAsync_Declined_SendsTheDeclinedTemplateWithTheFirstActiveLabCodeAndNoSchemeList()
    {
        var h = await Harness.CreateAsync();
        h.Queue("customer@example.com", isApproved: false, notes: "Not eligible");

        await h.Notifier.SendUnsentAsync();

        var sent = Assert.Single(h.Notify.Sent);
        Assert.Equal(DeclinedTemplate, sent.TemplateId);
        Assert.Equal("001", sent.Personalisation!["ParticipantName"]);
        Assert.Equal("Declined", sent.Personalisation["status"]);
        Assert.Equal(h.QalNumber, sent.Personalisation["CustomerName"]);
        Assert.Equal("Not eligible", sent.Personalisation["Notes"]);
        Assert.False(sent.Personalisation.ContainsKey("ApprovedSchemeOrders"));
    }

    [Fact]
    public async Task SendUnsentAsync_UnknownYear_UsesTheLegacyPlaceholder()
    {
        var h = await Harness.CreateAsync();
        h.Queue("customer@example.com").YearId = 1999;

        await h.Notifier.SendUnsentAsync();

        Assert.Equal("####", Assert.Single(h.Notify.Sent).Personalisation!["ContractYear"]);
    }

    [Fact]
    public async Task SendUnsentAsync_ContractWithNoItems_SendsAnEmptySchemeList()
    {
        var h = await Harness.CreateAsync();
        h.Contracts.ContractItems = null;
        h.Queue("customer@example.com");

        await h.Notifier.SendUnsentAsync();

        Assert.Equal(string.Empty, Assert.Single(h.Notify.Sent).Personalisation!["ApprovedSchemeOrders"]);
    }

    [Fact]
    public async Task SendUnsentAsync_Disabled_SendsNothingAndLeavesTheRowsUnsent()
    {
        var h = await Harness.CreateAsync(enabled: false);
        h.Queue("customer@example.com");

        var summary = await h.Notifier.SendUnsentAsync();

        Assert.Same(OnlineOrderEmailRunSummary.Empty, summary);
        Assert.Equal(0, h.PendingOrders.GetUnsentCalls);
        Assert.Empty(h.Notify.Sent);
        Assert.Single(h.PendingOrders.Unsent);
    }

    [Fact]
    public async Task SendUnsentAsync_NoRows_DoesNothing()
    {
        var h = await Harness.CreateAsync();

        Assert.Same(OnlineOrderEmailRunSummary.Empty, await h.Notifier.SendUnsentAsync());
        Assert.Empty(h.Notify.Sent);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task SendUnsentAsync_BlankAddress_StaysUnsentAndWarnsOnlyOncePerDay(string? address)
    {
        var h = await Harness.CreateAsync();
        h.Queue(address);

        var first = await h.Notifier.SendUnsentAsync();
        await h.Notifier.SendUnsentAsync();

        Assert.Equal(new OnlineOrderEmailRunSummary(1, 0, 1, 0), first);
        Assert.Empty(h.Notify.Sent);
        Assert.Single(h.PendingOrders.Unsent);
        Assert.Single(h.Logger.At(LogLevel.Warning));
    }

    [Fact]
    public async Task SendUnsentAsync_TemplateIdNotConfigured_StaysUnsent()
    {
        var h = await Harness.CreateAsync(approvedTemplate: string.Empty);
        h.Queue("customer@example.com");
        h.Queue("customer@example.com", isApproved: false);

        var summary = await h.Notifier.SendUnsentAsync();

        Assert.Equal(new OnlineOrderEmailRunSummary(2, 1, 1, 0), summary);
        Assert.Equal(DeclinedTemplate, Assert.Single(h.Notify.Sent).TemplateId);
        Assert.True(Assert.Single(h.PendingOrders.Unsent).IsApproved);
    }

    [Fact]
    public async Task SendUnsentAsync_NotifyFailure_StaysUnsentAndTheNextRunSendsItExactlyOnce()
    {
        var h = await Harness.CreateAsync();
        h.Queue("bad@example.com");
        h.Queue("good@example.com");
        h.Notify.FailFor = address => address == "bad@example.com" ? new InvalidOperationException("Notify is down") : null;

        var first = await h.Notifier.SendUnsentAsync();

        Assert.Equal(new OnlineOrderEmailRunSummary(2, 1, 0, 1), first);
        Assert.Equal("good@example.com", Assert.Single(h.Notify.Sent).Email);
        Assert.Equal("bad@example.com", Assert.Single(h.PendingOrders.Unsent).Email);

        h.Notify.FailFor = null;
        var second = await h.Notifier.SendUnsentAsync();

        Assert.Equal(new OnlineOrderEmailRunSummary(1, 1, 0, 0), second);
        Assert.Equal(["good@example.com", "bad@example.com"], h.Notify.Sent.Select(s => s.Email));
        Assert.Empty(h.PendingOrders.Unsent);
    }

    [Fact]
    public async Task SendUnsentAsync_RepeatedFailure_LogsOncePerDayWithoutTheAddressOrNotes()
    {
        var h = await Harness.CreateAsync();
        h.Queue("bad@example.com", notes: "private comment");
        h.Notify.FailFor = _ => new InvalidOperationException("Notify is down");

        await h.Notifier.SendUnsentAsync();
        await h.Notifier.SendUnsentAsync();

        Assert.Single(h.Logger.At(LogLevel.Error));
        h.Logger.AssertNoneContain("bad@example.com", "private comment", "@");
    }

    [Fact]
    public async Task SendForOrderAsync_SendsOnlyThatOrdersRows()
    {
        var h = await Harness.CreateAsync();
        var otherOrder = Guid.NewGuid();
        h.Queue("customer@example.com");
        h.Queue("someone-else@example.com", pendingContractId: otherOrder);

        await h.Notifier.SendForOrderAsync(h.PendingContractId);

        Assert.Equal("customer@example.com", Assert.Single(h.Notify.Sent).Email);
        Assert.Equal(otherOrder, Assert.Single(h.PendingOrders.Unsent).PendingContractId);
    }

    [Fact]
    public async Task SendForOrderAsync_AnythingFails_NeverThrowsAndLeavesTheRowUnsent()
    {
        var h = await Harness.CreateAsync();
        h.Queue("customer@example.com");
        h.PendingOrders.GetUnsentFailure = new InvalidOperationException("database is down");

        await h.Notifier.SendForOrderAsync(h.PendingContractId);

        Assert.Empty(h.Notify.Sent);
        Assert.Single(h.PendingOrders.Unsent);
        Assert.Single(h.Logger.At(LogLevel.Error));
    }

    [Fact]
    public async Task SendForOrderAsync_Cancelled_Propagates()
    {
        var h = await Harness.CreateAsync();
        h.Queue("customer@example.com");
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => h.Notifier.SendForOrderAsync(h.PendingContractId, cts.Token));
    }

    [Fact]
    public async Task ImmediateSendAndScheduler_RunningTogether_SendEachRowOnce()
    {
        var slow = new SlowNotifyClient();
        var h = await Harness.CreateAsync(client: slow);
        h.Queue("a@example.com");
        h.Queue("b@example.com");
        h.Queue("c@example.com");

        await Task.WhenAll(h.Notifier.SendForOrderAsync(h.PendingContractId), h.Notifier.SendUnsentAsync());

        Assert.Equal(3, slow.Sent.Count);
        Assert.Equal(3, slow.Sent.Select(s => s.Email).Distinct().Count());
        Assert.Empty(h.PendingOrders.Unsent);
    }
}
