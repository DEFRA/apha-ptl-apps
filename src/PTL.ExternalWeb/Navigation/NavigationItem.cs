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

    /// <summary>When true the item is hidden from a Lab User even though they hold the required role (a Lab User holds the
    /// Participant role too) - for what only the lab's primary contact may use, such as My account and Lab users.</summary>
    public bool ExcludeLabUsers { get; init; }

    /// <summary>When true, also requires ClaimsPrincipalExtensions.CanOrderOnline() - gates the
    /// Orders section on the linked Customer's CanOrderOnline flag, not just the Participant role.</summary>
    public bool RequireCanOrderOnline { get; init; }

    /// <summary>Legacy's menu (not its Home cards) hid these links for a participant with no Lab Code, so only the header
    /// menu passes the real <c>hasLabCode</c> to <see cref="IsVisibleTo"/>.</summary>
    public bool RequireLabCode { get; init; }

    public IReadOnlyList<NavigationItem> Children { get; init; } = [];

    /// <summary>Whether a user with these roles may see the item - the one rule the header menu and the Home cards share.</summary>
    public bool IsVisibleTo(IReadOnlyList<string> roles, bool isLabUser, bool canOrderOnline, bool hasLabCode = true) =>
        (RequiredRole is null || roles.Contains(RequiredRole, StringComparer.OrdinalIgnoreCase))
        && (!RequireCanOrderOnline || canOrderOnline)
        && (!ExcludeLabUsers || !isLabUser)
        && (!RequireLabCode || hasLabCode);

    /// <summary>False for a Create order link whose year's order is already submitted. Legacy hid it from the menu, while the
    /// Home card stays but is disabled. Any other item is always open.</summary>
    public bool IsOrderYearOpen(bool currentYearOpen, bool nextYearOpen) =>
        ControllerName != "Order" || ActionName switch
        {
            "CurrentYear" => currentYearOpen,
            "NextYear" => nextYearOpen,
            _ => true
        };
}
