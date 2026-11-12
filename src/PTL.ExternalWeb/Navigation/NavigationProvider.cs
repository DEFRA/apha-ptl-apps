using PTL.Contracts.ExternalUser;

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
    private const string DistributionControllerName = "Distribution";
    private const string TabulationControllerName = "Tabulation";
    private const string CommentsControllerName = "Comments";
    private const string ReportsText = "Reports";
    private const string ViewReportsText = "View reports";
    private const string MyAccountText = "My account";
    private const string LabUsersText = "Lab users";
    private const string LabUserControllerName = "LabUser";
    private const string CreateNewOrderText = "Create new order";

    // Legacy Home.aspx lists Orders, Schemes, Results, Accounts, while its top menu (NavigationBar.ascx) lists Schemes, Orders, Results.
    private static readonly string[] HomeSectionOrder = ["Orders", "Schemes", "Results", MyAccountText, LabUsersText];

    public static IReadOnlyList<NavigationItem> BuildForHome() =>
        [.. Build().OrderBy(section => Array.IndexOf(HomeSectionOrder, section.Text))];

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
                    Description = "List of current proficiency tests available",
                    RequireLabCode = true
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
                    ControllerName = DistributionControllerName,
                    ActionName = "Current",
                    Description = "Submit results for a current distribution",
                    RequiredRole = ExternalRoleNames.Participant,
                    RequireLabCode = true
                },
                new NavigationItem
                {
                    Text = "View results",
                    ControllerName = DistributionControllerName,
                    ActionName = "Previous",
                    Description = "View your results from a previous proficiency test",
                    RequiredRole = ExternalRoleNames.Participant,
                    RequireLabCode = true
                },
                new NavigationItem
                {
                    Text = ViewReportsText,
                    ControllerName = TabulationControllerName,
                    ActionName = IndexAction,
                    Description = "View completed proficiency test reports, which include all participant results",
                    RequiredRole = ExternalRoleNames.Viewer
                },
                new NavigationItem
                {
                    Text = "Enter comments",
                    ControllerName = CommentsControllerName,
                    ActionName = IndexAction,
                    Description = "Provide or view proficiency test comments",
                    RequiredRole = ExternalRoleNames.TestConsultant
                }
            ]
        },
        new NavigationItem
        {
            Text = MyAccountText,
            RequiredRole = ExternalRoleNames.Participant,
            // Editing the customer and the lab's own details is for the primary contact; a Lab User cannot.
            ExcludeLabUsers = true,
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
        },
        new NavigationItem
        {
            Text = LabUsersText,
            RequiredRole = ExternalRoleNames.Participant,
            // Only the lab's primary contact manages who can sign in to the lab.
            ExcludeLabUsers = true,
            Children =
            [
                new NavigationItem
                {
                    Text = "View lab users",
                    ControllerName = LabUserControllerName,
                    ActionName = IndexAction,
                    Description = "See who can sign in to this lab and make users active or inactive",
                    RequiredRole = ExternalRoleNames.Participant
                },
                new NavigationItem
                {
                    Text = "Add lab user",
                    ControllerName = LabUserControllerName,
                    ActionName = "Add",
                    Description = "Give a new person access to this lab",
                    RequiredRole = ExternalRoleNames.Participant
                }
            ]
        }
    ];

    // Legacy Web.sitemap titles (rendered by SiteMapPath as Home > ancestors > current page, current
    // page not a link) for pages whose breadcrumb differs from their nav group. Keyed controller/action.
    private static readonly Dictionary<string, BreadcrumbCrumb[]> Trails = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Distribution/Current"] = [new("Current distributions")],
        ["Help/Index"] = [new("Help")],
        ["Distribution/Entry"] = [new("Current distributions", DistributionControllerName, "Current"), new("Results entry")],
        ["Distribution/Previous"] = [new("Previous distributions")],
        ["Distribution/Results"] = [new("Previous distributions", DistributionControllerName, "Previous"), new("View results")],
        ["Tabulation/Index"] = [new(ReportsText)],
        ["Tabulation/Report"] = [new(ReportsText, TabulationControllerName, IndexAction), new(ViewReportsText)],
        ["Tabulation/AssessmentResults"] =
        [
            new(ReportsText, TabulationControllerName, IndexAction),
            new(ViewReportsText, TabulationControllerName, "Report", "publishedTabulationId"),
            new("Assessment results")
        ],
        ["Comments/Index"] = [new("Distributions")],
        ["Comments/Entry"] = [new("Distributions", CommentsControllerName, IndexAction), new("Comments entry")],
        ["Scheme/Index"] = [new("View schemes")],
        ["Customer/Index"] = [new(MyAccountText)],
        ["Participant/Index"] = [new(MyAccountText)],
        ["LabUser/Index"] = [new(LabUsersText)],
        ["LabUser/Add"] = [new(LabUsersText, LabUserControllerName, IndexAction), new("Add lab user")],
        ["LabUser/Edit"] = [new(LabUsersText, LabUserControllerName, IndexAction), new("Edit lab user")],
        ["Order/CurrentYear"] = [new(CreateNewOrderText)],
        ["Order/NextYear"] = [new(CreateNewOrderText)]
    };

    // Legacy PendingContractOrder.aspx showed Home > Create new order (link back to that year's order) >
    // stage title once past the start view; the year comes from the request's year slug.
    private static readonly Dictionary<string, string> OrderStageTitles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Order/Review"] = "Pending order",
        ["Order/ConfirmClear"] = "Clear the order form"
    };

    public static IReadOnlyList<BreadcrumbCrumb>? BreadcrumbTrail(string controller, string action, string? year = null)
    {
        var key = $"{controller}/{action}";
        if (OrderStageTitles.TryGetValue(key, out var stageTitle))
        {
            var yearAction = string.Equals(year, Features.Order.OrderController.NextYearSlug, StringComparison.OrdinalIgnoreCase)
                ? "NextYear"
                : "CurrentYear";
            return [new(CreateNewOrderText, "Order", yearAction), new(stageTitle)];
        }

        return Trails.TryGetValue(key, out var trail) ? trail : null;
    }
}

/// <summary>One breadcrumb step. Controller/Action null means plain text; RouteKey names a query value
/// (e.g. an id) the link needs from the current request.</summary>
public sealed record BreadcrumbCrumb(string Text, string? Controller = null, string? Action = null, string? RouteKey = null);
