namespace PTL.Contracts.Viewer;

// Always Success=true unless the viewer id no longer exists - spdViewer is an unconditional
// delete (no allocation block server-side); the confirmation listing assigned schemes/
// participants is a client-side-only courtesy, matching legacy ManageViewers.aspx.
public sealed record ViewerDeleteResponse(bool Success, string? Message);
