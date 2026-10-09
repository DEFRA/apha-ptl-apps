namespace PTL.ExternalWeb.Features.Home;

/// <summary>View model for the legacy ViewInformation.aspx equivalent, showing
/// tblExtWebsiteMessage.fldMessage (null/empty when no message is currently published).</summary>
public sealed record InformationViewModel(string? MessageHtml);
