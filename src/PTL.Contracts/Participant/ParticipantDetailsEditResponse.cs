namespace PTL.Contracts.Participant;

// Participant-facing read model for GET /api/participant-details-edit/{participantId} - legacy
// EditParticipantDetails.aspx. Values come from the outstanding PendingParticipantUpdate when one
// exists (HasOutstandingUpdate = true), otherwise from the live Participant record.
public sealed record ParticipantDetailsEditResponse(
    string LabCode,
    bool HasOutstandingUpdate,
    string ContactName,
    string Organisation,
    string Address1,
    string Address2,
    string Address3,
    string Address4,
    string Address5,
    Guid CountryId,
    string Telephone,
    string Email,
    string Email2);

public sealed record ParticipantDetailsEditSaveRequest(
    string ContactName,
    string Organisation,
    string Address1,
    string Address2,
    string Address3,
    string Address4,
    string Address5,
    Guid CountryId,
    string Telephone,
    string Email,
    string Email2);

public sealed record ParticipantDetailsEditSaveResult(bool Success, IReadOnlyDictionary<string, string[]> Errors);
