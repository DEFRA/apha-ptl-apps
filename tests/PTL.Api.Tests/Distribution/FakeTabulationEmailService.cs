using PTL.Core.Distribution;

namespace PTL.Api.Tests.Distribution;

// Records the schemes whose first Test Consultant email was requested after Save.
internal sealed class FakeTabulationEmailService : ITabulationEmailService
{
    public List<IReadOnlyList<Guid>> FirstEmailRequests { get; } = [];

    // Lets a test observe the world at the moment the email was requested.
    public Action? OnSendFirstEmails { get; set; }

    public Task SendFirstEmailsAsync(IReadOnlyList<Guid> monthlyDistributionSchemeIds, CancellationToken cancellationToken = default)
    {
        OnSendFirstEmails?.Invoke();
        FirstEmailRequests.Add(monthlyDistributionSchemeIds);
        return Task.CompletedTask;
    }

    public Task<TabulationEmailRunSummary> ProcessScheduledEmailsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(TabulationEmailRunSummary.Empty);
}
