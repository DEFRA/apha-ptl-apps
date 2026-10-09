namespace PTL.Contracts.Viewer;

public sealed record ViewerSaveResult(bool Success, ViewerResponse? Viewer, IReadOnlyDictionary<string, string[]> FieldErrors);
