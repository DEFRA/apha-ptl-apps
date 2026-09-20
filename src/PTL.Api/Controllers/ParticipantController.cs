using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PTL.Contracts.Participant;
using PTL.Core.Participant;

namespace PTL.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class ParticipantController(IParticipantService participantService, ILogger<ParticipantController> logger) : ControllerBase
{
    private static readonly Action<ILogger, Guid, Exception?> LogParticipantNotFoundMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Information,
            new EventId(1, nameof(LogParticipantNotFoundMessage)),
            "Participant {ParticipantId} not found");

    [HttpGet("customers/{customerId:guid}/participants")]
    public async Task<ActionResult<IReadOnlyList<ParticipantSummaryResponse>>> GetParticipants(Guid customerId, [FromQuery] bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var participants = await participantService.GetParticipantsAsync(customerId, includeInactive, cancellationToken);
        return Ok(participants.Select(ToSummaryResponse).ToList());
    }

    [HttpGet("customers/{customerId:guid}/participants/search")]
    public async Task<ActionResult<ParticipantSearchResponse>> SearchParticipants(Guid customerId, [FromQuery] string? searchTerm, [FromQuery] bool includeInactive = false, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var result = await participantService.SearchParticipantsAsync(customerId, searchTerm, includeInactive, page, pageSize, cancellationToken);
        return Ok(new ParticipantSearchResponse(result.Items.Select(ToSummaryResponse).ToList(), result.TotalCount, page, pageSize));
    }

    [HttpGet("participants/{participantId:guid}")]
    public async Task<ActionResult<ParticipantResponse>> GetParticipant(Guid participantId, CancellationToken cancellationToken)
    {
        var participant = await participantService.GetParticipantAsync(participantId, cancellationToken);
        if (participant is null)
        {
            LogParticipantNotFoundMessage(logger, participantId, null);
            return NotFound();
        }

        return Ok(ToResponse(participant));
    }

    [HttpPost("participants")]
    public async Task<ActionResult<ParticipantResponse>> CreateParticipant([FromBody] CreateParticipantRequest request, CancellationToken cancellationToken)
    {
        var created = await participantService.CreateParticipantAsync(ToEntity(request), cancellationToken);
        return CreatedAtAction(nameof(GetParticipant), new { participantId = created.ParticipantId }, ToResponse(created));
    }

    [HttpPut("participants/{participantId:guid}")]
    public async Task<ActionResult<ParticipantResponse>> UpdateParticipant(Guid participantId, [FromBody] UpdateParticipantRequest request, CancellationToken cancellationToken)
    {
        var updated = await participantService.UpdateParticipantAsync(participantId, ToEntity(participantId, request), cancellationToken);
        return updated is null ? NotFound() : Ok(ToResponse(updated));
    }

    [HttpPost("participants/{participantId:guid}/deactivate")]
    public async Task<ActionResult<ParticipantResponse>> DeactivateParticipant(Guid participantId, CancellationToken cancellationToken)
    {
        var updated = await participantService.DeactivateParticipantAsync(participantId, cancellationToken);
        return updated is null ? NotFound() : Ok(ToResponse(updated));
    }

    [HttpPost("participants/{participantId:guid}/reactivate")]
    public async Task<ActionResult<ParticipantResponse>> ReactivateParticipant(Guid participantId, CancellationToken cancellationToken)
    {
        var updated = await participantService.ReactivateParticipantAsync(participantId, cancellationToken);
        return updated is null ? NotFound() : Ok(ToResponse(updated));
    }

    private static Participant ToEntity(CreateParticipantRequest request) => new()
    {
        ParticipantId = Guid.NewGuid(),
        CustomerId = request.CustomerId,
        SsoId = request.SsoId,
        LabCode = request.LabCode,
        LabName = request.LabName,
        LabTypeId = request.LabTypeId,
        ContactName = request.ContactName,
        Organisation = request.Organisation,
        Address1 = request.Address1,
        Address2 = request.Address2,
        Address3 = request.Address3,
        Address4 = request.Address4,
        Address5 = request.Address5,
        CountryId = request.CountryId,
        Telephone = request.Telephone,
        Fax = request.Fax,
        Email = request.Email,
        Email2 = request.Email2,
        Comments = request.Comments,
        IsActive = request.IsActive
    };

    private static Participant ToEntity(Guid participantId, UpdateParticipantRequest request) => new()
    {
        ParticipantId = participantId,
        CustomerId = request.CustomerId,
        SsoId = request.SsoId,
        LabCode = request.LabCode,
        LabName = request.LabName,
        LabTypeId = request.LabTypeId,
        ContactName = request.ContactName,
        Organisation = request.Organisation,
        Address1 = request.Address1,
        Address2 = request.Address2,
        Address3 = request.Address3,
        Address4 = request.Address4,
        Address5 = request.Address5,
        CountryId = request.CountryId,
        Telephone = request.Telephone,
        Fax = request.Fax,
        Email = request.Email,
        Email2 = request.Email2,
        Comments = request.Comments,
        IsActive = request.IsActive
    };

    private static ParticipantResponse ToResponse(Participant participant) => new(
        participant.ParticipantId,
        participant.SsoId,
        participant.CustomerId,
        participant.LabCode,
        participant.LabName,
        participant.LabTypeId,
        participant.ContactName,
        participant.Organisation,
        participant.Address1,
        participant.Address2,
        participant.Address3,
        participant.Address4,
        participant.Address5,
        participant.CountryId,
        participant.Telephone,
        participant.Fax,
        participant.Email,
        participant.Email2,
        participant.Comments,
        participant.IsActive,
        participant.InactiveDate,
        participant.InactiveError,
        participant.InactiveErrorDate);

    private static ParticipantSummaryResponse ToSummaryResponse(ParticipantSummaryEntity participant) => new(
        participant.ParticipantId,
        participant.CustomerId,
        participant.LabCode,
        participant.LabName,
        participant.ContactName,
        participant.IsActive);
}
