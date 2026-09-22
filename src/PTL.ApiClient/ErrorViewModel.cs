namespace PTL.ApiClient;

/// <summary>
/// Shared error-page view model for every PTL web front-end (PTL.InternalWeb,
/// PTL.ExternalWeb, ...) - identical for each, so it lives here rather than being
/// duplicated per app.
/// </summary>
public class ErrorViewModel
{
    public string? RequestId { get; set; }

    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
}
