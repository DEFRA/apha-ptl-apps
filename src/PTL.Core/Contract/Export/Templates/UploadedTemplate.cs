namespace PTL.Core.Contract.Export.Templates;

/// <summary>
/// A mail merge template row from <c>tblUploadedTemplate</c>. The file bytes themselves live in
/// object storage; this record only carries the metadata the legacy table holds.
/// </summary>
public sealed class UploadedTemplate
{
    public Guid FileId { get; set; }
    public string Filename { get; set; } = string.Empty;
    public DateTime UploadedDate { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public bool Selected { get; set; }
}
