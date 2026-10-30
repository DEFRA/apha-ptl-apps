using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PTL.Core.Notifications;

namespace PTL.Core.Contract.ImportPermit;

// Import Permit reminders - legacy ProficiencyTestingService email.vb
// ProcessContractOrderImportPermitEmails, sent through GOV.UK Notify instead of SMTP.
//
// This is a faithful port: every eligibility rule, the 28/14/7 day bands with their 3-day catch-up
// window, the permit-expiry test and the de-duplication ledger live in
// spgaContractOrderImportPermitForEmail and tblImportPermitReminders. The code below only loops,
// sends and records.
public interface IImportPermitReminderService
{
    Task<ImportPermitReminderRunSummary> ProcessRemindersAsync(CancellationToken cancellationToken = default);
}

public sealed partial class ImportPermitReminderService(
    IImportPermitReminderRepository repository,
    INotifyClient notifyClient,
    ImportPermitReminderGuard guard,
    TimeProvider timeProvider,
    IOptions<ImportPermitReminderOptions> options,
    ILogger<ImportPermitReminderService> logger) : IImportPermitReminderService
{
    public async Task<ImportPermitReminderRunSummary> ProcessRemindersAsync(CancellationToken cancellationToken = default)
    {
        if (!options.Value.Enabled)
        {
            LogDisabled(logger);
            return ImportPermitReminderRunSummary.Empty;
        }

        var total = ImportPermitReminderRunSummary.Empty;
        foreach (var offset in options.Value.Offsets)
        {
            var summary = await ProcessOffsetAsync(offset, cancellationToken);
            total = new ImportPermitReminderRunSummary(
                total.CustomersDue + summary.CustomersDue,
                total.CustomerEmailsSent + summary.CustomerEmailsSent,
                total.ParticipantsDue + summary.ParticipantsDue,
                total.ParticipantEmailsSent + summary.ParticipantEmailsSent,
                total.Skipped + summary.Skipped,
                total.Failed + summary.Failed);
        }

        LogRunSummary(logger, total.CustomersDue, total.CustomerEmailsSent, total.ParticipantsDue, total.ParticipantEmailsSent, total.Skipped, total.Failed);
        return total;
    }

    private async Task<ImportPermitReminderRunSummary> ProcessOffsetAsync(int offset, CancellationToken cancellationToken)
    {
        var data = await repository.GetDueAsync(offset, cancellationToken);

        // Legacy order: customers first, then participants, and it matters. The procedure builds its
        // customer set from the participants it has not yet reminded, so once a participant reminder is
        // recorded that participant - and any customer row that depended on them - disappears from the
        // next run. If a customer send fails while its participant sends succeed, that customer reminder
        // is lost for this offset. Carried over unchanged from email.vb.
        var (customersDue, customerEmailsSent, customerSkipped, customerFailed) = await SendCustomerRemindersAsync(data, offset, cancellationToken);
        var (participantsDue, participantEmailsSent, participantSkipped, participantFailed) = await SendParticipantRemindersAsync(data, offset, cancellationToken);

        return new ImportPermitReminderRunSummary(
            customersDue,
            customerEmailsSent,
            participantsDue,
            participantEmailsSent,
            customerSkipped + participantSkipped,
            customerFailed + participantFailed);
    }

    // One grouped email per customer, covering every participant across every distribution that customer
    // has due at this offset.
    private async Task<(int Due, int Sent, int Skipped, int Failed)> SendCustomerRemindersAsync(
        ImportPermitReminderData data, int offset, CancellationToken cancellationToken)
    {
        var processed = new HashSet<Guid>();
        int due = 0, sent = 0, skipped = 0, failed = 0;

        foreach (var customer in data.Customers)
        {
            if (string.IsNullOrWhiteSpace(customer.CustomerEmail))
            {
                skipped++;
                if (FirstLogToday($"customer-no-email:{customer.CustomerId}"))
                {
                    LogCustomerSkipped(logger, customer.QalNumber, offset, "no email address");
                }

                continue;
            }

            if (customer.ReminderSent || !processed.Add(customer.CustomerId))
            {
                continue;
            }

            // Every distribution this customer has due at this offset, then every participant on each of
            // them - one grouped email rather than one per distribution.
            var distributions = data.Customers
                .Where(c => c.CustomerId == customer.CustomerId)
                .Select(c => c.MonthlyDistributionSchemeId)
                .ToHashSet();
            var participants = data.Participants
                .Where(p => p.CustomerId == customer.CustomerId && distributions.Contains(p.MonthlyDistributionSchemeId))
                .ToList();
            if (participants.Count == 0)
            {
                continue;
            }

            due++;
            try
            {
                await notifyClient.SendEmailAsync(
                    ImportPermitReminderTemplates.CustomerId,
                    customer.CustomerEmail,
                    new Dictionary<string, string> { ["participant_list"] = BuildParticipantList(participants) },
                    $"{customer.CustomerId}:{offset}",
                    cancellationToken);
                sent++;
                LogCustomerSent(logger, customer.QalNumber, offset, participants.Count);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failed++;
                if (FirstLogToday($"customer-send-failed:{customer.CustomerId}"))
                {
                    LogCustomerSendFailed(logger, customer.QalNumber, offset, ex);
                }

                continue;
            }

            // Legacy marks every row of this customer, so each contract/distribution gets its own ledger row.
            foreach (var row in data.Customers.Where(c => c.CustomerId == customer.CustomerId && !c.ReminderSent))
            {
                row.ReminderSent = true;
                await repository.MarkReminderSentAsync(
                    row.ContractId, row.CustomerId, row.MonthlyDistributionSchemeId, participantId: null, offset, CancellationToken.None);
            }
        }

        return (due, sent, skipped, failed);
    }

    // One email per participant, independent of whether their customer was reminded.
    private async Task<(int Due, int Sent, int Skipped, int Failed)> SendParticipantRemindersAsync(
        ImportPermitReminderData data, int offset, CancellationToken cancellationToken)
    {
        int due = 0, sent = 0, skipped = 0, failed = 0;

        foreach (var participant in data.Participants)
        {
            if (string.IsNullOrWhiteSpace(participant.ParticipantEmail))
            {
                skipped++;
                if (FirstLogToday($"participant-no-email:{participant.ParticipantId}"))
                {
                    LogParticipantSkipped(logger, participant.LabCode, participant.QalNumber, offset, "no email address");
                }

                continue;
            }

            due++;
            try
            {
                await notifyClient.SendEmailAsync(
                    ImportPermitReminderTemplates.ParticipantId,
                    participant.ParticipantEmail,
                    Personalise(participant),
                    $"{participant.ParticipantSchemeId}:{participant.MonthlyDistributionSchemeId}:{offset}",
                    cancellationToken);
                sent++;
                LogParticipantSent(logger, participant.LabCode, participant.QalNumber, offset);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failed++;
                if (FirstLogToday($"participant-send-failed:{participant.ParticipantSchemeId}"))
                {
                    LogParticipantSendFailed(logger, participant.LabCode, participant.QalNumber, offset, ex);
                }

                continue;
            }

            await repository.MarkReminderSentAsync(
                participant.ContractId,
                participant.CustomerId,
                participant.MonthlyDistributionSchemeId,
                participant.ParticipantId,
                offset,
                CancellationToken.None);
        }

        return (due, sent, skipped, failed);
    }

    // The legacy HTML table of the customer template's <<ParticipantList>>, as the plain labelled
    // blocks GOV.UK Notify can render.
    private static string BuildParticipantList(IReadOnlyList<ImportPermitParticipantEntity> participants)
    {
        var list = new StringBuilder();
        foreach (var participant in participants)
        {
            list.AppendLine("---");
            list.AppendLine();
            list.AppendLine(CultureInfo.InvariantCulture, $"QAL number: {participant.QalNumber}");
            list.AppendLine(CultureInfo.InvariantCulture, $"Lab code: {participant.LabCode}");
            list.AppendLine(CultureInfo.InvariantCulture, $"Scheme name: {participant.SchemeName}");
            list.AppendLine(CultureInfo.InvariantCulture, $"Distribution reference: {participant.DistributionReference}");
            list.AppendLine(CultureInfo.InvariantCulture, $"Scheme identifier: {participant.SchemeIdentifier}");
            list.AppendLine();
        }

        return list.ToString().TrimEnd();
    }

    // The tokens of the legacy ParticipantImportPermitReminderBodyFile.txt.
    private static Dictionary<string, string> Personalise(ImportPermitParticipantEntity participant) =>
        new()
        {
            ["qal_number"] = participant.QalNumber,
            ["lab_code"] = participant.LabCode,
            ["scheme_name"] = participant.SchemeName,
            ["scheme_identifier"] = participant.SchemeIdentifier,
            ["distribution_reference"] = participant.DistributionReference,
        };

    private bool FirstLogToday(string key) => guard.FirstLogToday(key, DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime));

    [LoggerMessage(EventId = 101, Level = LogLevel.Information, Message = "Import permit reminders are disabled.")]
    private static partial void LogDisabled(ILogger logger);

    [LoggerMessage(EventId = 102, Level = LogLevel.Information,
        Message = "Import permit customer reminder sent. QalNumber={QalNumber} ReminderType={ReminderType} Participants={Participants}")]
    private static partial void LogCustomerSent(ILogger logger, string qalNumber, int reminderType, int participants);

    [LoggerMessage(EventId = 103, Level = LogLevel.Error,
        Message = "Import permit customer reminder could not be sent; it stays unreminded. QalNumber={QalNumber} ReminderType={ReminderType}")]
    private static partial void LogCustomerSendFailed(ILogger logger, string qalNumber, int reminderType, Exception exception);

    [LoggerMessage(EventId = 104, Level = LogLevel.Warning,
        Message = "Import permit customer reminder skipped: {Reason}. QalNumber={QalNumber} ReminderType={ReminderType}")]
    private static partial void LogCustomerSkipped(ILogger logger, string qalNumber, int reminderType, string reason);

    [LoggerMessage(EventId = 105, Level = LogLevel.Information,
        Message = "Import permit participant reminder sent. LabCode={LabCode} QalNumber={QalNumber} ReminderType={ReminderType}")]
    private static partial void LogParticipantSent(ILogger logger, string labCode, string qalNumber, int reminderType);

    [LoggerMessage(EventId = 106, Level = LogLevel.Error,
        Message = "Import permit participant reminder could not be sent; it stays unreminded. LabCode={LabCode} QalNumber={QalNumber} ReminderType={ReminderType}")]
    private static partial void LogParticipantSendFailed(ILogger logger, string labCode, string qalNumber, int reminderType, Exception exception);

    [LoggerMessage(EventId = 107, Level = LogLevel.Warning,
        Message = "Import permit participant reminder skipped: {Reason}. LabCode={LabCode} QalNumber={QalNumber} ReminderType={ReminderType}")]
    private static partial void LogParticipantSkipped(ILogger logger, string labCode, string qalNumber, int reminderType, string reason);

    [LoggerMessage(EventId = 108, Level = LogLevel.Information,
        Message = "Import permit reminder run complete. CustomersDue={CustomersDue} CustomerEmailsSent={CustomerEmailsSent} ParticipantsDue={ParticipantsDue} ParticipantEmailsSent={ParticipantEmailsSent} Skipped={Skipped} Failed={Failed}")]
    private static partial void LogRunSummary(ILogger logger, int customersDue, int customerEmailsSent, int participantsDue, int participantEmailsSent, int skipped, int failed);
}
