namespace PTL.Contracts.Participant;

public sealed record UpdateParticipantRequest(
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
