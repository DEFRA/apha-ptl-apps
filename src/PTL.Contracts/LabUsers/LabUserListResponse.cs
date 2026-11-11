namespace PTL.Contracts.LabUsers;

/// <summary>A lab and the people who can sign in to it.</summary>
public sealed record LabUserListResponse(string LabCode, string LabName, IReadOnlyList<LabUserResponse> Users);

/// <summary>A lab and one of its lab users (the edit page).</summary>
public sealed record LabUserDetailsResponse(string LabCode, string LabName, LabUserResponse User);
