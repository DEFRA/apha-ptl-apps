using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.Participant;
using PTL.Core.Participant;

namespace PTL.Api.Controllers;

// Participant-scoped self-service endpoint (legacy EditParticipantDetails.aspx) - distinct from
// ParticipantController's unrestricted admin CRUD. ParticipantId path segment identifies which
// participant is editing their own record; CustomerId/LabCode are always resolved server-side,
// never accepted from the caller (see docs/ADR/0006-externalweb-participant-scoped-authorization-model.md).
[ApiController]
[Route("api/participant-details-edit")]
public sealed class ParticipantDetailsEditController(IParticipantDetailsEditService participantDetailsEditService) : ControllerBase
{
    [HttpGet("{participantId:guid}")]
    public async Task<ActionResult<ParticipantDetailsEditResponse>> Get(Guid participantId, CancellationToken cancellationToken)
    {
        var state = await participantDetailsEditService.GetAsync(participantId, cancellationToken);
        return state is null ? NotFound() : Ok(ToResponse(state));
    }

    [HttpPost("{participantId:guid}")]
    public async Task<ActionResult<ParticipantDetailsEditSaveResult>> Submit(
        Guid participantId, [FromBody] ParticipantDetailsEditSaveRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await participantDetailsEditService.SubmitAsync(participantId, ToEntity(request), cancellationToken);
            return Ok(new ParticipantDetailsEditSaveResult(true, new Dictionary<string, string[]>()));
        }
        catch (ParticipantValidationException ex)
        {
            var errors = ex.Errors
                .GroupBy(e => e.Field)
                .ToDictionary(g => g.Key, g => g.Select(e => e.Message).ToArray());
            return Ok(new ParticipantDetailsEditSaveResult(false, errors));
        }
        catch (PendingParticipantUpdateAlreadySubmittedException ex)
        {
            return Ok(new ParticipantDetailsEditSaveResult(false, new Dictionary<string, string[]> { [string.Empty] = [ex.Message] }));
        }
    }

    private static ParticipantDetailsEditResponse ToResponse(ParticipantDetailsEditState state) => new(
        state.LabCode,
        state.HasOutstandingUpdate,
        state.Values.ContactName,
        state.Values.Organisation,
        state.Values.Address1,
        state.Values.Address2,
        state.Values.Address3,
        state.Values.Address4,
        state.Values.Address5,
        state.Values.CountryId,
        state.Values.Telephone,
        state.Values.Email,
        state.Values.Email2);

    private static PendingParticipantUpdate ToEntity(ParticipantDetailsEditSaveRequest request) => new()
    {
        ContactName = request.ContactName,
        Organisation = request.Organisation,
        Address1 = request.Address1,
        Address2 = request.Address2,
        Address3 = request.Address3,
        Address4 = request.Address4,
        Address5 = request.Address5,
        CountryId = request.CountryId,
        Telephone = request.Telephone,
        Email = request.Email,
        Email2 = request.Email2
    };
}
