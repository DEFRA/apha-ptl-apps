using System.ComponentModel.DataAnnotations;

namespace PTL.InternalWeb.Features.SystemAdministration;

public sealed class ExternalSiteManagementViewModel
{
    // Further Information - no validation at all, matching legacy (TxtMessage on
    // EditMessageOnWebsite.aspx carries no validators despite the varchar(8000) column).
    public string Message { get; set; } = string.Empty;

    // Visible-character limit (500, after stripping HTML tags) is HTML-aware and so is owned by
    // PTL.Core.ExternalSiteMessage.ExternalSiteMessageValidator, not a DataAnnotation here - a
    // plain StringLength would count raw markup, not visible text.
    public string ImportantMessage { get; set; } = string.Empty;

    [Required(ErrorMessage = "The Email Address is required")]
    [RegularExpression(@"^[a-zA-Z0-9'._%-]+@[a-zA-Z0-9.-]+\.[a-zA-Z0-9-]{2,}$", ErrorMessage = "The Email Address is invalid")]
    public string SupportEmailAddress { get; set; } = string.Empty;
}
