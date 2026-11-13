namespace PTL.Contracts.LabUsers;

/// <summary>One person who can sign in to a lab, with their access to that lab.</summary>
/// <param name="LabUserId">The person's id (shared across the labs they work with).</param>
/// <param name="Name">The person's name.</param>
/// <param name="Email">The email address they sign in with.</param>
/// <param name="IsActive">Whether they can currently sign in to this lab.</param>
/// <param name="LoginInstructionsSent">Whether the login instructions email for this lab has been sent.</param>
/// <param name="CanChangeDetails">False when the person also works with other labs, so their name and email cannot be changed from this lab.</param>
/// <param name="HasSignedIn">Whether they have already linked a CIDM account.</param>
public sealed record LabUserResponse(
    Guid LabUserId,
    string Name,
    string Email,
    bool IsActive,
    bool LoginInstructionsSent,
    bool CanChangeDetails,
    bool HasSignedIn);
