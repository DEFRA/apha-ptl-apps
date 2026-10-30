using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PTL.Api.Tests.Distribution;
using PTL.Core.Contract.ImportPermit;

namespace PTL.Api.Tests.Contract;

// Legacy email.vb ProcessContractOrderImportPermitEmails: one grouped email per customer and one per
// participant, at 28/14/7 days before overseas posting, sent through GOV.UK Notify. All eligibility is
// decided by spgaContractOrderImportPermitForEmail, so these tests drive the service from its result sets.
public class ImportPermitReminderServiceTests
{
    private static readonly Guid Contract = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Customer = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Distribution = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid Participant = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private const string CustomerEmail = "customer@example.test";
    private const string ParticipantEmail = "participant@example.test";

    private sealed class FakeRepository : IImportPermitReminderRepository
    {
        public Dictionary<int, ImportPermitReminderData> ByOffset { get; } = [];

        public List<int> Queried { get; } = [];

        public List<(Guid ContractId, Guid CustomerId, Guid DistributionId, Guid? ParticipantId, int ReminderType)> Recorded { get; } = [];

        public Task<ImportPermitReminderData> GetDueAsync(int daysBeforeDistribution, CancellationToken cancellationToken = default)
        {
            Queried.Add(daysBeforeDistribution);
            return Task.FromResult(ByOffset.TryGetValue(daysBeforeDistribution, out var data) ? data : ImportPermitReminderData.Empty);
        }

        public Task MarkReminderSentAsync(
            Guid contractId,
            Guid customerId,
            Guid monthlyDistributionSchemeId,
            Guid? participantId,
            int reminderType,
            CancellationToken cancellationToken = default)
        {
            Recorded.Add((contractId, customerId, monthlyDistributionSchemeId, participantId, reminderType));
            return Task.CompletedTask;
        }
    }

    private sealed class Harness
    {
        public FakeRepository Repository { get; } = new();
        public RecordingNotifyClient Notify { get; } = new();
        public MessageCapturingLogger<ImportPermitReminderService> Logger { get; } = new();
        public SettableTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 10, 30, 9, 0, 0, TimeSpan.Zero));
        public ImportPermitReminderService Service { get; }

        public Harness() =>
            Service = new ImportPermitReminderService(
                Repository,
                Notify,
                new ImportPermitReminderGuard(),
                Clock,
                Logger);

        public void Due(int offset, IEnumerable<ImportPermitCustomerEntity>? customers = null, IEnumerable<ImportPermitParticipantEntity>? participants = null) =>
            Repository.ByOffset[offset] = new ImportPermitReminderData(
                (customers ?? []).ToList(),
                (participants ?? []).ToList());
    }

    private static ImportPermitCustomerEntity CustomerRow(
        Guid? customerId = null,
        Guid? distributionId = null,
        Guid? contractId = null,
        string email = CustomerEmail,
        bool reminderSent = false) =>
        new()
        {
            ContractId = contractId ?? Contract,
            CustomerId = customerId ?? Customer,
            MonthlyDistributionSchemeId = distributionId ?? Distribution,
            CustomerName = "Overseas Labs Ltd",
            CustomerEmail = email,
            QalNumber = "QAL123",
            DistributionReference = "D-001",
            SchemeIdentifier = "PT-900",
            ReminderSent = reminderSent,
        };

    private static ImportPermitParticipantEntity ParticipantRow(
        Guid? participantId = null,
        Guid? customerId = null,
        Guid? distributionId = null,
        string email = ParticipantEmail,
        string labCode = "LAB-1",
        string schemeName = "Salmonella") =>
        new()
        {
            ContractId = Contract,
            CustomerId = customerId ?? Customer,
            ParticipantId = participantId ?? Participant,
            ParticipantSchemeId = Guid.NewGuid(),
            SchemeId = Guid.NewGuid(),
            MonthlyDistributionSchemeId = distributionId ?? Distribution,
            SchemeName = schemeName,
            QalNumber = "QAL123",
            ParticipantEmail = email,
            LabCode = labCode,
            DistributionReference = "D-001",
            SchemeIdentifier = "PT-900",
        };

    // ---- Offsets ----

    [Fact]
    public async Task ProcessRemindersAsync_QueriesTwentyEightFourteenAndSevenDaysInOrder()
    {
        var h = new Harness();

        await h.Service.ProcessRemindersAsync();

        Assert.Equal([28, 14, 7], h.Repository.Queried);
    }

    [Fact]
    public async Task ProcessRemindersAsync_NothingDue_SendsNothing()
    {
        var h = new Harness();

        var summary = await h.Service.ProcessRemindersAsync();

        Assert.Empty(h.Notify.Sent);
        Assert.Empty(h.Repository.Recorded);
        Assert.Equal(ImportPermitReminderRunSummary.Empty, summary);
    }

    // ---- Order ----

    [Fact]
    public async Task ProcessRemindersAsync_SendsTheCustomerEmailBeforeTheParticipantEmail()
    {
        var h = new Harness();
        h.Due(28, [CustomerRow()], [ParticipantRow()]);

        await h.Service.ProcessRemindersAsync();

        Assert.Equal(
            [ImportPermitReminderTemplates.CustomerId, ImportPermitReminderTemplates.ParticipantId],
            h.Notify.Sent.Select(s => s.TemplateId));
        Assert.Equal([CustomerEmail, ParticipantEmail], h.Notify.Sent.Select(s => s.Email));
    }

    // ---- Customer email ----

    [Fact]
    public async Task ProcessRemindersAsync_CustomerEmailListsEveryParticipantAcrossEveryDistribution()
    {
        var otherDistribution = Guid.NewGuid();
        var h = new Harness();
        h.Due(
            28,
            [CustomerRow(), CustomerRow(distributionId: otherDistribution)],
            [
                ParticipantRow(labCode: "LAB-1", schemeName: "Salmonella"),
                ParticipantRow(participantId: Guid.NewGuid(), distributionId: otherDistribution, labCode: "LAB-2", schemeName: "Campylobacter"),
            ]);

        await h.Service.ProcessRemindersAsync();

        var customerEmail = Assert.Single(h.Notify.Sent, s => s.TemplateId == ImportPermitReminderTemplates.CustomerId);
        var list = Assert.Contains("participant_list", customerEmail.Personalisation!);
        Assert.Contains("LAB-1", list, StringComparison.Ordinal);
        Assert.Contains("LAB-2", list, StringComparison.Ordinal);
        Assert.Contains("Salmonella", list, StringComparison.Ordinal);
        Assert.Contains("Campylobacter", list, StringComparison.Ordinal);
        Assert.Contains("QAL123", list, StringComparison.Ordinal);
        Assert.Contains("D-001", list, StringComparison.Ordinal);
        Assert.Contains("PT-900", list, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProcessRemindersAsync_SendsOneEmailPerCustomerNotPerRow()
    {
        var h = new Harness();
        h.Due(28, [CustomerRow(), CustomerRow(distributionId: Guid.NewGuid())], [ParticipantRow()]);

        await h.Service.ProcessRemindersAsync();

        Assert.Single(h.Notify.Sent, s => s.TemplateId == ImportPermitReminderTemplates.CustomerId);
    }

    [Fact]
    public async Task ProcessRemindersAsync_RecordsOneLedgerRowPerCustomerRowWithNoParticipantId()
    {
        var otherDistribution = Guid.NewGuid();
        var h = new Harness();
        h.Due(
            28,
            [CustomerRow(), CustomerRow(distributionId: otherDistribution)],
            [ParticipantRow(), ParticipantRow(participantId: Guid.NewGuid(), distributionId: otherDistribution)]);

        await h.Service.ProcessRemindersAsync();

        var customerRows = h.Repository.Recorded.Where(r => r.ParticipantId is null).ToList();
        Assert.Equal(2, customerRows.Count);
        Assert.Equal([Distribution, otherDistribution], customerRows.Select(r => r.DistributionId));
        Assert.All(customerRows, r =>
        {
            Assert.Equal(Contract, r.ContractId);
            Assert.Equal(Customer, r.CustomerId);
            Assert.Equal(28, r.ReminderType);
        });
    }

    [Fact]
    public async Task ProcessRemindersAsync_CustomerAlreadyReminded_IsSkipped()
    {
        var h = new Harness();
        h.Due(28, [CustomerRow(reminderSent: true)], [ParticipantRow()]);

        await h.Service.ProcessRemindersAsync();

        Assert.DoesNotContain(h.Notify.Sent, s => s.TemplateId == ImportPermitReminderTemplates.CustomerId);
        Assert.DoesNotContain(h.Repository.Recorded, r => r.ParticipantId is null);
    }

    [Fact]
    public async Task ProcessRemindersAsync_CustomerWithNoParticipants_IsNotEmailed()
    {
        var h = new Harness();
        h.Due(28, [CustomerRow()], []);

        await h.Service.ProcessRemindersAsync();

        Assert.Empty(h.Notify.Sent);
        Assert.Empty(h.Repository.Recorded);
    }

    [Fact]
    public async Task ProcessRemindersAsync_CustomerWithNoEmailAddress_IsSkippedAndWarned()
    {
        var h = new Harness();
        h.Due(28, [CustomerRow(email: "  ")], [ParticipantRow()]);

        var summary = await h.Service.ProcessRemindersAsync();

        Assert.DoesNotContain(h.Notify.Sent, s => s.TemplateId == ImportPermitReminderTemplates.CustomerId);
        Assert.Equal(1, summary.Skipped);
        var warning = Assert.Single(h.Logger.At(LogLevel.Warning));
        Assert.Contains("no email address", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProcessRemindersAsync_CustomerSendFails_IsLoggedAndNotRecordedButParticipantsStillSend()
    {
        var h = new Harness();
        h.Due(28, [CustomerRow()], [ParticipantRow()]);
        h.Notify.FailFor = email => email == CustomerEmail ? new InvalidOperationException("notify down") : null;

        var summary = await h.Service.ProcessRemindersAsync();

        Assert.DoesNotContain(h.Repository.Recorded, r => r.ParticipantId is null);
        Assert.Single(h.Notify.Sent, s => s.TemplateId == ImportPermitReminderTemplates.ParticipantId);
        Assert.Equal(1, summary.Failed);
        Assert.Single(h.Logger.At(LogLevel.Error));
    }

    // ---- Participant email ----

    [Fact]
    public async Task ProcessRemindersAsync_ParticipantEmailCarriesTheLegacyTokens()
    {
        var h = new Harness();
        h.Due(28, [], [ParticipantRow()]);

        await h.Service.ProcessRemindersAsync();

        var sent = Assert.Single(h.Notify.Sent);
        Assert.Equal(ImportPermitReminderTemplates.ParticipantId, sent.TemplateId);
        Assert.Equal(
            new Dictionary<string, string>
            {
                ["qal_number"] = "QAL123",
                ["lab_code"] = "LAB-1",
                ["scheme_name"] = "Salmonella",
                ["scheme_identifier"] = "PT-900",
                ["distribution_reference"] = "D-001",
            },
            sent.Personalisation);
    }

    [Fact]
    public async Task ProcessRemindersAsync_RecordsOneLedgerRowPerParticipant()
    {
        var second = Guid.NewGuid();
        var h = new Harness();
        h.Due(28, [], [ParticipantRow(), ParticipantRow(participantId: second)]);

        await h.Service.ProcessRemindersAsync();

        var participantRows = h.Repository.Recorded.Where(r => r.ParticipantId is not null).ToList();
        Assert.Equal([Participant, second], participantRows.Select(r => r.ParticipantId));
        Assert.All(participantRows, r => Assert.Equal(28, r.ReminderType));
    }

    [Fact]
    public async Task ProcessRemindersAsync_ParticipantWithNoEmailAddress_IsSkippedAndWarned()
    {
        var h = new Harness();
        h.Due(28, [], [ParticipantRow(email: "")]);

        var summary = await h.Service.ProcessRemindersAsync();

        Assert.Empty(h.Notify.Sent);
        Assert.Empty(h.Repository.Recorded);
        Assert.Equal(1, summary.Skipped);
        Assert.Single(h.Logger.At(LogLevel.Warning));
    }

    [Fact]
    public async Task ProcessRemindersAsync_ParticipantSendFails_IsNotRecordedAndTheNextOneStillSends()
    {
        var second = Guid.NewGuid();
        var h = new Harness();
        h.Due(28, [], [ParticipantRow(email: "broken@example.test"), ParticipantRow(participantId: second)]);
        h.Notify.FailFor = email => email == "broken@example.test" ? new InvalidOperationException("notify down") : null;

        var summary = await h.Service.ProcessRemindersAsync();

        var recorded = Assert.Single(h.Repository.Recorded);
        Assert.Equal(second, recorded.ParticipantId);
        Assert.Equal(1, summary.Failed);
        Assert.Equal(1, summary.ParticipantEmailsSent);
    }

    // ---- Repeated polls: the job runs on every scheduler poll, so the same problem is logged once a day ----

    [Fact]
    public async Task ProcessRemindersAsync_CustomerWithNoEmailAddress_IsWarnedOnceADayNotOnEveryPoll()
    {
        var h = new Harness();
        h.Due(28, [CustomerRow(email: "")], [ParticipantRow()]);

        var first = await h.Service.ProcessRemindersAsync();
        var second = await h.Service.ProcessRemindersAsync();

        Assert.Equal(1, first.Skipped);
        Assert.Equal(1, second.Skipped);
        Assert.Single(h.Logger.At(LogLevel.Warning));

        h.Clock.Now = h.Clock.Now.AddDays(1);
        await h.Service.ProcessRemindersAsync();

        Assert.Equal(2, h.Logger.At(LogLevel.Warning).Count());
    }

    [Fact]
    public async Task ProcessRemindersAsync_ParticipantWithNoEmailAddress_IsWarnedOnceADayNotOnEveryPoll()
    {
        var h = new Harness();
        h.Due(28, [], [ParticipantRow(email: "")]);

        await h.Service.ProcessRemindersAsync();
        await h.Service.ProcessRemindersAsync();
        Assert.Single(h.Logger.At(LogLevel.Warning));

        h.Clock.Now = h.Clock.Now.AddDays(1);
        await h.Service.ProcessRemindersAsync();

        Assert.Equal(2, h.Logger.At(LogLevel.Warning).Count());
    }

    [Fact]
    public async Task ProcessRemindersAsync_EachDifferentParticipantWithNoEmailAddress_IsWarnedOnce()
    {
        var h = new Harness();
        h.Due(28, [], [ParticipantRow(email: ""), ParticipantRow(participantId: Guid.NewGuid(), email: "")]);

        await h.Service.ProcessRemindersAsync();

        Assert.Equal(2, h.Logger.At(LogLevel.Warning).Count());
    }

    [Fact]
    public async Task ProcessRemindersAsync_CustomerSendFailure_IsRetriedOnEveryPollButLoggedOnceADay()
    {
        var h = new Harness();
        h.Due(28, [CustomerRow()], [ParticipantRow()]);
        var attempts = 0;
        h.Notify.FailFor = email =>
        {
            if (email != CustomerEmail)
            {
                return null;
            }

            attempts++;
            return new InvalidOperationException("notify down");
        };

        var first = await h.Service.ProcessRemindersAsync();
        var second = await h.Service.ProcessRemindersAsync();

        Assert.Equal(2, attempts);
        Assert.Equal(1, first.Failed);
        Assert.Equal(1, second.Failed);
        Assert.Single(h.Logger.At(LogLevel.Error));

        h.Clock.Now = h.Clock.Now.AddDays(1);
        await h.Service.ProcessRemindersAsync();

        Assert.Equal(2, h.Logger.At(LogLevel.Error).Count());
    }

    [Fact]
    public async Task ProcessRemindersAsync_ParticipantSendFailure_IsRetriedOnEveryPollButLoggedOnceADay()
    {
        var h = new Harness();
        h.Due(28, [], [ParticipantRow()]);
        var attempts = 0;
        h.Notify.FailFor = _ =>
        {
            attempts++;
            return new InvalidOperationException("notify down");
        };

        await h.Service.ProcessRemindersAsync();
        await h.Service.ProcessRemindersAsync();

        Assert.Equal(2, attempts);
        Assert.Empty(h.Repository.Recorded);
        Assert.Single(h.Logger.At(LogLevel.Error));

        h.Clock.Now = h.Clock.Now.AddDays(1);
        await h.Service.ProcessRemindersAsync();

        Assert.Equal(2, h.Logger.At(LogLevel.Error).Count());
    }

    // ---- Summary and logging ----

    [Fact]
    public async Task ProcessRemindersAsync_SummaryAggregatesEveryOffset()
    {
        var h = new Harness();
        h.Due(28, [CustomerRow()], [ParticipantRow()]);
        h.Due(7, [CustomerRow(customerId: Guid.NewGuid(), email: "other@example.test")], [ParticipantRow(participantId: Guid.NewGuid())]);

        var summary = await h.Service.ProcessRemindersAsync();

        Assert.Equal(1, summary.CustomersDue);
        Assert.Equal(1, summary.CustomerEmailsSent);
        Assert.Equal(2, summary.ParticipantsDue);
        Assert.Equal(2, summary.ParticipantEmailsSent);
        Assert.Equal(0, summary.Failed);
    }

    [Fact]
    public async Task ProcessRemindersAsync_DoesNotLogEmailAddresses()
    {
        var h = new Harness();
        h.Due(28, [CustomerRow()], [ParticipantRow()]);

        await h.Service.ProcessRemindersAsync();

        h.Logger.AssertNoneContain(CustomerEmail, ParticipantEmail, "@");
    }

    [Fact]
    public async Task ProcessRemindersAsync_SecondRunAfterTheProcedureExcludesThem_SendsNothing()
    {
        var h = new Harness();
        h.Due(28, [CustomerRow()], [ParticipantRow()]);
        await h.Service.ProcessRemindersAsync();
        h.Notify.Sent.Clear();

        // The procedure drops everything already in tblImportPermitReminders.
        h.Due(28, [], []);
        await h.Service.ProcessRemindersAsync();

        Assert.Empty(h.Notify.Sent);
    }
}
