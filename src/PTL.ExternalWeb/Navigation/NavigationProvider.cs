using PTL.ExternalWeb.Features.Account;

namespace PTL.ExternalWeb.Navigation;

// Fixed configuration for the external-facing navigation: top-level section names drive the
// header menu panel (_HeaderMenu.cshtml - the "header menu" from legacy's top horizontal nav),
// and each section's Children drive the Home page's quick-link cards (_QuickLinkCard.cshtml) - a
// single source of truth for both, reproducing legacy
// ProficiencyTestingExternalWeb/NavigationBar.ascx's role-gated menu groups
// (Schemes/Orders/Results) - see docs/Parity-Analysis/01-Module-Migration-Inventory.md
// "Navigation & Shared Chrome". Results' four children (Enter results/View results/View
// reports/Enter comments) each carry their own RequiredRole, matching legacy's single "Results"
// dropdown (NavigationBar.ascx.vb) where Viewer- and Test Consultant-only items sit alongside the
// Participant ones in one group, rather than being split into separate top-level sections. Every
// entry now routes to its own placeholder controller (each with its own [Authorize(Policy=...)])
// rather than the shared ComingSoonController - real functionality is added to each controller in
// turn per docs/Parity-Analysis/08-Migration-Parity-Tracker.md's sequencing.
public static class NavigationProvider
{
    // Every child below routes to its own controller's default action - named once here since
    // SonarCloud flags the repeated "Index" literal (S1192).
    private const string IndexAction = "Index";

    public static IReadOnlyList<NavigationItem> Build() =>
    [
        new NavigationItem
        {
            Text = "Schemes",
            RequiredRole = ExternalRoleNames.Participant,
            Children =
            [
                new NavigationItem
                {
                    Text = "View schemes",
                    ControllerName = "Scheme",
                    ActionName = IndexAction,
                    Description = "List of current proficiency tests available"
                }
            ]
        },
        new NavigationItem
        {
            Text = "Orders",
            RequiredRole = ExternalRoleNames.Participant,
            // Legacy's PnlOrdersSection.Visible = IsParticipant AndAlso CanOrderOnline (Home.aspx.vb) -
            // being a Participant alone isn't enough; the linked Customer must also be order-eligible.
            RequireCanOrderOnline = true,
            Children =
            [
                new NavigationItem
                {
                    Text = "Create order for current year",
                    ControllerName = "Order",
                    ActionName = "CurrentYear",
                    Description = "Order proficiency tests for the current year",
                    RequiredRole = ExternalRoleNames.Participant
                },
                new NavigationItem
                {
                    Text = "Create order for next year",
                    ControllerName = "Order",
                    ActionName = "NextYear",
                    Description = "Order proficiency tests for the next year",
                    RequiredRole = ExternalRoleNames.Participant
                }
            ]
        },
        new NavigationItem
        {
            // No RequiredRole here - visibility comes entirely from each child's own role below,
            // since Participant/Viewer/Test Consultant each only see their own subset.
            Text = "Results",
            Children =
            [
                new NavigationItem
                {
                    Text = "Enter results",
                    ControllerName = "Distribution",
                    ActionName = "Current",
                    Description = "Submit results for a current distribution",
                    RequiredRole = ExternalRoleNames.Participant
                },
                new NavigationItem
                {
                    Text = "View results",
                    ControllerName = "Distribution",
                    ActionName = "Previous",
                    Description = "View your results from a previous proficiency test",
                    RequiredRole = ExternalRoleNames.Participant
                },
                new NavigationItem
                {
                    Text = "View reports",
                    ControllerName = "Tabulation",
                    ActionName = IndexAction,
                    Description = "View completed proficiency test reports, which include all participant results",
                    RequiredRole = ExternalRoleNames.Viewer
                },
                new NavigationItem
                {
                    Text = "Enter comments",
                    ControllerName = "Comments",
                    ActionName = IndexAction,
                    Description = "Provide or view proficiency test comments",
                    RequiredRole = ExternalRoleNames.TestConsultant
                }
            ]
        },
        new NavigationItem
        {
            Text = "My account",
            RequiredRole = ExternalRoleNames.Participant,
            Children =
            [
                new NavigationItem
                {
                    Text = "Edit customer details",
                    ControllerName = "Customer",
                    ActionName = IndexAction,
                    Description = "Submit an update to your customer details",
                    RequiredRole = ExternalRoleNames.Participant
                },
                new NavigationItem
                {
                    Text = "Edit participant details",
                    ControllerName = "Participant",
                    ActionName = IndexAction,
                    Description = "Submit an update to participating laboratory details",
                    RequiredRole = ExternalRoleNames.Participant
                }
            ]
        }
    ];
}
