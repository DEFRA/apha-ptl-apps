using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PTL.Api.Tests.Customer;
using PTL.Api.Tests.Distribution;
using PTL.Api.Tests.Lookup;
using PTL.Api.Tests.Scheme;
using PTL.Core.Contract;
using PTL.Core.Contract.PendingOrder;
using PTL.Core.Lookup;
using CoreScheme = PTL.Core.Scheme.Scheme;

namespace PTL.Api.Tests.Contract;

// Legacy email.vb ProcessOnlineContractOrderEmails: each unsent tblOnlineContractOrderEmail row of a decided order is sent
// through GOV.UK Notify and marked sent only afterwards, so a failed send stays unsent.
public class PendingOrderNotifierTests
{
    private const string ApprovedTemplate = OnlineOrderEmailTemplates.ApprovedId;
    private const string DeclinedTemplate = OnlineOrderEmailTemplates.DeclinedId;
    private const int YearId = 2026;

    private static readonly DateTimeOffset Now = new(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);

    private sealed class Harness
    {
        public FakePendingOrderRepository PendingOrders { get; } = new();
        public FakeContractRepository Contracts { get; } = new();
        public RecordingNotifyClient Notify { get; } = new();
        public MessageCapturingLogger<PendingOrderNotifier> Logger { get; } = new();
        public PendingOrderNotifier Notifier { get; private set; } = null!;
        public Guid PendingContractId { get; } = Guid.NewGuid();
        public Guid ContractId { get; } = Guid.NewGuid();
        public string QalNumber { get; private set; } = string.Empty;

        public static async Task<Harness> CreateAsync(bool enabled = true)
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
                new PendingOrderEmailPersonaliser(h.PendingOrders, h.Contracts, schemes, customers, participants, lookups),
                h.Notify,
                Options.Create(new OnlineOrderEmailOptions { Enabled = enabled }),
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

    private static string RunSummary(Harness h) =>
        Assert.Single(h.Logger.At(LogLevel.Information), e => e.Message.StartsWith("Online contract order email run complete", StringComparison.Ordinal)).Message;

    [Fact]
    public async Task SendForOrderAsync_Approved_SendsTheApprovedTemplateWithTheLegacyTokensAndMarksItSent()
    {
        var h = await Harness.CreateAsync();
        var row = h.Queue("customer@example.com");

        await h.Notifier.SendForOrderAsync(h.PendingContractId);

        Assert.Equal("Online contract order email run complete. Unsent=1 Sent=1 Skipped=0 Failed=0", RunSummary(h));
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
    public async Task SendForOrderAsync_Declined_SendsTheDeclinedTemplateWithTheFirstActiveLabCodeAndNoSchemeList()
    {
        var h = await Harness.CreateAsync();
        h.Queue("customer@example.com", isApproved: false, notes: "Not eligible");

        await h.Notifier.SendForOrderAsync(h.PendingContractId);

        var sent = Assert.Single(h.Notify.Sent);
        Assert.Equal(DeclinedTemplate, sent.TemplateId);
        Assert.Equal("001", sent.Personalisation!["ParticipantName"]);
        Assert.Equal("Declined", sent.Personalisation["status"]);
        Assert.Equal(h.QalNumber, sent.Personalisation["CustomerName"]);
        Assert.Equal("Not eligible", sent.Personalisation["Notes"]);
        Assert.False(sent.Personalisation.ContainsKey("ApprovedSchemeOrders"));
    }

    [Fact]
    public async Task SendForOrderAsync_UnknownYear_UsesTheLegacyPlaceholder()
    {
        var h = await Harness.CreateAsync();
        h.Queue("customer@example.com").YearId = 1999;

        await h.Notifier.SendForOrderAsync(h.PendingContractId);

        Assert.Equal("####", Assert.Single(h.Notify.Sent).Personalisation!["ContractYear"]);
    }

    [Fact]
    public async Task SendForOrderAsync_ContractWithNoItems_SendsAnEmptySchemeList()
    {
        var h = await Harness.CreateAsync();
        h.Contracts.ContractItems = null;
        h.Queue("customer@example.com");

        await h.Notifier.SendForOrderAsync(h.PendingContractId);

        Assert.Equal(string.Empty, Assert.Single(h.Notify.Sent).Personalisation!["ApprovedSchemeOrders"]);
    }

    [Fact]
    public async Task SendForOrderAsync_Disabled_SendsNothingAndLeavesTheRowsUnsent()
    {
        var h = await Harness.CreateAsync(enabled: false);
        h.Queue("customer@example.com");

        await h.Notifier.SendForOrderAsync(h.PendingContractId);

        Assert.Equal(0, h.PendingOrders.GetUnsentCalls);
        Assert.Empty(h.Notify.Sent);
        Assert.Single(h.PendingOrders.Unsent);
    }

    [Fact]
    public async Task SendForOrderAsync_NoRows_DoesNothing()
    {
        var h = await Harness.CreateAsync();

        await h.Notifier.SendForOrderAsync(h.PendingContractId);

        Assert.Empty(h.Notify.Sent);
        Assert.Empty(h.Logger.Entries);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task SendForOrderAsync_BlankAddress_StaysUnsentAndWarns(string? address)
    {
        var h = await Harness.CreateAsync();
        h.Queue(address);

        await h.Notifier.SendForOrderAsync(h.PendingContractId);

        Assert.Equal("Online contract order email run complete. Unsent=1 Sent=0 Skipped=1 Failed=0", RunSummary(h));
        Assert.Empty(h.Notify.Sent);
        Assert.Single(h.PendingOrders.Unsent);
        Assert.Single(h.Logger.At(LogLevel.Warning));
    }

    [Fact]
    public async Task SendForOrderAsync_NotifyFailure_StaysUnsentAndTheOtherRowsStillGo()
    {
        var h = await Harness.CreateAsync();
        h.Queue("bad@example.com");
        h.Queue("good@example.com");
        h.Notify.FailFor = address => address == "bad@example.com" ? new InvalidOperationException("Notify is down") : null;

        await h.Notifier.SendForOrderAsync(h.PendingContractId);

        Assert.Equal("Online contract order email run complete. Unsent=2 Sent=1 Skipped=0 Failed=1", RunSummary(h));
        Assert.Equal("good@example.com", Assert.Single(h.Notify.Sent).Email);
        Assert.Equal("bad@example.com", Assert.Single(h.PendingOrders.Unsent).Email);
        Assert.Single(h.Logger.At(LogLevel.Error));
    }

    [Fact]
    public async Task SendForOrderAsync_Failure_LogsWithoutTheAddressOrNotes()
    {
        var h = await Harness.CreateAsync();
        h.Queue("bad@example.com", notes: "private comment");
        h.Notify.FailFor = _ => new InvalidOperationException("Notify is down");

        await h.Notifier.SendForOrderAsync(h.PendingContractId);

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
    public async Task SendForOrderAsync_CancelledWhileSending_PropagatesAndLeavesTheRowUnsent()
    {
        var h = await Harness.CreateAsync();
        h.Queue("customer@example.com");
        h.Notify.FailFor = _ => new OperationCanceledException();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => h.Notifier.SendForOrderAsync(h.PendingContractId));

        Assert.Single(h.PendingOrders.Unsent);
        Assert.Empty(h.Logger.At(LogLevel.Error));
    }
}
