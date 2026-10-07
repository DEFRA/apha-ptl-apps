namespace PTL.ExternalWeb.Features.Home;

/// <summary>View model for the external landing page, showing the signed-in CIDM user's display
/// name and which roles resolved to an existing PT-LIMS record.</summary>
public sealed record HomeIndexViewModel(string DisplayName, IReadOnlyList<string> Roles);
