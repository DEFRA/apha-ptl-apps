namespace PTL.ExternalWeb.Navigation;

// One role-gated section, shared by both the top navigation (Text only, until its own landing
// page lands) and the Home page's quick-link cards (Children). Mirrors
// PTL.InternalWeb.Navigation.SideNavigationItem's shape, but adds RequiredRole since visibility
// here is role-gated (Participant/Viewer/Test Consultant) rather than always-visible like the
// internal admin menu.
public sealed class NavigationItem
{
    public required string Text { get; init; }
    public string? ControllerName { get; init; }
    public string? ActionName { get; init; }
    public bool IsEnabled { get; init; } = true;

    /// <summary>Card blurb text, ported from legacy's Home.aspx.resx "*Blurb" resources.</summary>
    public string? Description { get; init; }

    /// <summary>Role required to see this item; null means visible to any authenticated external user.</summary>
    public string? RequiredRole { get; init; }

    /// <summary>When true, also requires ClaimsPrincipalExtensions.CanOrderOnline() - gates the
    /// Orders section on the linked Customer's CanOrderOnline flag, not just the Participant role.</summary>
    public bool RequireCanOrderOnline { get; init; }

    public IReadOnlyList<NavigationItem> Children { get; init; } = [];
}
