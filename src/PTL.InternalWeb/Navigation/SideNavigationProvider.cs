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
    private const string EditAction = "Edit";
    private const string SchemeControllerName = "Scheme";

    public static IReadOnlyList<SideNavigationItem> Build() =>
    [
        new SideNavigationItem { Text = "Home", ControllerName = "Home", ActionName = IndexAction },
        new SideNavigationItem { Text = "System Administration", IsEnabled = false },
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
                        new SideNavigationItem { Text = "Review Pending Customer Updates", IsEnabled = false },
                        new SideNavigationItem { Text = "Review Pending Participant Updates", IsEnabled = false },
                        new SideNavigationItem { Text = "Review Pending Orders", IsEnabled = false },

                        // Hidden: only reached via a specific customer row's "View" link - lets the
                        // breadcrumb trail resolve to Manage Contracts > Customers > Customer Details
                        // instead of falling back to a plain controller/action crumb.
                        new SideNavigationItem { Text = "Customer Details", ControllerName = "Customer", ActionName = "Details", IsHidden = true },

                        // Hidden: same reasoning as "Customer Details" above, for the Edit page.
                        new SideNavigationItem { Text = "Edit Customer", ControllerName = "Customer", ActionName = EditAction, IsHidden = true },

                        // Hidden: only reached from within a specific customer (Customer Details'
                        // "View participants"/"View contracts" links), never listed here - matches
                        // the legacy sitemap's hidden Participants/Contracts nodes under Customers.
                        new SideNavigationItem
                        {
                            Text = "Participants",
                            ControllerName = "Participant",
                            ActionName = IndexAction,
                            IsHidden = true,
                            Children =
                            [
                                new SideNavigationItem { Text = "Create Participant", ControllerName = "Participant", ActionName = CreateAction },
                                new SideNavigationItem { Text = "Participant Details", ControllerName = "Participant", ActionName = "Details", IsHidden = true },
                                new SideNavigationItem { Text = "Edit Participant", ControllerName = "Participant", ActionName = EditAction, IsHidden = true }
                            ]
                        },
                        new SideNavigationItem
                        {
                            Text = "Contracts",
                            ControllerName = "Contract",
                            ActionName = IndexAction,
                            IsHidden = true,
                            Children =
                            [
                                new SideNavigationItem { Text = "Create Contract", ControllerName = "Contract", ActionName = CreateAction },
                                new SideNavigationItem { Text = "Contract Details", ControllerName = "Contract", ActionName = "Details", IsHidden = true },
                                new SideNavigationItem { Text = "Edit Contract", ControllerName = "Contract", ActionName = EditAction, IsHidden = true }
                            ]
                        }
                    ]
                },
                new SideNavigationItem { Text = "Search", IsEnabled = false },
                new SideNavigationItem { Text = "Group Addresses", IsEnabled = false },
                new SideNavigationItem { Text = "Exports", IsEnabled = false },
                new SideNavigationItem { Text = "Invoice Generation", IsEnabled = false }
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
                        new SideNavigationItem { Text = "Scheme History", ControllerName = SchemeControllerName, ActionName = "History", IsHidden = true },

                        // Hidden: lets the breadcrumb trail resolve to Manage Schemes > Scheme >
                        // Scheme Details instead of falling back to a plain controller/action crumb.
                        new SideNavigationItem { Text = "Scheme Details", ControllerName = SchemeControllerName, ActionName = "Details", IsHidden = true },

                        // Hidden: same reasoning as "Scheme Details" above, for the Edit page.
                        new SideNavigationItem { Text = "Edit Scheme", ControllerName = SchemeControllerName, ActionName = EditAction, IsHidden = true }
                    ]
                },
                new SideNavigationItem { Text = "Search", IsEnabled = false },
                new SideNavigationItem { Text = "Test Types", IsEnabled = false },
                new SideNavigationItem { Text = "Test Result Items", IsEnabled = false },
                new SideNavigationItem { Text = "Test Method Items", IsEnabled = false },
                new SideNavigationItem { Text = "Category Items", IsEnabled = false },
                new SideNavigationItem { Text = "Criterion Items", IsEnabled = false }
            ]
        },
        new SideNavigationItem { Text = "Distributions", IsEnabled = false },
        new SideNavigationItem { Text = "Test Consultant", IsEnabled = false },
        new SideNavigationItem { Text = "Assessor", IsEnabled = false },
        new SideNavigationItem { Text = "Results Sign-Off", IsEnabled = false }
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

