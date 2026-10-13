namespace PTL.Contracts.ExternalSiteMessage;

// Public API contract for GET/PUT /api/external-site-message - a mutable singleton (legacy
// tblExtWebsiteMessage, always exactly one row) backing the "External Site Management" admin
// screen. Further Information/Important Message are rich-text HTML, sanitised server-side.
public sealed record ExternalSiteMessageResponse(string Message, string ImportantMessage, string SupportEmailAddress);
