using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PTL.Core.Configuration;
using PTL.Core.Notifications;
using PTL.Core.Participant;

namespace PTL.Core.Distribution;

public sealed record PostedParticipant(Guid ParticipantId, Guid MonthlyDistributionSchemeId, string Distribution, string SchemeName);

public interface IDistributionPostedNotifier
{
    // Legacy DistributionPostedEmailCollection.AddNewDistributionPostedEmail: one queued email per
    // participant email address (primary, plus the secondary if there is one), never duplicating
    // an email still waiting to be sent.
    Task<IReadOnlyList<DistributionPostedEmailEntity>> BuildEmailsAsync(
        IReadOnlyList<PostedParticipant> posted, CancellationToken cancellationToken = default);

    // Replaces legacy's ProficiencyTestingService SMTP send with GOV.UK Notify.
    Task DispatchAsync(IReadOnlyList<DistributionPostedEmailEntity> emails, CancellationToken cancellationToken = default);
}

public sealed class DistributionPostedNotifier(
    IDistributionRepository repository,
    IParticipantService participantService,
    INotifyClient notifyClient,
    IOptions<ExternalOptions> externalOptions,
    TimeProvider timeProvider,
    ILogger<DistributionPostedNotifier> logger) : IDistributionPostedNotifier
{
    private static readonly Action<ILogger, Guid, Exception?> LogSendFailedMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Warning,
            new EventId(1, nameof(LogSendFailedMessage)),
            "Distribution Posted notification {DistributionPostedEmailId} could not be sent and stays queued");

    public async Task<IReadOnlyList<DistributionPostedEmailEntity>> BuildEmailsAsync(
        IReadOnlyList<PostedParticipant> posted, CancellationToken cancellationToken = default)
    {
        if (posted.Count == 0)
        {
            return [];
        }

        var unsent = await repository.GetUnsentDistributionPostedEmailsAsync(cancellationToken);
        var queued = unsent
            .Select(e => (e.ParticipantId, e.MonthlyDistributionSchemeId, Email: e.Email.ToUpperInvariant()))
            .ToHashSet();

        var emails = new List<DistributionPostedEmailEntity>();
        foreach (var item in posted)
        {
            var participant = await participantService.GetParticipantAsync(item.ParticipantId, cancellationToken);
            if (participant is null)
            {
                continue;
            }

            foreach (var address in new[] { participant.Email, participant.Email2 })
            {
                if (string.IsNullOrWhiteSpace(address)
                    || !queued.Add((item.ParticipantId, item.MonthlyDistributionSchemeId, address.ToUpperInvariant())))
                {
                    continue;
                }

                emails.Add(new DistributionPostedEmailEntity
                {
                    DistributionPostedEmailId = Guid.NewGuid(),
                    ParticipantId = item.ParticipantId,
                    MonthlyDistributionSchemeId = item.MonthlyDistributionSchemeId,
                    Email = address,
                    Distribution = item.Distribution,
                    SchemeName = item.SchemeName,
                });
            }
        }

        return emails;
    }

    public async Task DispatchAsync(IReadOnlyList<DistributionPostedEmailEntity> emails, CancellationToken cancellationToken = default)
    {
        if (emails.Count == 0)
        {
            return;
        }

        foreach (var email in emails)
        {
            var now = timeProvider.GetUtcNow().UtcDateTime;
            var personalisation = new Dictionary<string, string>
            {
                ["scheme_name"] = email.SchemeName,
                ["distribution"] = email.Distribution,
                ["external_link"] = externalOptions.Value.AppUrl,
                ["date"] = now.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
            };

            try
            {
                await notifyClient.SendEmailAsync(DistributionPostedNotificationOptions.TemplateId, email.Email, personalisation, email.DistributionPostedEmailId.ToString(), cancellationToken);
                await repository.MarkDistributionPostedEmailSentAsync(email.DistributionPostedEmailId, now, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // The Save has already committed; a failed send must not undo it - the row stays queued.
                LogSendFailedMessage(logger, email.DistributionPostedEmailId, ex);
            }
        }
    }
}
