using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.SystemMessage;
using PTL.Core.SystemMessage;

namespace PTL.Api.Controllers;

// Single shared admin-authored banner message (tblExtWebsiteMessage, fldMessageId = 0), not
// scoped to any user - see legacy ExternalWeb Home.aspx's "Important Message" placeholder, edited
// via ProficiencyTestingWeb/Admin/EditMessageOnWebsite.aspx (not yet migrated).
[ApiController]
[Route("api/system-messages")]
public sealed class SystemMessageController(ISystemMessageService systemMessageService) : ControllerBase
{
    [HttpGet("important")]
    public async Task<ActionResult<GetImportantMessageResponse>> GetImportantMessage(CancellationToken cancellationToken)
    {
        var message = await systemMessageService.GetImportantMessageAsync(cancellationToken);
        return Ok(new GetImportantMessageResponse(message));
    }
}
