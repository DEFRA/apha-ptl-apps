namespace PTL.Contracts.Viewer;

public sealed record ViewerSchemeResponse(string Identifier, string Name);

public sealed record ViewerParticipantResponse(string LabCode, string LabName);

public sealed record ViewerResponse(
    Guid ViewerId,
    string Name,
    string Email,
    bool HasLogin,
    IReadOnlyList<ViewerSchemeResponse> Schemes,
    IReadOnlyList<ViewerParticipantResponse> Participants);
