namespace PTL.Core.Lookup;

// Keyless domain projection for the list returned by spgaVatRating.
public class VatRatingEntity
{
    public Guid VatRatingId { get; set; }
    public string VatRating { get; set; } = string.Empty;
}
