namespace PTL.ExternalWeb.Features.Home;

/// <summary>View model for the external landing page, showing the signed-in CIDM user's display
/// name, which roles resolved to an existing PT-LIMS record, and the shared admin-authored
/// "Important Message" banner (null/empty when no message is currently published). The two
/// CanCreate* flags disable an Orders card once an order for that year has been submitted (legacy
/// CanCreateNewCurrentOrder / CanCreateNewNextOrder); CurrentYear/NextYear are the year labels its message names.</summary>
public sealed record HomeIndexViewModel(
    string DisplayName,
    IReadOnlyList<string> Roles,
    string? ImportantMessageHtml = null,
    bool CanCreateCurrentYearOrder = true,
    bool CanCreateNextYearOrder = true,
    string CurrentYear = "",
    string NextYear = "")
{
    // Legacy replaced the card blurb with "Order has been submitted for {year}"; null means the card stays as normal.
    public string? SubmittedOrderMessage(string actionName) => actionName switch
    {
        "CurrentYear" when !CanCreateCurrentYearOrder => Describe(CurrentYear, "the current year"),
        "NextYear" when !CanCreateNextYearOrder => Describe(NextYear, "the next year"),
        _ => null
    };

    private static string Describe(string year, string fallback) => $"Order has been submitted for {(string.IsNullOrWhiteSpace(year) ? fallback : year)}.";

    public bool IsParticipant => Roles.Contains(Account.ExternalRoleNames.Participant, StringComparer.OrdinalIgnoreCase);

    public bool IsViewer => Roles.Contains(Account.ExternalRoleNames.Viewer, StringComparer.OrdinalIgnoreCase);

    public bool IsTestConsultant => Roles.Contains(Account.ExternalRoleNames.TestConsultant, StringComparer.OrdinalIgnoreCase);
}
