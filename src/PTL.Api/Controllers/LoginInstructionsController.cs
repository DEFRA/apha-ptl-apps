using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.LoginInstructions;
using PTL.Core.LoginInstructions;

namespace PTL.Api.Controllers;

// Login instruction emails for Manage Participants / Manage Viewers / Manage External Test Consultants. The email goes to the
// address saved on the record, never to one supplied by the caller. Not yet protected with authorization, like the other
// System Administration controllers.
[ApiController]
[Route("api/login-instructions")]
public sealed class LoginInstructionsController(ILoginInstructionsService loginInstructionsService) : ControllerBase
{
    [HttpPost("participants/{participantId:guid}")]
    public Task<ActionResult<LoginInstructionsResponse>> SendToParticipant(Guid participantId, CancellationToken cancellationToken) =>
        SendAsync(LoginInstructionUserType.Participant, participantId, cancellationToken);

    [HttpPost("viewers/{viewerId:guid}")]
    public Task<ActionResult<LoginInstructionsResponse>> SendToViewer(Guid viewerId, CancellationToken cancellationToken) =>
        SendAsync(LoginInstructionUserType.Viewer, viewerId, cancellationToken);

    [HttpPost("external-test-consultants/{externalTestConsultantId:guid}")]
    public Task<ActionResult<LoginInstructionsResponse>> SendToExternalTestConsultant(Guid externalTestConsultantId, CancellationToken cancellationToken) =>
        SendAsync(LoginInstructionUserType.ExternalTestConsultant, externalTestConsultantId, cancellationToken);

    private async Task<ActionResult<LoginInstructionsResponse>> SendAsync(LoginInstructionUserType userType, Guid id, CancellationToken cancellationToken)
    {
        var result = await loginInstructionsService.SendAsync(userType, id, cancellationToken);

        return result.Outcome == LoginInstructionsOutcome.NotFound
            ? NotFound()
            : Ok(result.Outcome.ToResponse());
    }
}
