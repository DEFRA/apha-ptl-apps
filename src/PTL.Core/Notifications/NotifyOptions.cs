namespace PTL.Core.Notifications;

/// <summary>Connection settings for the shared GOV.UK Notify client - key and endpoint only.</summary>
public sealed class NotifyOptions
{
    public const string SectionName = "GovUkNotify";

    /// <summary>
    /// The full API key copied from the GOV.UK Notify portal, in its
    /// "{name}-{serviceId guid}-{secret guid}" form - never hardcoded, always from
    /// appsettings/environment variables/Secrets Manager.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://api.notifications.service.gov.uk";
}
