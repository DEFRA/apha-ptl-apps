namespace PTL.Contracts.Lookup;

// GET /api/lookups/group-addresses - matches legacy GroupAddressCollection.FetchGroupAddressCollection().
public sealed record GroupAddressResponse(Guid GroupAddressId, string Identifier, string Address1, Guid CountryId);
