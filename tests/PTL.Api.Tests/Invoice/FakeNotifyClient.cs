using PTL.Core.Notifications;

namespace PTL.Api.Tests.Invoice;

// In-memory INotifyClient test double - shared GOV.UK Notify abstraction, faked here purely for
// InvoiceService tests (no real HTTP call to GOV.UK Notify).
internal sealed class FakeNotifyClient : INotifyClient
{
    public List<string> SentTo { get; } = [];
    public string? ThrowForEmailAddress { get; set; }

    public Task SendEmailAsync(
        string templateId,
        string emailAddress,
        IReadOnlyDictionary<string, string>? personalisation = null,
        string? reference = null,
        CancellationToken cancellationToken = default)
    {
        if (emailAddress == ThrowForEmailAddress)
        {
            throw new InvalidOperationException("Simulated GOV.UK Notify failure.");
        }

        SentTo.Add(emailAddress);
        return Task.CompletedTask;
    }
}
