using PTL.ExternalWeb.Features.Account;

namespace PTL.ExternalWeb.Navigation;

// Fixed configuration for the external-facing navigation: top-level section names drive the
// govuk-service-navigation bar (_TopNavigation.cshtml), and each section's Children drive the
// Home page's quick-link cards (_QuickLinkCard.cshtml) - a single source of truth for both,
// reproducing legacy ProficiencyTestingExternalWeb/NavigationBar.ascx's role-gated menu groups
// (Schemes/Orders/Results/Reports/Comments) - see
// docs/Parity-Analysis/01-Module-Migration-Inventory.md "Navigation & Shared Chrome". No pages
// exist yet for any of these entries - they are Disabled() placeholders until each screen lands
// (docs/Parity-Analysis/08-Migration-Parity-Tracker.md).
public static class NavigationProvider
{
    // Placeholder for a menu entry whose page hasn't been migrated yet.
    private static NavigationItem Disabled(string text) => new() { Text = text, IsEnabled = false };

    public static IReadOnlyList<NavigationItem> Build() =>
    [
        new NavigationItem
        {
            Text = "Schemes",
            RequiredRole = ExternalRoleNames.Participant,
            Children = [Disabled("View schemes")]
        },
        new NavigationItem
        {
            Text = "Orders",
            RequiredRole = ExternalRoleNames.Participant,
            Children = [Disabled("Create order")]
        },
        new NavigationItem
        {
            Text = "Results",
            RequiredRole = ExternalRoleNames.Participant,
            Children = [Disabled("Enter results"), Disabled("View results")]
        },
        new NavigationItem
        {
            Text = "Reports",
            RequiredRole = ExternalRoleNames.Viewer,
            Children = [Disabled("View tabulations")]
        },
        new NavigationItem
        {
            Text = "Comments",
            RequiredRole = ExternalRoleNames.TestConsultant,
            Children = [Disabled("Enter comments")]
        },
        new NavigationItem
        {
            Text = "My account",
            RequiredRole = ExternalRoleNames.Participant,
            Children = [Disabled("Edit customer details"), Disabled("Edit participant details")]
        }
    ];
}
