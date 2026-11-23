using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.ExternalSiteMessage;
using PTL.Core.ExternalSiteMessage;

namespace PTL.Api.Controllers;

// [NEEDS INVESTIGATION] Not yet protected with authorization: authentication/authorization are
// out of scope for this phase, same as the other System Administration controllers.
//
// Admin-publish side. PTL.ExternalWeb's error pages also read the support email from GET (legacy
// ErrorGeneric.aspx/ErrorPageNotFound.aspx); its Home banner and Information page use api/system-messages.
[ApiController]
[Route("api/external-site-message")]
public sealed class ExternalSiteMessageController(IExternalSiteMessageService externalSiteMessageService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ExternalSiteMessageResponse>> GetExternalSiteMessage(CancellationToken cancellationToken)
    {
        var message = await externalSiteMessageService.GetAsync(cancellationToken);
        return Ok(ToResponse(message));
    }

    [HttpPut]
    public async Task<ActionResult<ExternalSiteMessageResponse>> UpdateExternalSiteMessage([FromBody] ExternalSiteMessageSaveRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var updated = await externalSiteMessageService.UpdateAsync(request.Message, request.ImportantMessage, request.SupportEmailAddress, cancellationToken);
            return Ok(ToResponse(updated));
        }
        catch (ExternalSiteMessageValidationException ex)
        {
            foreach (var error in ex.Errors)
            {
                ModelState.AddModelError(error.Field, error.Message);
            }

            return ValidationProblem(ModelState);
        }
    }

    private static ExternalSiteMessageResponse ToResponse(PTL.Core.ExternalSiteMessage.ExternalSiteMessage message) =>
        new(message.Message, message.ImportantMessage, message.SupportEmailAddress);
}
