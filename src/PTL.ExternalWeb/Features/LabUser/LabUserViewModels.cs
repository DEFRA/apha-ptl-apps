using PTL.Contracts.LabUsers;

namespace PTL.ExternalWeb.Features.LabUser;

/// <summary>The Lab users page: the lab and the people who can sign in to it.</summary>
public sealed record LabUserListViewModel(string LabCode, string LabName, IReadOnlyList<LabUserResponse> Users);

/// <summary>The Add lab user / Edit lab user form.</summary>
public sealed class LabUserFormViewModel
{
    /// <summary>The person being edited; null when adding.</summary>
    public Guid? LabUserId { get; set; }

    // Nullable so an empty box is reported once, by the API's message, rather than also by ASP.NET Core's own "required" check.
    public string? Name { get; set; }

    public string? Email { get; set; }

    public bool IsActive { get; set; } = true;

    public string LabCode { get; set; } = string.Empty;

    public string LabName { get; set; } = string.Empty;

    /// <summary>False when the person also works with other labs, so only their access to this lab can be changed.</summary>
    public bool CanChangeDetails { get; set; } = true;

    /// <summary>True when the person has already linked a CIDM account, which a changed email address ends.</summary>
    public bool HasSignedIn { get; set; }

    public bool IsEdit => LabUserId is not null;
}
