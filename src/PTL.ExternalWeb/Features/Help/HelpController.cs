using Microsoft.AspNetCore.Mvc;
using PTL.ExternalWeb.Features.Account;

namespace PTL.ExternalWeb.Features.Help;

// Legacy Help.aspx: each area of guidance is shown only to users who hold that area's role.
public sealed class HelpController : Controller
{
    public IActionResult Index() => View(new HelpViewModel(
        User.IsInRole(ExternalRoleNames.Participant),
        User.IsInRole(ExternalRoleNames.TestConsultant),
        User.IsInRole(ExternalRoleNames.Viewer)));
}

public sealed record HelpViewModel(bool ShowParticipant, bool ShowTestConsultant, bool ShowViewer);
