using PTL.Core.Notifications;

namespace PTL.Api.Tests.Invoice;

// In-memory INotifyClient test double - shared GOV.UK Notify abstraction, faked here purely for
// InvoiceService tests (no real HTTP call to GOV.UK Notify).
internal sealed class FakeNotifyClient : INotifyClient
{
    public List<string> SentTo { get; } = [];
    public IReadOnlyDictionary<string, string>? LastPersonalisation { get; private set; }
    public string? LastFilePersonalisationKey { get; private set; }
    public byte[]? LastFileContent { get; private set; }
    public string? LastFilename { get; private set; }
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

        LastPersonalisation = personalisation;
        SentTo.Add(emailAddress);
        return Task.CompletedTask;
    }

    public Task SendEmailWithFileAsync(NotifyFileEmailRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.EmailAddress == ThrowForEmailAddress)
        {
            throw new InvalidOperationException("Simulated GOV.UK Notify failure.");
        }

        LastPersonalisation = request.Personalisation;
        LastFilePersonalisationKey = request.FilePersonalisationKey;
        LastFileContent = request.FileContent;
        LastFilename = request.Filename;
        SentTo.Add(request.EmailAddress);
        return Task.CompletedTask;
    }
}
