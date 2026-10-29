namespace PTL.InternalWeb.AuditReason;

// Content of the shared audit reason page (Views/Shared/AuditReasonPage.cshtml) - legacy Audit.aspx.
// PostUrl and Fields are filled in by AuditReasonControllerExtensions: Save re-posts every field of the
// edit being saved, plus the reason, to the same URL, so the action runs unchanged on the second request.
public sealed record AuditReasonPageViewModel
{
    // Legacy's "Changes have been made to the following object:" heading, e.g. "Scheme PT1234: Name".
    public required string AuditObjectName { get; init; }

    // What the user typed, kept when the page is shown again with an error.
    public string? AuditWhy { get; init; }

    // Where Cancel goes: the changes are discarded.
    public required string CancelUrl { get; init; }

    public string PostUrl { get; init; } = string.Empty;

    public IReadOnlyList<KeyValuePair<string, string>> Fields { get; init; } = [];
}

// The form fields the audit reason page owns. They are left out of the echoed fields so each is posted once.
public static class AuditReasonFlow
{
    public const string ViewName = "AuditReasonPage";

    public const string IsAuditStepField = "IsAuditStep";

    public const string AuditObjectNameField = "AuditObjectName";

    public const string AuditWhyField = "AuditWhy";
}
