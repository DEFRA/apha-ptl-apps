namespace PTL.Contracts.ExternalSiteMessage;

// Posted to PUT /api/external-site-message - matches legacy ButtonSave_Click (all three areas are
// published together in one save).
public sealed record ExternalSiteMessageSaveRequest(string Message, string ImportantMessage, string SupportEmailAddress);
