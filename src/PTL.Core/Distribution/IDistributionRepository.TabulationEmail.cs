namespace PTL.Core.Distribution;

public partial interface IDistributionRepository
{
    // Distributions sent to the Test Consultant (fldIsReadyForTestConsultant) with a ready date in [from, toExclusive).
    Task<IReadOnlyList<TabulationEmailCandidateEntity>> GetTabulationEmailCandidatesAsync(
        DateTime from, DateTime toExclusive, CancellationToken cancellationToken = default);

    // spgaUserAllTestConsultant.
    Task<IReadOnlyList<TabulationConsultantEntity>> GetTabulationEmailConsultantsAsync(CancellationToken cancellationToken = default);

    // spuMonthlyDistributionSchemeForEmail.
    Task SetTabulationEmailStateAsync(Guid monthlyDistributionSchemeId, int emailState, CancellationToken cancellationToken = default);

    // spgaNotificationEmail - rows with no fldDateSent.
    Task<IReadOnlyList<NotificationEmailEntity>> GetUnsentNotificationEmailsAsync(CancellationToken cancellationToken = default);

    // spuNotificationEmail.
    Task MarkNotificationEmailSentAsync(Guid notificationEmailId, DateTime dateSent, CancellationToken cancellationToken = default);
}
