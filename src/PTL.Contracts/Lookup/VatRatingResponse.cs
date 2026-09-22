namespace PTL.Contracts.Lookup;

// Public API contract for GET /api/lookups/vat-ratings; matches the legacy VatRatingCollection /
// spgaVatRating (not surfaced as a dropdown anywhere in the legacy Web Forms UI, same situation as
// CustomerType - see CustomerTypeResponse.cs).
public sealed record VatRatingResponse(Guid VatRatingId, string VatRating);
