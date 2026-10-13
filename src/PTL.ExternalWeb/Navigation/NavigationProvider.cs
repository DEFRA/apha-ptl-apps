using PTL.ExternalWeb.Features.Account;

namespace PTL.ExternalWeb.Navigation;

// Fixed configuration for the external-facing navigation: top-level section names drive the
// side navigation (_SideNavigation.cshtml - the "header menu" from legacy's top horizontal nav
// has no literal header-bar equivalent here; it was deliberately moved into this side nav
// alongside PTL.InternalWeb's existing pattern), and each section's Children drive the Home
// page's quick-link cards (_QuickLinkCard.cshtml) - a single source of truth for both,
// reproducing legacy ProficiencyTestingExternalWeb/NavigationBar.ascx's role-gated menu groups
// (Schemes/Orders/Results) - see docs/Parity-Analysis/01-Module-Migration-Inventory.md
// "Navigation & Shared Chrome". Results' four children (Enter results/View results/View
// reports/Enter comments) each carry their own RequiredRole, matching legacy's single "Results"
// dropdown (NavigationBar.ascx.vb) where Viewer- and Test Consultant-only items sit alongside the
// Participant ones in one group, rather than being split into separate top-level sections. No
// screen behind any of these entries has its own migration slice yet, so every one routes to the
// shared ComingSoonController rather than being a dead/disabled link, matching legacy's
// always-clickable navigation (docs/Parity-Analysis/08-Migration-Parity-Tracker.md).
public static class NavigationProvider
{
    // Real, clickable link for a menu entry whose own page hasn't been migrated yet. requiredRole
    // is null when the item inherits visibility purely from its parent section.
    // description ports the matching "*Blurb" resource from legacy's Home.aspx.resx.
    private static NavigationItem Stub(string text, string description, string? requiredRole = null) =>
        new() { Text = text, ControllerName = "ComingSoon", ActionName = "Index", Description = description, RequiredRole = requiredRole };

    public static IReadOnlyList<NavigationItem> Build() =>
    [
        new NavigationItem
        {
            Text = "Schemes",
            RequiredRole = ExternalRoleNames.Participant,
            Children = [Stub("View schemes", "List of current proficiency tests available")]
        },
        new NavigationItem
        {
            Text = "Orders",
            RequiredRole = ExternalRoleNames.Participant,
            // Legacy's PnlOrdersSection.Visible = IsParticipant AndAlso CanOrderOnline (Home.aspx.vb) -
            // being a Participant alone isn't enough; the linked Customer must also be order-eligible.
            RequireCanOrderOnline = true,
            Children = [Stub("Create order", "Order proficiency tests for the current or next year")]
        },
        new NavigationItem
        {
            // No RequiredRole here - visibility comes entirely from each child's own role below,
            // since Participant/Viewer/Test Consultant each only see their own subset.
            Text = "Results",
            Children =
            [
                Stub("Enter results", "Submit results for a current distribution", ExternalRoleNames.Participant),
                Stub("View results", "View your results from a previous proficiency test", ExternalRoleNames.Participant),
                Stub("View reports", "View completed proficiency test reports, which include all participant results", ExternalRoleNames.Viewer),
                Stub("Enter comments", "Provide or view proficiency test comments", ExternalRoleNames.TestConsultant)
            ]
        },
        new NavigationItem
        {
            Text = "My account",
            RequiredRole = ExternalRoleNames.Participant,
            Children =
            [
                Stub("Edit customer details", "Submit an update to your customer details"),
                Stub("Edit participant details", "Submit an update to participating laboratory details")
            ]
        }
    ];
}
