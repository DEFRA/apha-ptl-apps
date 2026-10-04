namespace PTL.Contracts.Contract;

/// <summary>One row of the legacy Exports template grid: Filename, Upload Date, Open/Delete/Select.</summary>
public sealed record ExportTemplateResponse(
    Guid FileId,
    string Filename,
    DateTime UploadedDate,
    string DocumentType,
    bool Selected);

public sealed record ExportTemplateListResponse(
    string DocumentType,
    string DisplayName,
    IReadOnlyList<ExportTemplateResponse> Templates);

public sealed record ExportTemplateUploadResponse(
    bool Success,
    string? ErrorMessage,
    ExportTemplateResponse? Template);
