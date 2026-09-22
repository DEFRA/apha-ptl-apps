namespace PTL.InternalWeb.Navigation;

// One node in the left-hand PTLIMS navigation tree. Mirrors a node from the legacy Web.sitemap
// (ProficiencyTestingWeb/ProficiencyTestingAdmin) - see SideNavigationProvider for the full
// Legacy -> Target mapping. The current page's own node supplies the title shown in the nav, and
// that node's Children are what gets listed below it (never a flat multi-level tree at once).
public sealed class SideNavigationItem
{
    public required string Text { get; init; }
    public string? ControllerName { get; init; }
    public string? ActionName { get; init; }
    public bool IsEnabled { get; init; } = true;

    // Hidden nodes are excluded when rendering their parent's own child list (e.g. Participants/
    // Contracts, only ever reached once already inside a specific customer - matches the legacy
    // sitemap's hidden="true" attribute) but remain reachable via FindNode/FindPath.
    public bool IsHidden { get; init; }
    public IReadOnlyList<SideNavigationItem> Children { get; init; } = [];
}
