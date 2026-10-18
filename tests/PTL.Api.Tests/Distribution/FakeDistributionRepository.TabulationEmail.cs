using PTL.Core.Distribution;

namespace PTL.Api.Tests.Distribution;

internal sealed partial class FakeDistributionRepository
{
    public List<TabulationEmailCandidateEntity> EmailCandidates { get; } = [];

    public List<TabulationConsultantEntity> EmailConsultants { get; } = [];

    public List<NotificationEmailEntity> UnsentNotifications { get; } = [];

    public List<(DateTime From, DateTime ToExclusive)> EmailCandidateRequests { get; } = [];

    public List<(Guid Id, int State)> EmailStates { get; } = [];

    public List<(Guid Id, DateTime DateSent)> NotificationsSent { get; } = [];

    public Exception? EmailCandidatesError { get; set; }

    public Exception? EmailStateError { get; set; }

    // When set, EmailStateError applies only to this scheme.
    public Guid? EmailStateErrorOnlyFor { get; set; }

    public Exception? NotificationSentError { get; set; }

    public int ConsultantLoads { get; private set; }

    public int UnsentLoads { get; private set; }

    public Task<IReadOnlyList<TabulationEmailCandidateEntity>> GetTabulationEmailCandidatesAsync(
        DateTime from, DateTime toExclusive, CancellationToken cancellationToken = default)
    {
        EmailCandidateRequests.Add((from, toExclusive));
        return EmailCandidatesError is null
            ? Task.FromResult<IReadOnlyList<TabulationEmailCandidateEntity>>([.. EmailCandidates])
            : Task.FromException<IReadOnlyList<TabulationEmailCandidateEntity>>(EmailCandidatesError);
    }

    public Task<IReadOnlyList<TabulationConsultantEntity>> GetTabulationEmailConsultantsAsync(CancellationToken cancellationToken = default)
    {
        ConsultantLoads++;
        return Task.FromResult<IReadOnlyList<TabulationConsultantEntity>>([.. EmailConsultants]);
    }

    public Task SetTabulationEmailStateAsync(Guid monthlyDistributionSchemeId, int emailState, CancellationToken cancellationToken = default)
    {
        if (EmailStateError is not null && (EmailStateErrorOnlyFor is null || EmailStateErrorOnlyFor == monthlyDistributionSchemeId))
        {
            return Task.FromException(EmailStateError);
        }

        EmailStates.Add((monthlyDistributionSchemeId, emailState));

        // Like the stored procedure, the next candidate query sees the new state.
        foreach (var candidate in EmailCandidates.Where(c => c.MonthlyDistributionSchemeId == monthlyDistributionSchemeId))
        {
            candidate.EmailState = emailState;
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<NotificationEmailEntity>> GetUnsentNotificationEmailsAsync(CancellationToken cancellationToken = default)
    {
        UnsentLoads++;
        return Task.FromResult<IReadOnlyList<NotificationEmailEntity>>([.. UnsentNotifications]);
    }

    public Task MarkNotificationEmailSentAsync(Guid notificationEmailId, DateTime dateSent, CancellationToken cancellationToken = default)
    {
        if (NotificationSentError is not null)
        {
            return Task.FromException(NotificationSentError);
        }

        NotificationsSent.Add((notificationEmailId, dateSent));
        return Task.CompletedTask;
    }
}
