using PTL.ExternalWeb.Features.Account;

namespace PTL.ExternalWeb.Navigation;

// Fixed configuration for the external-facing navigation: top-level section names drive the
// govuk-service-navigation bar (_TopNavigation.cshtml), and each section's Children drive the
// Home page's quick-link cards (_QuickLinkCard.cshtml) - a single source of truth for both,
// reproducing legacy ProficiencyTestingExternalWeb/NavigationBar.ascx's role-gated menu groups
// (Schemes/Orders/Results/Reports/Comments) - see
// docs/Parity-Analysis/01-Module-Migration-Inventory.md "Navigation & Shared Chrome". No screen
// behind any of these entries has its own migration slice yet, so every one routes to the shared
// ComingSoonController rather than being a dead/disabled link, matching legacy's always-clickable
// navigation (docs/Parity-Analysis/08-Migration-Parity-Tracker.md).
public static class NavigationProvider
{
    // Real, clickable link for a menu entry whose own page hasn't been migrated yet.
    private static NavigationItem Stub(string text) => new() { Text = text, ControllerName = "ComingSoon", ActionName = "Index" };

    public static IReadOnlyList<NavigationItem> Build() =>
    [
        new NavigationItem
        {
            Text = "Schemes",
            RequiredRole = ExternalRoleNames.Participant,
            Children = [Stub("View schemes")]
        },
        new NavigationItem
        {
            Text = "Orders",
            RequiredRole = ExternalRoleNames.Participant,
            Children = [Stub("Create order")]
        },
        new NavigationItem
        {
            Text = "Results",
            RequiredRole = ExternalRoleNames.Participant,
            Children = [Stub("Enter results"), Stub("View results")]
        },
        new NavigationItem
        {
            Text = "Reports",
            RequiredRole = ExternalRoleNames.Viewer,
            Children = [Stub("View tabulations")]
        },
        new NavigationItem
        {
            Text = "Comments",
            RequiredRole = ExternalRoleNames.TestConsultant,
            Children = [Stub("Enter comments")]
        },
        new NavigationItem
        {
            Text = "My account",
            RequiredRole = ExternalRoleNames.Participant,
            Children = [Stub("Edit customer details"), Stub("Edit participant details")]
        }
    ];
}
