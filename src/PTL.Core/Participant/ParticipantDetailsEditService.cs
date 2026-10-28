namespace PTL.Core.Participant;

// Legacy EditParticipantDetails.aspx.vb's getPendingParticipantUpdate/ButtonUpdateDetails_Click,
// reproduced as a stateless service: resolves the participant's own record server-side (never
// trusts a client-supplied ParticipantId - see docs/ADR/0006), then mirrors the exact
// pending-update-or-live-participant precedence legacy uses to populate the form.
public sealed class ParticipantDetailsEditService(
    IParticipantRepository participantRepository,
    IPendingParticipantUpdateRepository pendingParticipantUpdateRepository) : IParticipantDetailsEditService
{
    public async Task<ParticipantDetailsEditState?> GetAsync(Guid participantId, CancellationToken cancellationToken = default)
    {
        var participant = await participantRepository.GetByIdAsync(participantId, cancellationToken);
        if (participant is null)
        {
            return null;
        }

        var pending = await pendingParticipantUpdateRepository.GetByParticipantIdAsync(participantId, cancellationToken);
        if (pending is not null)
        {
            return new ParticipantDetailsEditState(participant.LabCode, HasOutstandingUpdate: true, pending);
        }

        var fromParticipant = new PendingParticipantUpdate
        {
            ParticipantId = participant.ParticipantId,
            CustomerId = participant.CustomerId,
            LabCode = participant.LabCode,
            ContactName = participant.ContactName,
            Organisation = participant.Organisation,
            Address1 = participant.Address1,
            Address2 = participant.Address2,
            Address3 = participant.Address3,
            Address4 = participant.Address4,
            Address5 = participant.Address5,
            CountryId = participant.CountryId,
            Telephone = participant.Telephone,
            Fax = participant.Fax,
            Email = participant.Email,
            Email2 = participant.Email2
        };

        return new ParticipantDetailsEditState(participant.LabCode, HasOutstandingUpdate: false, fromParticipant);
    }

    public async Task SubmitAsync(Guid participantId, PendingParticipantUpdate proposed, CancellationToken cancellationToken = default)
    {
        var participant = await participantRepository.GetByIdAsync(participantId, cancellationToken)
            ?? throw new InvalidOperationException($"Participant {participantId} not found.");

        var existingPending = await pendingParticipantUpdateRepository.GetByParticipantIdAsync(participantId, cancellationToken);
        if (existingPending is not null)
        {
            throw new PendingParticipantUpdateAlreadySubmittedException();
        }

        // Fax isn't on the edit form (legacy parity) - carry the live value across so the admin
        // review screen's diff doesn't flag it as changed (legacy ConvertCustomerToPendingParticipantUpdate).
        proposed.Fax = participant.Fax;

        var validation = PendingParticipantUpdateValidator.Validate(proposed);
        if (!validation.IsValid)
        {
            throw new ParticipantValidationException(validation.Errors);
        }

        proposed.ParticipantId = participant.ParticipantId;
        proposed.CustomerId = participant.CustomerId;
        proposed.LabCode = participant.LabCode;
        proposed.PendingParticipantUpdateId = Guid.Empty;
        proposed.IsSubmitted = true;
        proposed.IsDeleted = false;

        await pendingParticipantUpdateRepository.UpsertAsync(proposed, cancellationToken);
    }
}
