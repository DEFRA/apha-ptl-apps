namespace PTL.Contracts.Viewer;

// Success is false (with Message populated) when the viewer no longer exists or is still assigned to
// schemes/participants - ViewerService.DeleteAsync blocks that, as legacy ManageViewers.aspx intended.
public sealed record ViewerDeleteResponse(bool Success, string? Message);
