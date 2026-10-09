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
    private const string DetailsAction = "Details";
    private const string SchemeControllerName = "Scheme";
    private const string CustomerControllerName = "Customer";
    private const string ParticipantControllerName = "Participant";
    private const string ContractControllerName = "Contract";
    private const string GroupAddressControllerName = "GroupAddress";
    private const string ParticipantSchemeControllerName = "ParticipantScheme";
    private const string SystemAdministrationControllerName = "SystemAdministration";

    // Placeholder for a menu entry whose page hasn't been migrated yet - see class remarks.
    private static SideNavigationItem Disabled(string text) => new() { Text = text, IsEnabled = false };

    public static IReadOnlyList<SideNavigationItem> Build() =>
    [
        new SideNavigationItem { Text = "Home", ControllerName = "Home", ActionName = IndexAction },
        new SideNavigationItem
        {
            Text = "System Administration",
            ControllerName = SystemAdministrationControllerName,
            ActionName = SystemAdministrationControllerName,
            Children =
            [
                new SideNavigationItem { Text = "Create User", ControllerName = SystemAdministrationControllerName, ActionName = "CreateUser" },
                new SideNavigationItem { Text = "Assign Roles to User", ControllerName = SystemAdministrationControllerName, ActionName = "ManageUserRoles" },
                new SideNavigationItem { Text = "Remove User", ControllerName = SystemAdministrationControllerName, ActionName = "RemoveUser" },
                new SideNavigationItem { Text = "Internal Test Consultant Department Management", ControllerName = SystemAdministrationControllerName, ActionName = "InternalTestConsultantDepartment" },
                new SideNavigationItem { Text = "External Test Consultant Management", ControllerName = SystemAdministrationControllerName, ActionName = "ExternalTestConsultantManagement" },
                new SideNavigationItem { Text = "Viewer Management", ControllerName = SystemAdministrationControllerName, ActionName = "ViewerManagement" },
                new SideNavigationItem { Text = "Country Management", ControllerName = SystemAdministrationControllerName, ActionName = "CountryManagement" },
                new SideNavigationItem { Text = "External Site Management", ControllerName = SystemAdministrationControllerName, ActionName = "ExternalSiteManagement" },
                new SideNavigationItem { Text = "Administration Charges Management", ControllerName = SystemAdministrationControllerName, ActionName = "AdministrationCharge" },
                new SideNavigationItem
                {
                    Text = "Weighted Charging Plan",
                    ControllerName = SystemAdministrationControllerName,
                    ActionName = "WeightedPricingPlan",
                    Children =
                    [
                        // "Renew" doesn't follow the "{ParentActionName}{Suffix}" convention the
                        // prefix fallback in FindNode/FindPath relies on (unlike e.g.
                        // "PostagePricingPlanRenew"), so it needs its own explicit entry - same
                        // pattern as "Renew Contracts" under the hidden "Contracts" node below.
                        new SideNavigationItem { Text = "Renew Weighted Pricing Plan", ControllerName = SystemAdministrationControllerName, ActionName = "Renew", IsHidden = true }
                    ]
                },
                new SideNavigationItem { Text = "Postage Pricing Plan", ControllerName = SystemAdministrationControllerName, ActionName = "PostagePricingPlan" }
            ]
        },
        new SideNavigationItem
        {
            Text = "Manage Contracts",
            ControllerName = ContractControllerName,
            ActionName = "ManageContracts",
            Children =
            [
                new SideNavigationItem
                {
                    Text = "Customers",
                    ControllerName = CustomerControllerName,
                    ActionName = IndexAction,
                    Children =
                    [
                        new SideNavigationItem { Text = "Create Customer", ControllerName = CustomerControllerName, ActionName = CreateAction },
                        new SideNavigationItem
                        {
                            Text = "Review Pending Customer Updates",
                            ControllerName = CustomerControllerName,
                            ActionName = "ReviewPendingCustomerUpdates",
                            Children =
                            [
                                new SideNavigationItem { Text = "Pending Customer Update Details", ControllerName = CustomerControllerName, ActionName = "PendingCustomerUpdateDetails", IsHidden = true },
                                new SideNavigationItem { Text = "Edit Pending Customer Update", ControllerName = CustomerControllerName, ActionName = "EditPendingCustomerUpdate", IsHidden = true }
                            ]
                        },
                        new SideNavigationItem
                        {
                            Text = "Review Pending Participant Updates",
                            ControllerName = ParticipantControllerName,
                            ActionName = "ReviewPendingParticipantUpdates",
                            Children =
                            [
                                new SideNavigationItem { Text = "Pending Participant Update Details", ControllerName = ParticipantControllerName, ActionName = "PendingParticipantUpdateDetails", IsHidden = true },
                                new SideNavigationItem { Text = "Edit Pending Participant Update", ControllerName = ParticipantControllerName, ActionName = "EditPendingParticipantUpdate", IsHidden = true }
                            ]
                        },
                        new SideNavigationItem
                        {
                            Text = "Review Pending Orders",
                            ControllerName = ContractControllerName,
                            ActionName = "ReviewPendingOrders",
                            Children =
                            [
                                new SideNavigationItem { Text = "Pending Order Details", ControllerName = ContractControllerName, ActionName = "PendingOrderDetails", IsHidden = true }
                            ]
                        },

                        // Hidden: not a menu entry, but present so Details/Edit pages resolve a full
                        // breadcrumb trail instead of a plain controller/action crumb.
                        new SideNavigationItem { Text = "Customer Details", ControllerName = CustomerControllerName, ActionName = DetailsAction, IsHidden = true },
                        new SideNavigationItem { Text = "Edit Customer", ControllerName = CustomerControllerName, ActionName = EditAction, IsHidden = true },

                        // Hidden: only reached from within a specific customer (Customer Details'
                        // "View participants"/"View contracts" links), never listed here - matches
                        // the legacy sitemap's hidden Participants/Contracts nodes under Customers.
                        new SideNavigationItem
                        {
                            Text = "Participants",
                            ControllerName = ParticipantControllerName,
                            ActionName = IndexAction,
                            IsHidden = true,
                            LinkRouteParameter = BreadcrumbRouteValues.CustomerIdKey,
                            LinkRouteValueKey = BreadcrumbRouteValues.CustomerIdKey,
                            Children =
                            [
                                new SideNavigationItem { Text = "Create Participant", ControllerName = ParticipantControllerName, ActionName = CreateAction },
                                new SideNavigationItem { Text = "Participant Details", ControllerName = ParticipantControllerName, ActionName = DetailsAction, IsHidden = true },
                                new SideNavigationItem { Text = "Edit Participant", ControllerName = ParticipantControllerName, ActionName = EditAction, IsHidden = true },
                                new SideNavigationItem { Text = "Participant Viewers", ControllerName = ParticipantControllerName, ActionName = "Viewers", IsHidden = true }
                            ]
                        },
                        new SideNavigationItem
                        {
                            Text = "Contracts",
                            ControllerName = ContractControllerName,
                            ActionName = IndexAction,
                            IsHidden = true,
                            LinkRouteParameter = BreadcrumbRouteValues.CustomerIdKey,
                            LinkRouteValueKey = BreadcrumbRouteValues.CustomerIdKey,
                            Children =
                            [
                                new SideNavigationItem { Text = "Create Contract", ControllerName = ContractControllerName, ActionName = CreateAction },
                                new SideNavigationItem { Text = "Renew Contracts", ControllerName = ContractControllerName, ActionName = "RenewContracts" },
                                new SideNavigationItem { Text = "Contract Details", ControllerName = ContractControllerName, ActionName = DetailsAction, IsHidden = true },
                                new SideNavigationItem { Text = "Edit Contract", ControllerName = ContractControllerName, ActionName = EditAction, IsHidden = true },

                                // Hidden: reached from the Contracts list and from Contract Details.
                                // Its own children all return here via their Back/Cancel buttons.
                                new SideNavigationItem
                                {
                                    Text = "Contract Items",
                                    ControllerName = ContractControllerName,
                                    ActionName = "ContractItems",
                                    IsHidden = true,
                                    LinkRouteParameter = "id",
                                    LinkRouteValueKey = BreadcrumbRouteValues.ContractIdKey,
                                    Children =
                                    [
                                        new SideNavigationItem { Text = "Contract Item Details", ControllerName = ParticipantSchemeControllerName, ActionName = DetailsAction, IsHidden = true },
                                        new SideNavigationItem { Text = "Add Contract Item", ControllerName = ParticipantSchemeControllerName, ActionName = CreateAction, IsHidden = true },
                                        new SideNavigationItem { Text = "Edit Contract Item", ControllerName = ParticipantSchemeControllerName, ActionName = EditAction, IsHidden = true },
                                        new SideNavigationItem { Text = "Import Permits", ControllerName = ContractControllerName, ActionName = "ImportPermits", IsHidden = true }
                                    ]
                                },

                                // Hidden: a download action that only renders a page when the
                                // requested document type has no template (FeatureNotAvailable).
                                new SideNavigationItem { Text = "Export", ControllerName = ContractControllerName, ActionName = "Export", IsHidden = true }
                            ]
                        }
                    ]
                },
                Disabled("Search"),
                new SideNavigationItem
                {
                    Text = "Group Addresses",
                    ControllerName = GroupAddressControllerName,
                    ActionName = IndexAction,
                    Children =
                    [
                        new SideNavigationItem { Text = "Create Group Address", ControllerName = GroupAddressControllerName, ActionName = CreateAction },
                        new SideNavigationItem { Text = "Group Address Details", ControllerName = GroupAddressControllerName, ActionName = DetailsAction, IsHidden = true },
                        new SideNavigationItem { Text = "Edit Group Address", ControllerName = GroupAddressControllerName, ActionName = EditAction, IsHidden = true }
                    ]
                },
                new SideNavigationItem
                {
                    Text = "Exports",
                    ControllerName = ContractControllerName,
                    ActionName = "Exports",
                    Children =
                    [
                        new SideNavigationItem { Text = "Export Contracts", ControllerName = ContractControllerName, ActionName = "ExportContracts" },
                        new SideNavigationItem { Text = "Export Job Sheets", ControllerName = ContractControllerName, ActionName = "ExportJobSheets" },
                        new SideNavigationItem { Text = "Export Renewal Letters", ControllerName = ContractControllerName, ActionName = "ExportRenewalLetters" },
                        new SideNavigationItem { Text = "Export Address Confirmation Letters", ControllerName = ContractControllerName, ActionName = "ExportAddressConfirmationLetters" }
                    ]
                },
                new SideNavigationItem
                {
                    Text = "Invoice Generation",
                    ControllerName = "Invoice",
                    ActionName = IndexAction
                }
            ]
        },
        new SideNavigationItem
        {
            Text = "Manage Schemes",
            ControllerName = SchemeControllerName,
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
                        // Legacy's two separate entry points: Web.sitemap's "Create Scheme for Next
                        // Year" and the "Create Scheme for Current Year" item SchemeList.aspx adds.
                                                new SideNavigationItem { Text = "Create Scheme for Current Year", ControllerName = SchemeControllerName, ActionName = "CreateForCurrentYear" },
                        new SideNavigationItem { Text = "Create Scheme for Next Year", ControllerName = SchemeControllerName, ActionName = CreateAction },
                        new SideNavigationItem { Text = "Printable Schemes", ControllerName = SchemeControllerName, ActionName = "PrintableSchemes" },


                        // Hidden: not menu entries, but present so Details/Edit pages resolve a full
                        // breadcrumb trail instead of a plain controller/action crumb.
                        new SideNavigationItem { Text = "Scheme Details", ControllerName = SchemeControllerName, ActionName = DetailsAction, IsHidden = true },
                        new SideNavigationItem { Text = "Edit Scheme", ControllerName = SchemeControllerName, ActionName = EditAction, IsHidden = true },

                        // Hidden: Renew reuses the Create view/action but needs its own breadcrumb
                        // leaf - Home > Manage Schemes > Scheme > Scheme - Renew - rather than
                        // falling back to a plain controller/action crumb.
                        new SideNavigationItem { Text = "Scheme - Renew", ControllerName = SchemeControllerName, ActionName = "Renew", IsHidden = true },

                        // Hidden: only reached from a specific scheme's Details page ("View family
                        // history" link), not listed as a Scheme List child - same hidden pattern.
                        new SideNavigationItem { Text = "Scheme History", ControllerName = SchemeControllerName, ActionName = "History", IsHidden = true },

                        // Hidden: legacy "Printable Scheme" (singular) is a hidden sitemap node too,
                        // reached only via the Printable Schemes list's View links.
                        new SideNavigationItem { Text = "Printable Scheme", ControllerName = SchemeControllerName, ActionName = "PrintableScheme", IsHidden = true }
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
    //
    // Falls back to a prefix match (same controller, longest registered ActionName that the
    // current action name starts with) when no exact match exists - needed because many POST
    // sub-actions (e.g. ViewerManagementController's "ViewerManagementRemove", "CreateUserSearch")
    // return View(viewName, ...) directly rather than redirecting, so RouteData's action name
    // stays the sub-action's own name, never the logical page's registered ActionName. Without
    // this, the sidebar/breadcrumb would have no current node to show (see
    // _SideNavigation.cshtml's "currentNode is null" branch).
    public static SideNavigationItem? FindNode(IReadOnlyList<SideNavigationItem> items, string controllerName, string actionName) =>
        FindNodeExact(items, controllerName, actionName) ?? FindNodeByActionPrefix(items, controllerName, actionName);

    private static SideNavigationItem? FindNodeExact(IReadOnlyList<SideNavigationItem> items, string controllerName, string actionName)
    {
        foreach (var item in items)
        {
            if (string.Equals(item.ControllerName, controllerName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.ActionName, actionName, StringComparison.OrdinalIgnoreCase))
            {
                return item;
            }

            var found = FindNodeExact(item.Children, controllerName, actionName);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private static SideNavigationItem? FindNodeByActionPrefix(IReadOnlyList<SideNavigationItem> items, string controllerName, string actionName)
    {
        SideNavigationItem? best = null;

        void Walk(IReadOnlyList<SideNavigationItem> nodes)
        {
            foreach (var item in nodes)
            {
                if (string.Equals(item.ControllerName, controllerName, StringComparison.OrdinalIgnoreCase)
                    && item.ActionName is not null
                    && actionName.StartsWith(item.ActionName, StringComparison.OrdinalIgnoreCase)
                    && (best is null || item.ActionName.Length > best.ActionName!.Length))
                {
                    best = item;
                }

                Walk(item.Children);
            }
        }

        Walk(items);
        return best;
    }

    // The full ancestor chain from the root down to (and including) the matching node - used to
    // render the breadcrumb trail as the exact reverse of the drill-down navigation journey.
    // Same exact-then-prefix-fallback strategy as FindNode - once the prefix fallback resolves
    // the logical node, re-runs the exact path-finder against ITS OWN (controller, action) pair
    // rather than duplicating the path-tracking walk.
    public static IReadOnlyList<SideNavigationItem>? FindPath(IReadOnlyList<SideNavigationItem> items, string controllerName, string actionName)
    {
        var exactPath = FindPathExact(items, controllerName, actionName);
        if (exactPath is not null)
        {
            return exactPath;
        }

        var fallbackNode = FindNodeByActionPrefix(items, controllerName, actionName);
        return fallbackNode is null ? null : FindPathExact(items, fallbackNode.ControllerName!, fallbackNode.ActionName!);
    }

    private static IReadOnlyList<SideNavigationItem>? FindPathExact(IReadOnlyList<SideNavigationItem> items, string controllerName, string actionName)
    {
        foreach (var item in items)
        {
            if (string.Equals(item.ControllerName, controllerName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.ActionName, actionName, StringComparison.OrdinalIgnoreCase))
            {
                return [item];
            }

            var childPath = FindPathExact(item.Children, controllerName, actionName);
            if (childPath is not null)
            {
                return [item, .. childPath];
            }
        }

        return null;
    }

    public static SideNavigationItem? FindActiveNode(IReadOnlyList<SideNavigationItem> items, string controllerName, string actionName) =>
        FindNode(items, controllerName, actionName);

    public static IReadOnlyList<SideNavigationItem>? FindActivePath(IReadOnlyList<SideNavigationItem> items, string controllerName, string actionName) =>
        FindPath(items, controllerName, actionName);
}

