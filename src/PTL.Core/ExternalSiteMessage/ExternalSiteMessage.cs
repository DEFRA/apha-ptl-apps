namespace PTL.Core.ExternalSiteMessage;

// Keyless singleton projection for tblExtWebsiteMessage (legacy MainPageMessage) - the table only
// ever holds one row (fldMessageId = 0), so no id is modelled here.
public sealed class ExternalSiteMessage
{
    public string Message { get; set; } = string.Empty;
    public string ImportantMessage { get; set; } = string.Empty;
    public string SupportEmailAddress { get; set; } = string.Empty;
}
