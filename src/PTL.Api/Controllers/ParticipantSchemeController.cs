using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PTL.Contracts.Participant;
using PTL.Core.Participant;

namespace PTL.Api.Controllers;

// [NEEDS INVESTIGATION] Not yet protected with authorization - see ContractController's identical note.
[ApiController]
[Route("api")]
public sealed class ParticipantSchemeController(IParticipantSchemeService participantSchemeService, ILogger<ParticipantSchemeController> logger) : ControllerBase
{
    private static readonly Action<ILogger, Guid, Exception?> LogNotFoundMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Information,
            new EventId(1, nameof(LogNotFoundMessage)),
            "Participant scheme {ParticipantSchemeId} not found");

    [HttpGet("participant-schemes/{participantSchemeId:guid}")]
    public async Task<ActionResult<ParticipantSchemeResponse>> GetParticipantScheme(Guid participantSchemeId, CancellationToken cancellationToken)
    {
        var participantScheme = await participantSchemeService.GetParticipantSchemeAsync(participantSchemeId, cancellationToken);
        if (participantScheme is null)
        {
            LogNotFoundMessage(logger, participantSchemeId, null);
            return NotFound();
        }

        return Ok(ToResponse(participantScheme));
    }

    [HttpPost("participant-schemes")]
    public async Task<ActionResult<ParticipantSchemeResponse>> CreateParticipantScheme([FromBody] CreateParticipantSchemeRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var created = await participantSchemeService.CreateParticipantSchemeAsync(ToEntity(Guid.Empty, request), cancellationToken);
            return CreatedAtAction(nameof(GetParticipantScheme), new { participantSchemeId = created.ParticipantSchemeId }, ToResponse(created));
        }
        catch (ParticipantSchemeValidationException ex)
        {
            return ToValidationProblem(ex);
        }
    }

    [HttpPut("participant-schemes/{participantSchemeId:guid}")]
    public async Task<ActionResult<ParticipantSchemeResponse>> UpdateParticipantScheme(Guid participantSchemeId, [FromBody] UpdateParticipantSchemeRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var updated = await participantSchemeService.UpdateParticipantSchemeAsync(participantSchemeId, ToEntity(participantSchemeId, request), cancellationToken);
            return updated is null ? NotFound() : Ok(ToResponse(updated));
        }
        catch (ParticipantSchemeValidationException ex)
        {
            return ToValidationProblem(ex);
        }
    }

    [HttpDelete("participant-schemes/{participantSchemeId:guid}")]
    public async Task<IActionResult> DeleteParticipantScheme(Guid participantSchemeId, CancellationToken cancellationToken)
    {
        try
        {
            var removed = await participantSchemeService.DeleteParticipantSchemeAsync(participantSchemeId, cancellationToken);
            return removed ? NoContent() : NotFound();
        }
        catch (ParticipantSchemeValidationException ex)
        {
            return ToValidationProblem(ex);
        }
    }

    private ActionResult ToValidationProblem(ParticipantSchemeValidationException ex)
    {
        foreach (var error in ex.Errors)
        {
            ModelState.AddModelError(error.Field, error.Message);
        }

        return ValidationProblem(ModelState);
    }

    private static ParticipantSchemeRecord ToEntity(Guid participantSchemeId, CreateParticipantSchemeRequest request) => new()
    {
        ParticipantSchemeId = participantSchemeId,
        ContractId = request.ContractId,
        ParticipantId = request.ParticipantId,
        SchemeId = request.SchemeId,
        DistributionMonthJan = request.DistributionMonthJan,
        DistributionMonthFeb = request.DistributionMonthFeb,
        DistributionMonthMar = request.DistributionMonthMar,
        DistributionMonthApr = request.DistributionMonthApr,
        DistributionMonthMay = request.DistributionMonthMay,
        DistributionMonthJun = request.DistributionMonthJun,
        DistributionMonthJul = request.DistributionMonthJul,
        DistributionMonthAug = request.DistributionMonthAug,
        DistributionMonthSep = request.DistributionMonthSep,
        DistributionMonthOct = request.DistributionMonthOct,
        DistributionMonthNov = request.DistributionMonthNov,
        DistributionMonthDec = request.DistributionMonthDec,
        NumberOfSetsRequired = request.NumberOfSetsRequired,
        ExternalReference = request.ExternalReference,
        Contact = request.Contact,
        ImportExportLicenceRequired = request.ImportExportLicenceRequired,
        CustomsCertificateRequired = request.CustomsCertificateRequired,
        NonFeePaying = request.NonFeePaying,
        PackingInstructions = request.PackingInstructions,
        IsWeightedPricing = request.IsWeightedPricing,
        DataConsentDeclarationGiven = request.DataConsentDeclarationGiven,
        IsOverrideJan = request.IsOverrideJan,
        IsOverrideFeb = request.IsOverrideFeb,
        IsOverrideMar = request.IsOverrideMar,
        IsOverrideApr = request.IsOverrideApr,
        IsOverrideMay = request.IsOverrideMay,
        IsOverrideJun = request.IsOverrideJun,
        IsOverrideJul = request.IsOverrideJul,
        IsOverrideAug = request.IsOverrideAug,
        IsOverrideSep = request.IsOverrideSep,
        IsOverrideOct = request.IsOverrideOct,
        IsOverrideNov = request.IsOverrideNov,
        IsOverrideDec = request.IsOverrideDec,
        GroupAddressId = request.GroupAddressId
    };

    private static ParticipantSchemeRecord ToEntity(Guid participantSchemeId, UpdateParticipantSchemeRequest request) => new()
    {
        ParticipantSchemeId = participantSchemeId,
        DistributionMonthJan = request.DistributionMonthJan,
        DistributionMonthFeb = request.DistributionMonthFeb,
        DistributionMonthMar = request.DistributionMonthMar,
        DistributionMonthApr = request.DistributionMonthApr,
        DistributionMonthMay = request.DistributionMonthMay,
        DistributionMonthJun = request.DistributionMonthJun,
        DistributionMonthJul = request.DistributionMonthJul,
        DistributionMonthAug = request.DistributionMonthAug,
        DistributionMonthSep = request.DistributionMonthSep,
        DistributionMonthOct = request.DistributionMonthOct,
        DistributionMonthNov = request.DistributionMonthNov,
        DistributionMonthDec = request.DistributionMonthDec,
        NumberOfSetsRequired = request.NumberOfSetsRequired,
        ExternalReference = request.ExternalReference,
        Contact = request.Contact,
        ImportExportLicenceRequired = request.ImportExportLicenceRequired,
        CustomsCertificateRequired = request.CustomsCertificateRequired,
        NonFeePaying = request.NonFeePaying,
        PackingInstructions = request.PackingInstructions,
        IsWeightedPricing = request.IsWeightedPricing,
        DataConsentDeclarationGiven = request.DataConsentDeclarationGiven,
        IsOverrideJan = request.IsOverrideJan,
        IsOverrideFeb = request.IsOverrideFeb,
        IsOverrideMar = request.IsOverrideMar,
        IsOverrideApr = request.IsOverrideApr,
        IsOverrideMay = request.IsOverrideMay,
        IsOverrideJun = request.IsOverrideJun,
        IsOverrideJul = request.IsOverrideJul,
        IsOverrideAug = request.IsOverrideAug,
        IsOverrideSep = request.IsOverrideSep,
        IsOverrideOct = request.IsOverrideOct,
        IsOverrideNov = request.IsOverrideNov,
        IsOverrideDec = request.IsOverrideDec,
        GroupAddressId = request.GroupAddressId
    };

    private static ParticipantSchemeResponse ToResponse(ParticipantSchemeRecord record) => new(
        record.ParticipantSchemeId,
        record.ContractId,
        record.ParticipantId,
        record.SchemeId,
        record.DistributionMonthJan,
        record.DistributionMonthFeb,
        record.DistributionMonthMar,
        record.DistributionMonthApr,
        record.DistributionMonthMay,
        record.DistributionMonthJun,
        record.DistributionMonthJul,
        record.DistributionMonthAug,
        record.DistributionMonthSep,
        record.DistributionMonthOct,
        record.DistributionMonthNov,
        record.DistributionMonthDec,
        record.CanEditJan,
        record.CanEditFeb,
        record.CanEditMar,
        record.CanEditApr,
        record.CanEditMay,
        record.CanEditJun,
        record.CanEditJul,
        record.CanEditAug,
        record.CanEditSep,
        record.CanEditOct,
        record.CanEditNov,
        record.CanEditDec,
        record.NumberOfSetsRequired,
        record.ExternalReference,
        record.Contact,
        record.IsRemoved,
        record.ImportExportLicenceRequired,
        record.CustomsCertificateRequired,
        record.NonFeePaying,
        record.PackingInstructions,
        record.IsWeightedPricing,
        record.DataConsentDeclarationGiven,
        record.IsOverrideJan,
        record.IsOverrideFeb,
        record.IsOverrideMar,
        record.IsOverrideApr,
        record.IsOverrideMay,
        record.IsOverrideJun,
        record.IsOverrideJul,
        record.IsOverrideAug,
        record.IsOverrideSep,
        record.IsOverrideOct,
        record.IsOverrideNov,
        record.IsOverrideDec,
        record.Price,
        record.ParticipantDisplayName,
        record.SchemeDisplayName,
        record.GroupAddressId);
}
