namespace PTL.InternalWeb.Navigation;

// One node in the left-hand PTLIMS navigation tree. Mirrors a node from the legacy Web.sitemap
// (ProficiencyTestingWeb/ProficiencyTestingAdmin) - see SideNavigationProvider for the full
// Legacy -> Target mapping. The current page's own node supplies the title shown in the nav, and
// that node's Children are what gets listed below it (never a flat multi-level tree at once).
public sealed class SideNavigationItem
{
    public required string Text { get; init; }

    // Policy a user must satisfy to see this entry (legacy MasterContent hid menu children whose folder role the
    // user lacked). Children without their own inherit their parent's - see SideNavigationProvider.Build.
    public string? RequiredPolicy { get; set; }
    public string? ControllerName { get; init; }
    public string? ActionName { get; init; }
    public bool IsEnabled { get; init; } = true;

    // Hidden nodes are excluded when rendering their parent's own child list (e.g. Participants/
    // Contracts, only ever reached once already inside a specific customer - matches the legacy
    // sitemap's hidden="true" attribute) but remain reachable via FindNode/FindPath.
    public bool IsHidden { get; init; }
    public IReadOnlyList<SideNavigationItem> Children { get; init; } = [];

    // Set when this node's route needs a parameter to resolve to a usable page - e.g. the
    // Contracts list is per-customer. LinkRouteParameter is the route parameter to emit;
    // LinkRouteValueKey names the ambient value that supplies it (see BreadcrumbRouteValues).
    public string? LinkRouteParameter { get; init; }
    public string? LinkRouteValueKey { get; init; }

    // Request values (query string or route) copied onto this node's breadcrumb link - e.g. a
    // Distribution month page needs year and monthId. If any is absent the crumb renders as plain text.
    public IReadOnlyList<string> LinkQueryKeys { get; init; } = [];

    // Like LinkQueryKeys, but for a node whose own action tolerates the value being absent (e.g.
    // Distributions/Index defaults its year when none is supplied) - carried onto the link when
    // present, simply omitted rather than disabling the whole link when absent.
    public IReadOnlyList<string> OptionalLinkQueryKeys { get; init; } = [];
}
