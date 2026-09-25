using Microsoft.Extensions.Logging;
using PTL.Core.Contract;

namespace PTL.Core.Participant;

public sealed class ParticipantSchemeService(IParticipantSchemeRepository participantSchemeRepository, IContractRepository contractRepository, ILogger<ParticipantSchemeService> logger) : IParticipantSchemeService
{
    private static readonly Action<ILogger, Guid, Exception?> LogNotFoundMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Information,
            new EventId(1, nameof(LogNotFoundMessage)),
            "Participant scheme {ParticipantSchemeId} not found");

    private static readonly Action<ILogger, Guid, Exception?> LogCreatedMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Information,
            new EventId(2, nameof(LogCreatedMessage)),
            "Created participant scheme {ParticipantSchemeId}");

    private static readonly Action<ILogger, Guid, Exception?> LogUpdatedMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Information,
            new EventId(3, nameof(LogUpdatedMessage)),
            "Updated participant scheme {ParticipantSchemeId}");

    private static readonly Action<ILogger, Guid, Exception?> LogDeletedMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Information,
            new EventId(4, nameof(LogDeletedMessage)),
            "Removed participant scheme {ParticipantSchemeId}");

    private static readonly Action<ILogger, Guid, string, Exception?> LogValidationFailedMessage =
        LoggerMessage.Define<Guid, string>(
            LogLevel.Warning,
            new EventId(5, nameof(LogValidationFailedMessage)),
            "Participant scheme validation failed for {ParticipantSchemeId}: {Errors}");

    public Task<ParticipantSchemeRecord?> GetParticipantSchemeAsync(Guid participantSchemeId, CancellationToken cancellationToken = default) =>
        participantSchemeRepository.GetByIdAsync(participantSchemeId, cancellationToken);

    public async Task<ParticipantSchemeRecord> CreateParticipantSchemeAsync(ParticipantSchemeRecord record, CancellationToken cancellationToken = default)
    {
        record.ParticipantSchemeId = Guid.NewGuid();
        record.IsRemoved = false;

        await EnsureContractIsEditableAsync(record.ContractId, cancellationToken);
        await ValidateAsync(record, cancellationToken);

        var created = await participantSchemeRepository.CreateAsync(record, cancellationToken);
        LogCreatedMessage(logger, created.ParticipantSchemeId, null);
        return created;
    }

    public async Task<ParticipantSchemeRecord?> UpdateParticipantSchemeAsync(Guid participantSchemeId, ParticipantSchemeRecord updatedFields, CancellationToken cancellationToken = default)
    {
        var existing = await participantSchemeRepository.GetByIdAsync(participantSchemeId, cancellationToken);
        if (existing is null)
        {
            LogNotFoundMessage(logger, participantSchemeId, null);
            return null;
        }

        await EnsureContractIsEditableAsync(existing.ContractId, cancellationToken);

        updatedFields.ParticipantSchemeId = existing.ParticipantSchemeId;
        updatedFields.ContractId = existing.ContractId;
        updatedFields.ParticipantId = existing.ParticipantId;
        updatedFields.SchemeId = existing.SchemeId;
        updatedFields.IsRemoved = existing.IsRemoved;

        // Sticky override flags - matches legacy LoadObjectFromForm's
        // `(monthChecked And overrideMode) Or existingIsOverrideX`: once true for a month, it is
        // never cleared back to false by a later save.
        updatedFields.IsOverrideJan |= existing.IsOverrideJan;
        updatedFields.IsOverrideFeb |= existing.IsOverrideFeb;
        updatedFields.IsOverrideMar |= existing.IsOverrideMar;
        updatedFields.IsOverrideApr |= existing.IsOverrideApr;
        updatedFields.IsOverrideMay |= existing.IsOverrideMay;
        updatedFields.IsOverrideJun |= existing.IsOverrideJun;
        updatedFields.IsOverrideJul |= existing.IsOverrideJul;
        updatedFields.IsOverrideAug |= existing.IsOverrideAug;
        updatedFields.IsOverrideSep |= existing.IsOverrideSep;
        updatedFields.IsOverrideOct |= existing.IsOverrideOct;
        updatedFields.IsOverrideNov |= existing.IsOverrideNov;
        updatedFields.IsOverrideDec |= existing.IsOverrideDec;

        await ValidateAsync(updatedFields, cancellationToken);

        await participantSchemeRepository.UpdateAsync(updatedFields, cancellationToken);
        LogUpdatedMessage(logger, participantSchemeId, null);
        return await participantSchemeRepository.GetByIdAsync(participantSchemeId, cancellationToken);
    }

    public async Task<bool> DeleteParticipantSchemeAsync(Guid participantSchemeId, CancellationToken cancellationToken = default)
    {
        var existing = await participantSchemeRepository.GetByIdAsync(participantSchemeId, cancellationToken);
        if (existing is null)
        {
            LogNotFoundMessage(logger, participantSchemeId, null);
            return false;
        }

        await EnsureContractIsEditableAsync(existing.ContractId, cancellationToken);

        existing.IsRemoved = true;
        var removed = await participantSchemeRepository.UpdateAsync(existing, cancellationToken);
        if (removed)
        {
            LogDeletedMessage(logger, participantSchemeId, null);
        }

        return removed;
    }

    private async Task EnsureContractIsEditableAsync(Guid contractId, CancellationToken cancellationToken)
    {
        var contract = await contractRepository.GetByIdAsync(contractId, cancellationToken);
        if (contract is { IsReadOnly: true })
        {
            // Mirrors legacy ParticipantScheme.aspx.vb hiding every editable control when
            // mContract.IsReadOnly (or the item IsRemoved) is true - made an explicit server-side rule.
            throw new ParticipantSchemeValidationException([new ParticipantSchemeValidationError(string.Empty, "This contract belongs to a closed year and cannot be edited.")]);
        }
    }

    private async Task ValidateAsync(ParticipantSchemeRecord record, CancellationToken cancellationToken)
    {
        var result = ParticipantSchemeValidator.Validate(record);

        // Matches legacy AreDistributionMonthsValid()'s
        // ValidatorParticipantAlreadyOnSchemeInThisContract check: a participant may only appear
        // once (non-removed) against a given scheme within the same contract - re-adding them
        // should edit the existing item instead.
        var contractItems = await contractRepository.GetContractItemsAsync(record.ContractId, cancellationToken);
        var alreadyOnScheme = contractItems?.Schemes
            .FirstOrDefault(s => s.SchemeId == record.SchemeId)?.Participants
            .Any(p => p.ParticipantId == record.ParticipantId && p.ParticipantSchemeId != record.ParticipantSchemeId) ?? false;
        if (alreadyOnScheme)
        {
            result.Errors.Add(new ParticipantSchemeValidationError(
                "ParticipantId",
                "This participant is already on this scheme, on this contract. You should edit the existing contract item."));
        }

        if (!result.IsValid)
        {
            LogValidationFailedMessage(logger, record.ParticipantSchemeId, string.Join("; ", result.Errors.Select(e => e.Message)), null);
            throw new ParticipantSchemeValidationException(result.Errors);
        }
    }
}
