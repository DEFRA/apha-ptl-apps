namespace PTL.Contracts.GroupAddress;

public sealed record GroupAddressSaveRequest(
    string Identifier,
    string Address1,
    string Address2,
    string Address3,
    string Address4,
    string Address5,
    Guid CountryId,
    string Telephone,
    string PackingInstructions);
