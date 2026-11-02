namespace PTL.ExternalWeb.ViewSupport;

// Parameter object for Views/Shared/_DistributionReferenceSchemeSearchFields.cshtml - the Month +
// Distribution Reference + Scheme Code + Scheme Name filter fields shared by Distribution/Previous
// and Tabulation/Index (their Year field differs - free text vs a dropdown - so that stays with
// each caller).
public sealed record DistributionReferenceSchemeSearchFields(int? Month, string? DistributionReference, string? SchemeCode, string? SchemeName);
