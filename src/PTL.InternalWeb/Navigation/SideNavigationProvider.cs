namespace PTL.InternalWeb.Navigation;

// Fixed configuration for PTLIMS's left-hand navigation, preserving the legacy left-sidebar menu
// (ProficiencyTestingWeb/Web.sitemap, ProficiencyTestingAdmin/Web.sitemap - rendered there via the
// custom PtaBusinessObjects ListControl) exactly: same names, order, and hierarchy.
//
// Legacy behaviour this reproduces: the left nav is context-based, not a permanent full tree.
// Each page shows only its OWN node's title plus that node's direct children - never the whole
// menu at once. Navigating to "Manage Contracts" shows the Contracts Administration landing page
// and its children (Customers, Search, Group Addresses, Exports, Invoice Generation); navigating
// on to "Customers" replaces that with the Customer children (Create Customer, Review Pending
// Customer/Participant Updates, Review Pending Orders); navigating to "Create Customer" (a leaf)
// shows only "Create Customer". See _SideNavigation.cshtml for the rendering logic.
//
// No new pages are created for disabled entries - they exist only so the overall menu shape
// matches the legacy application until each area is migrated (docs/prompts/05-feature-implementation.md).
public static class SideNavigationProvider
{
    private const string IndexAction = "Index";
    private const string CreateAction = "Create";
    private const string SchemeControllerName = "Scheme";

    // Placeholder for a menu entry whose page hasn't been migrated yet - see class remarks.
    private static SideNavigationItem Disabled(string text) => new() { Text = text, IsEnabled = false };

    public static IReadOnlyList<SideNavigationItem> Build() =>
    [
        new SideNavigationItem { Text = "Home", ControllerName = "Home", ActionName = IndexAction },
        new SideNavigationItem
        {
            Text = "System Administration",
            ControllerName = "Menu",
            ActionName = "SystemAdministration",
            Children =
            [
                Disabled("Create User"),
                Disabled("Assign Roles to User"),
                Disabled("Remove User"),
                Disabled("Internal Test Consultant Department Management"),
                Disabled("External Test Consultant Management"),
                Disabled("Viewer Management"),
                Disabled("Country Management"),
                Disabled("External Site Management"),
                Disabled("Administration Charges Management"),
                Disabled("Weighted Charging Plan"),
                Disabled("Postage Pricing Plan")
            ]
        },
        new SideNavigationItem
        {
            Text = "Manage Contracts",
            ControllerName = "Menu",
            ActionName = "ManageContracts",
            Children =
            [
                new SideNavigationItem
                {
                    Text = "Customers",
                    ControllerName = "Customer",
                    ActionName = IndexAction,
                    Children =
                    [
                        new SideNavigationItem { Text = "Create Customer", ControllerName = "Customer", ActionName = CreateAction },
                        Disabled("Review Pending Customer Updates"),
                        Disabled("Review Pending Participant Updates"),
                        Disabled("Review Pending Orders"),

                        // Hidden: only reached from within a specific customer (Customer Details'
                        // "View participants"/"View contracts" links), never listed here - matches
                        // the legacy sitemap's hidden Participants/Contracts nodes under Customers.
                        new SideNavigationItem
                        {
                            Text = "Participants",
                            ControllerName = "Participant",
                            ActionName = IndexAction,
                            IsHidden = true,
                            Children = [new SideNavigationItem { Text = "Create Participant", ControllerName = "Participant", ActionName = CreateAction }]
                        },
                        new SideNavigationItem
                        {
                            Text = "Contracts",
                            ControllerName = "Contract",
                            ActionName = IndexAction,
                            IsHidden = true,
                            Children = [new SideNavigationItem { Text = "Create Contract", ControllerName = "Contract", ActionName = CreateAction }]
                        }
                    ]
                },
                Disabled("Search"),
                Disabled("Group Addresses"),
                Disabled("Exports"),
                Disabled("Invoice Generation")
            ]
        },
        new SideNavigationItem
        {
            Text = "Manage Schemes",
            ControllerName = "Menu",
            ActionName = "ManageSchemes",
            Children =
            [
                new SideNavigationItem
                {
                    Text = SchemeControllerName,
                    ControllerName = SchemeControllerName,
                    ActionName = IndexAction,
                    Children =
                    [
                        new SideNavigationItem { Text = "Create Scheme", ControllerName = SchemeControllerName, ActionName = CreateAction },

                        // Hidden: only reached from a specific scheme's Details page ("View family
                        // history" link), not listed as a Scheme List child - same hidden pattern.
                        new SideNavigationItem { Text = "Scheme History", ControllerName = SchemeControllerName, ActionName = "History", IsHidden = true }
                    ]
                },
                Disabled("Search"),
                Disabled("Test Types"),
                Disabled("Test Result Items"),
                Disabled("Test Method Items"),
                Disabled("Category Items"),
                Disabled("Criterion Items")
            ]
        },
        Disabled("Distributions"),
        Disabled("Test Consultant"),
        Disabled("Assessor"),
        Disabled("Results Sign-Off")
    ];

    // Depth-first search for the node matching the current controller/action, regardless of
    // depth or IsHidden - this is what makes Participants/Contracts reachable even though they're
    // excluded from their parent's rendered child list.
    public static SideNavigationItem? FindNode(IReadOnlyList<SideNavigationItem> items, string controllerName, string actionName)
    {
        foreach (var item in items)
        {
            if (string.Equals(item.ControllerName, controllerName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.ActionName, actionName, StringComparison.OrdinalIgnoreCase))
            {
                return item;
            }

            var found = FindNode(item.Children, controllerName, actionName);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    // The full ancestor chain from the root down to (and including) the matching node - used to
    // render the breadcrumb trail as the exact reverse of the drill-down navigation journey.
    public static IReadOnlyList<SideNavigationItem>? FindPath(IReadOnlyList<SideNavigationItem> items, string controllerName, string actionName)
    {
        foreach (var item in items)
        {
            if (string.Equals(item.ControllerName, controllerName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.ActionName, actionName, StringComparison.OrdinalIgnoreCase))
            {
                return [item];
            }

            var childPath = FindPath(item.Children, controllerName, actionName);
            if (childPath is not null)
            {
                return [item, .. childPath];
            }
        }

        return null;
    }
}

