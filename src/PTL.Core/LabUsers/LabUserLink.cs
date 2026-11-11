namespace PTL.Core.LabUsers;

/// <summary>
/// One person's access to one lab, as listed to the lab's primary contact: the person (tblLabUser) and the link to
/// the lab (tlnkLabUserParticipant) with the per-lab state.
/// </summary>
public sealed record LabUserLink
{
    /// <summary>The link's own id - what the login instructions email is recorded against.</summary>
    public Guid LabUserParticipantId { get; init; }

    public Guid LabUserId { get; init; }

    public Guid ParticipantId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    /// <summary>Whether the person can currently sign in to this lab.</summary>
    public bool IsActive { get; init; }

    public bool LoginInstructionsSent { get; init; }

    /// <summary>How many other labs the person is linked to; above zero means their name and email are shared and cannot be changed from here.</summary>
    public int OtherLabCount { get; init; }

    /// <summary>Whether the person has already linked a CIDM account.</summary>
    public bool HasSignedIn { get; init; }

    public string LabCode { get; init; } = string.Empty;

    public string LabName { get; init; } = string.Empty;
}
