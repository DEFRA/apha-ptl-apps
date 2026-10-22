using Microsoft.Extensions.Logging;
using PTL.Core.Contract;
using PTL.Core.Customer;

namespace PTL.Core.Participant;

public sealed class ParticipantSchemeService(IParticipantSchemeRepository participantSchemeRepository, IContractRepository contractRepository, ICustomerRepository customerRepository, IContractPostageService contractPostageService, ILogger<ParticipantSchemeService> logger) : IParticipantSchemeService
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

    // Legacy NewParticipantScheme(contractId, yearId, customer.PaymentNonUK): a non-UK customer's items
    // use the Overseas posting dates when deciding which months are already posted.
    public async Task<ParticipantSchemeContext> GetParticipantSchemeContextAsync(Guid contractId, Guid schemeId, CancellationToken cancellationToken = default)
    {
        if (contractId == Guid.Empty || schemeId == Guid.Empty)
        {
            return new ParticipantSchemeContext();
        }

        var contract = await contractRepository.GetByIdAsync(contractId, cancellationToken);
        var customer = contract is null ? null : await customerRepository.GetByIdAsync(contract.CustomerId, cancellationToken);
        return await participantSchemeRepository.GetSchemeContextAsync(schemeId, contractId, customer?.PaymentNonUK ?? false, cancellationToken);
    }

    public async Task<ParticipantSchemeRecord> CreateParticipantSchemeAsync(ParticipantSchemeRecord record, CancellationToken cancellationToken = default)
    {
        record.ParticipantSchemeId = Guid.NewGuid();
        record.IsRemoved = false;

        await EnsureContractIsEditableAsync(record.ContractId, cancellationToken);

        // A new item has nothing stored yet, so a month that is already posted can only be set through Override.
        var context = await GetParticipantSchemeContextAsync(record.ContractId, record.SchemeId, cancellationToken);
        ParticipantSchemeMonths.SetCanEdit(record, context.CanEdit);
        var (months, overrides) = ParticipantSchemeMonths.ApplyLocks(
            new bool[ParticipantSchemeMonths.Count],
            new bool[ParticipantSchemeMonths.Count],
            context.CanEdit,
            ParticipantSchemeMonths.Distribution(record),
            ParticipantSchemeMonths.Override(record));
        ParticipantSchemeMonths.SetDistribution(record, months);
        ParticipantSchemeMonths.SetOverride(record, overrides);

        await ValidateAsync(record, cancellationToken);

        var created = await participantSchemeRepository.CreateAsync(record, cancellationToken);
        await contractPostageService.RecalculateAsync(created.ContractId, cancellationToken);
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

        // Legacy ParticipantScheme month setters ignore a change to a month whose distribution is
        // already posted unless it is overridden - disabled checkboxes post nothing, so without this a
        // locked month would be saved as unticked. Sticky overrides: once true, never cleared.
        var (months, overrides) = ParticipantSchemeMonths.ApplyLocks(
            ParticipantSchemeMonths.Distribution(existing),
            ParticipantSchemeMonths.Override(existing),
            ParticipantSchemeMonths.CanEdit(existing),
            ParticipantSchemeMonths.Distribution(updatedFields),
            ParticipantSchemeMonths.Override(updatedFields));
        ParticipantSchemeMonths.SetDistribution(updatedFields, months);
        ParticipantSchemeMonths.SetOverride(updatedFields, overrides);

        await ValidateAsync(updatedFields, cancellationToken);

        await participantSchemeRepository.UpdateAsync(updatedFields, cancellationToken);
        await contractPostageService.RecalculateAsync(existing.ContractId, cancellationToken);
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
            .Any(p => !p.IsRemoved && p.ParticipantId == record.ParticipantId && p.ParticipantSchemeId != record.ParticipantSchemeId) ?? false;
        if (alreadyOnScheme)
        {
            result.Errors.Add(new ParticipantSchemeValidationError(
                "ParticipantId",
                "This participant is already on this scheme, on this contract. You should edit the existing contract item."));
        }

        // Legacy only reaches the cross-contract check when the month-required and same-contract rules passed.
        var noMonthsSelected = result.Errors.Any(e => e.Field == ParticipantSchemeValidator.NoMonthsField);
        if (!noMonthsSelected && !alreadyOnScheme && record.ParticipantId != Guid.Empty && record.SchemeId != Guid.Empty)
        {
            await AddCrossContractMonthErrorsAsync(record, result, cancellationToken);
        }

        if (!result.IsValid)
        {
            LogValidationFailedMessage(logger, record.ParticipantSchemeId, string.Join("; ", result.Errors.Select(e => e.Message)), null);
            throw new ParticipantSchemeValidationException(result.Errors);
        }
    }

    // Matches legacy AreDistributionMonthsValid(): a participant cannot be on the same scheme, in a
    // different contract, for a month this item also selects (removed items are ignored).
    private async Task AddCrossContractMonthErrorsAsync(ParticipantSchemeRecord record, ParticipantSchemeValidationResult result, CancellationToken cancellationToken)
    {
        var selected = ParticipantSchemeMonths.Distribution(record);
        var others = await participantSchemeRepository.GetByParticipantAndSchemeAsync(record.ParticipantId, record.SchemeId, cancellationToken);
        for (var i = 0; i < ParticipantSchemeMonths.Count; i++)
        {
            if (selected[i] && others.Any(o => !o.IsRemoved && o.ParticipantSchemeId != record.ParticipantSchemeId && o.Months[i]))
            {
                result.Errors.Add(new ParticipantSchemeValidationError(
                    ParticipantSchemeMonths.DistributionFields[i],
                    "This participant is already on this scheme, for this month"));
            }
        }
    }
}
