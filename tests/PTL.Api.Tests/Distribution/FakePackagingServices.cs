using Microsoft.Extensions.Logging;
using PTL.Core.Contract.Document;
using PTL.Core.Distribution;
using PTL.Core.Notifications;
using PTL.Core.Participant;
using PTL.Contracts.Contract;
using ParticipantEntity = PTL.Core.Participant.Participant;

namespace PTL.Api.Tests.Distribution;

internal sealed class FakePackagingService : IPackagingService
{
    public PackagingSheet? Sheet { get; set; }

    public PackagingSaveOutcome SaveOutcome { get; set; } = new(true);

    public List<(int YearId, int MonthId, PackagingMode Mode, int Week)> SheetRequests { get; } = [];

    public List<IReadOnlyList<PackagingStageUpdate>> SavedUpdates { get; } = [];

    public Task<PackagingSheet?> GetPackagingAsync(int yearId, int monthId, PackagingMode mode, int week, CancellationToken cancellationToken = default)
    {
        SheetRequests.Add((yearId, monthId, mode, week));
        return Task.FromResult(Sheet);
    }

    public Task<PackagingSaveOutcome> SavePackagingAsync(
        int yearId, int monthId, IReadOnlyList<PackagingStageUpdate> updates, CancellationToken cancellationToken = default)
    {
        SavedUpdates.Add(updates);
        return Task.FromResult(SaveOutcome);
    }
}

internal sealed class FakePackagingOutputService : IPackagingOutputService
{
    public IReadOnlyList<PackagingLabel>? Labels { get; set; }

    public PackagingCustomsOutcome Customs { get; set; } = new(false, "Not configured", string.Empty, []);

    public List<(PackagingMode Mode, int Week, IReadOnlyCollection<string> Keys)> LabelRequests { get; } = [];

    public List<(PackagingMode Mode, int Week, IReadOnlyCollection<string> Keys)> CustomsRequests { get; } = [];

    public Task<IReadOnlyList<PackagingLabel>?> GetLabelsAsync(
        int yearId, int monthId, PackagingMode mode, int week, IReadOnlyCollection<string> selectedKeys, CancellationToken cancellationToken = default)
    {
        LabelRequests.Add((mode, week, selectedKeys));
        return Task.FromResult(Labels);
    }

    public Task<PackagingCustomsOutcome> GetCustomsDocumentAsync(
        int yearId, int monthId, PackagingMode mode, int week, IReadOnlyCollection<string> selectedKeys, CancellationToken cancellationToken = default)
    {
        CustomsRequests.Add((mode, week, selectedKeys));
        return Task.FromResult(Customs);
    }
}

internal sealed class FakePostedNotifier : IDistributionPostedNotifier
{
    public List<IReadOnlyList<PostedParticipant>> BuildRequests { get; } = [];

    public List<IReadOnlyList<DistributionPostedEmailEntity>> Dispatched { get; } = [];

    // Emails BuildEmailsAsync hands back for whatever was posted.
    public List<DistributionPostedEmailEntity> EmailsToReturn { get; } = [];

    // Emails DispatchAsync reports as not sent.
    public List<DistributionPostedEmailEntity> FailedEmails { get; } = [];

    // Mirrors the real notifier's contract (nothing posted -> nothing to send) unless told otherwise.
    public bool ReturnEmailsEvenWhenNothingPosted { get; set; }

    public Task<IReadOnlyList<DistributionPostedEmailEntity>> BuildEmailsAsync(
        IReadOnlyList<PostedParticipant> posted, CancellationToken cancellationToken = default)
    {
        BuildRequests.Add(posted);
        return Task.FromResult<IReadOnlyList<DistributionPostedEmailEntity>>(
            posted.Count == 0 && !ReturnEmailsEvenWhenNothingPosted ? [] : [.. EmailsToReturn]);
    }

    public Task<IReadOnlyList<DistributionPostedEmailEntity>> DispatchAsync(
        IReadOnlyList<DistributionPostedEmailEntity> emails, CancellationToken cancellationToken = default)
    {
        Dispatched.Add(emails);
        return Task.FromResult<IReadOnlyList<DistributionPostedEmailEntity>>([.. FailedEmails]);
    }
}

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

internal sealed class FakeTemplateMergeService : ITemplateMergeService
{
    public byte[] Result { get; set; } = [1, 2, 3];

    public byte[]? MergedTemplate { get; private set; }

    public IReadOnlyList<ContractDocumentMergeData>? MergedDocuments { get; private set; }

    public byte[] MergeTemplateContentMany(byte[] templateContent, IReadOnlyList<ContractDocumentMergeData> documents, CancellationToken cancellationToken = default)
    {
        MergedTemplate = templateContent;
        MergedDocuments = documents;
        return Result;
    }

    public byte[] MergeTemplate(
        string templatePath,
        IReadOnlyDictionary<string, string> mergeValues,
        IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>>? regions = null,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public byte[] MergeTemplateMany(string templatePath, IReadOnlyList<ContractDocumentMergeData> documents, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public byte[] MergeTemplateContent(
        byte[] templateContent,
        IReadOnlyDictionary<string, string> mergeValues,
        IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>>? regions = null,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();
}

// Returns canned participants by id; an id with no entry is "participant not found".
internal sealed class FakePostedParticipantService : IParticipantService
{
    public Dictionary<Guid, ParticipantEntity> Participants { get; } = [];

    public List<Guid> Requested { get; } = [];

    public Task<ParticipantEntity?> GetParticipantAsync(Guid participantId, CancellationToken cancellationToken = default)
    {
        Requested.Add(participantId);
        return Task.FromResult(Participants.GetValueOrDefault(participantId));
    }

    public Task<IReadOnlyList<ParticipantSummaryEntity>> GetParticipantsAsync(Guid customerId, bool includeInactive = false, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<ParticipantSearchResult> SearchParticipantsAsync(Guid customerId, string? searchTerm, bool includeInactive, int page, int pageSize, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<ParticipantEntity> CreateParticipantAsync(ParticipantEntity participant, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<ParticipantEntity?> UpdateParticipantAsync(Guid participantId, ParticipantEntity updatedFields, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<ParticipantEntity?> DeactivateParticipantAsync(Guid participantId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<ParticipantEntity?> ReactivateParticipantAsync(Guid participantId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<PendingParticipantUpdateSummaryEntity>> GetPendingParticipantUpdatesAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<(ParticipantEntity Current, PendingParticipantUpdate Pending)?> GetPendingParticipantUpdateAsync(Guid participantId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<bool> ApprovePendingParticipantUpdateAsync(Guid participantId, PendingParticipantUpdate? editedFields = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<bool> DeclinePendingParticipantUpdateAsync(Guid participantId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<ParticipantViewerAssignment?> GetParticipantViewersAsync(Guid participantId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<bool> UpdateParticipantViewersAsync(Guid participantId, IReadOnlyList<Guid> targetViewerIds, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}

internal sealed class CapturingLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, Exception? Exception)> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        Entries.Add((logLevel, exception));
}

// Notify client that records the arguments and can be told to throw.
internal sealed class RecordingNotifyClient : INotifyClient
{
    public List<(string TemplateId, string Email, IReadOnlyDictionary<string, string>? Personalisation, string? Reference)> Sent { get; } = [];

    public Func<string, Exception?>? FailFor { get; set; }

    public Task SendEmailAsync(
        string templateId,
        string emailAddress,
        IReadOnlyDictionary<string, string>? personalisation = null,
        string? reference = null,
        CancellationToken cancellationToken = default)
    {
        if (FailFor?.Invoke(emailAddress) is { } failure)
        {
            throw failure;
        }

        Sent.Add((templateId, emailAddress, personalisation, reference));
        return Task.CompletedTask;
    }
}
