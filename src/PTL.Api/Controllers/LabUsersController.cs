using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.LabUsers;
using PTL.Contracts.LoginInstructions;
using PTL.Core.LabUsers;
using PTL.Core.LoginInstructions;

namespace PTL.Api.Controllers;

// A lab's primary contact managing the people who sign in to the lab (PT-LIMS external app, "Lab users"). The participant id
// in the path is the lab and is the only scope: every call reads or changes only that lab's links, so a lab user of another
// lab is simply not found. Like the other external-web endpoints it relies on the web app having authorised the caller as the
// lab's primary contact (policy PrimaryContact) and taking the participant id from their own signed-in session.
[ApiController]
[Route("api/participants/{participantId:guid}/lab-users")]
public sealed class LabUsersController(ILabUserService labUserService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<LabUserListResponse>> List(Guid participantId, CancellationToken cancellationToken)
    {
        var list = await labUserService.GetListAsync(participantId, cancellationToken);
        return list is null ? NotFound() : Ok(list);
    }

    [HttpGet("{labUserId:guid}")]
    public async Task<ActionResult<LabUserDetailsResponse>> Get(Guid participantId, Guid labUserId, CancellationToken cancellationToken)
    {
        var details = await labUserService.GetAsync(participantId, labUserId, cancellationToken);
        return details is null ? NotFound() : Ok(details);
    }

    [HttpPost]
    public async Task<ActionResult<LabUserSaveResult>> Add(Guid participantId, [FromBody] LabUserSaveRequest request, CancellationToken cancellationToken)
    {
        var result = await labUserService.AddAsync(participantId, request, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPut("{labUserId:guid}")]
    public async Task<ActionResult<LabUserSaveResult>> Update(Guid participantId, Guid labUserId, [FromBody] LabUserSaveRequest request, CancellationToken cancellationToken)
    {
        var result = await labUserService.UpdateAsync(participantId, labUserId, request, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("{labUserId:guid}/login-instructions")]
    public async Task<ActionResult<LoginInstructionsResponse>> SendLoginInstructions(Guid participantId, Guid labUserId, CancellationToken cancellationToken)
    {
        var outcome = await labUserService.SendLoginInstructionsAsync(participantId, labUserId, cancellationToken);
        return outcome is null or LoginInstructionsOutcome.NotFound ? NotFound() : Ok(outcome.Value.ToResponse());
    }
}
