namespace PTL.Core.Participant;

/// <summary>
/// Legacy <c>UserPermissions.GetPermissions</c>: a participant counts as acting for the customer only when it is the
/// customer's single active participant. Ordering online and editing the customer and participant details both depend on it.
/// </summary>
internal static class SoleActiveParticipant
{
    public static async Task<bool> IsAsync(IParticipantRepository participantRepository, Participant participant, CancellationToken cancellationToken)
    {
        if (!participant.IsActive)
        {
            return false;
        }

        var customerParticipants = await participantRepository.GetSummariesAsync(participant.CustomerId, includeInactive: true, cancellationToken);
        var active = customerParticipants.Where(p => p.IsActive).ToList();
        return active.Count == 1 && active[0].ParticipantId == participant.ParticipantId;
    }
}
