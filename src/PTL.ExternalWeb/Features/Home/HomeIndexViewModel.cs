namespace PTL.ExternalWeb.Features.Home;

/// <summary>View model for the external landing page, showing the signed-in CIDM user's display
/// name, which roles resolved to an existing PT-LIMS record, and the shared admin-authored
/// "Important Message" banner (null/empty when no message is currently published). The two
/// CanCreate* flags hide an Orders card once an order for that year has been submitted (legacy
/// CanCreateNewCurrentOrder / CanCreateNewNextOrder).</summary>
public sealed record HomeIndexViewModel(
    string DisplayName,
    IReadOnlyList<string> Roles,
    string? ImportantMessageHtml = null,
    bool CanCreateCurrentYearOrder = true,
    bool CanCreateNextYearOrder = true)
{
    public bool IsParticipant => Roles.Contains(Account.ExternalRoleNames.Participant, StringComparer.OrdinalIgnoreCase);

    public bool IsViewer => Roles.Contains(Account.ExternalRoleNames.Viewer, StringComparer.OrdinalIgnoreCase);

    public bool IsTestConsultant => Roles.Contains(Account.ExternalRoleNames.TestConsultant, StringComparer.OrdinalIgnoreCase);
}
