namespace PTL.Core.Notifications;

/// <summary>
/// Builds the wire shape GOV.UK Notify requires for its "send a file by email" personalisation
/// value (docs/migration/invoice-migration.md). Notify has no raw MIME attachment concept - a file
/// is delivered by base64-encoding it into one personalisation value shaped exactly like
/// <see cref="NotifyFilePersonalisationValue"/>, sent through the same /v2/notifications/email
/// endpoint as any other templated email.
/// </summary>
public static class NotifyFileAttachment
{
    /// <summary>GOV.UK Notify's documented maximum file size for this feature.</summary>
    public const int MaxFileSizeBytes = 2 * 1024 * 1024;

    public static NotifyFilePersonalisationValue Build(byte[] content, string filename) =>
        new(file: Convert.ToBase64String(content), filename: filename, confirm_email_before_download: false, retention_period: null);
}

// Property names are snake_case to match GOV.UK Notify's documented JSON field names exactly -
// this type is a wire contract, not an internal model (same convention NotifyClient already uses
// for email_address/template_id).
public sealed record NotifyFilePersonalisationValue(
    string file,
    string filename,
    bool confirm_email_before_download,
    string? retention_period);
