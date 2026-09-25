using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.Contract;
using PTL.Core.Contract.ImportPermit;

namespace PTL.Api.Controllers;

// [NEEDS INVESTIGATION] Not yet protected with authorization - see ContractController's identical note.
[ApiController]
[Route("api")]
public sealed class ImportPermitController(IImportPermitService importPermitService) : ControllerBase
{
    // GET /api/contracts/{contractId}/import-permits - see ImportPermit.aspx.
    [HttpGet("contracts/{contractId:guid}/import-permits")]
    public async Task<ActionResult<IReadOnlyList<ImportPermitResponse>>> GetImportPermits(Guid contractId, CancellationToken cancellationToken)
    {
        var permits = await importPermitService.GetByContractIdAsync(contractId, cancellationToken);
        return Ok(permits.Select(ToResponse).ToList());
    }

    // PUT /api/contracts/import-permits/{participantSchemeId} - matches legacy btnApply_Click/
    // btnSave_Click looping ImportPermitDataAccess.UpdateImportPermit per row.
    [HttpPut("contracts/import-permits/{participantSchemeId:guid}")]
    public async Task<IActionResult> UpdateImportPermit(Guid participantSchemeId, [FromBody] UpdateImportPermitRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await importPermitService.UpdateAsync(participantSchemeId, request.ImportPermitReceived, request.ImportPermitExpiry, cancellationToken);
            return NoContent();
        }
        catch (ImportPermitValidationException ex)
        {
            ModelState.AddModelError(nameof(request.ImportPermitExpiry), ex.Message);
            return ValidationProblem(ModelState);
        }
    }

    private static ImportPermitResponse ToResponse(ImportPermitEntity entity) => new(
        entity.ParticipantSchemeId,
        entity.SchemeNumber,
        entity.SchemeName,
        entity.LabId,
        entity.ImportPermitRequired,
        entity.ImportPermitReceived,
        entity.ImportPermitExpiry);
}
