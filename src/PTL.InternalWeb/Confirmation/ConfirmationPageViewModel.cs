namespace PTL.InternalWeb.Confirmation;

// Content of the shared GOV.UK confirmation page (Views/Shared/ConfirmationPage.cshtml) that replaces
// browser confirm() dialogs. PostUrl and Fields are filled in by ControllerConfirmationExtensions.
public sealed record ConfirmationPageViewModel
{
    public required string Title { get; init; }

    public string? Caption { get; init; }

    public string? Message { get; init; }

    public string? WarningText { get; init; }

    public string ConfirmText { get; init; } = "Confirm";

    public string CancelText { get; init; } = "Cancel";

    // Renders Confirm as a GOV.UK warning (red) button.
    public bool IsDestructive { get; init; }

    // Where Cancel returns to. Null means the page being confirmed holds unsaved state in its form, so
    // Cancel posts that form back with confirmation=cancel and the action redisplays it unchanged.
    public string? CancelUrl { get; init; }

    public string PostUrl { get; init; } = string.Empty;

    public IReadOnlyList<KeyValuePair<string, string>> Fields { get; init; } = [];
}

// The "confirmation" form value that drives the two-step flow: absent = ask, confirm = do it, cancel = back out.
public static class ConfirmationFlow
{
    public const string FieldName = "confirmation";

    public const string Confirmed = "confirm";

    public const string Cancelled = "cancel";

    public const string ViewName = "ConfirmationPage";

    public static bool IsConfirmed(string? confirmation) =>
        string.Equals(confirmation, Confirmed, StringComparison.Ordinal);

    public static bool IsCancelled(string? confirmation) =>
        string.Equals(confirmation, Cancelled, StringComparison.Ordinal);
}
