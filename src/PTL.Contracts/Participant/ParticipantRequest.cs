namespace PTL.Contracts.Participant;

// Shared by POST /api/participants (create) and PUT /api/participants/{id} (update). The editable
// field set matches the stored Participant entity and is identical for both operations.
public sealed record ParticipantRequest(
    Guid CustomerId,
    Guid SsoId,
    string LabCode,
    string LabName,
    Guid LabTypeId,
    string ContactName,
    string Organisation,
    string Address1,
    string Address2,
    string Address3,
    string Address4,
    string Address5,
    Guid CountryId,
    string Telephone,
    string Fax,
    string Email,
    string Email2,
    string Comments,
    bool IsActive);
