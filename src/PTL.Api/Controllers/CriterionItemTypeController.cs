using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.CriterionItemType;
using PTL.Core.CriterionItemType;

namespace PTL.Api.Controllers;

// [NEEDS INVESTIGATION] Not yet protected with authorization: authentication/authorization are
// out of scope for this phase, same as CategoryItemTypeController/TestTypeController.
[ApiController]
[Route("api/criterion-item-types")]
public sealed class CriterionItemTypeController(ICriterionItemTypeService criterionItemTypeService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CriterionItemTypeResponse>>> GetByYear([FromQuery] int yearId, CancellationToken cancellationToken)
    {
        var items = await criterionItemTypeService.GetByYearAsync(yearId, cancellationToken);
        return Ok(items.Select(ToResponse).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<CriterionItemTypeResponse>> Create([FromBody] CriterionItemTypeSaveRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var created = await criterionItemTypeService.CreateAsync(request.Name, request.NoLongerInUse, request.YearId, cancellationToken);
            return Ok(ToResponse(created));
        }
        catch (CriterionItemTypeValidationException ex)
        {
            return ToValidationProblem(ex);
        }
    }

    [HttpPut("{criterionItemTypeId:guid}")]
    public async Task<ActionResult<CriterionItemTypeResponse>> Update(Guid criterionItemTypeId, [FromBody] CriterionItemTypeSaveRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var updated = await criterionItemTypeService.UpdateAsync(criterionItemTypeId, request.Name, request.NoLongerInUse, request.YearId, cancellationToken);
            return updated is null ? NotFound() : Ok(ToResponse(updated));
        }
        catch (CriterionItemTypeValidationException ex)
        {
            return ToValidationProblem(ex);
        }
        catch (CriterionItemTypeInUseException ex)
        {
            return Conflict(new CriterionItemTypeSaveResult(false, null, new Dictionary<string, string[]> { [string.Empty] = [ex.Message] }));
        }
    }

    [HttpPatch("{criterionItemTypeId:guid}/no-longer-in-use")]
    public async Task<ActionResult<CriterionItemTypeResponse>> SetNoLongerInUse(Guid criterionItemTypeId, [FromBody] CriterionItemTypeNoLongerInUseRequest request, CancellationToken cancellationToken)
    {
        var updated = await criterionItemTypeService.SetNoLongerInUseAsync(criterionItemTypeId, request.NoLongerInUse, request.YearId, cancellationToken);
        return updated is null ? NotFound() : Ok(ToResponse(updated));
    }

    // Returns 200 OK with Success=false (not 400/404/409) when removal is blocked - a legitimate
    // business outcome, not an invalid request. yearId is required (like TestTypeController.Delete,
    // unlike CategoryItemTypeController.Delete) because the delete semantics are year-scoped.
    [HttpDelete("{criterionItemTypeId:guid}")]
    public async Task<ActionResult<CriterionItemTypeDeleteResponse>> Delete(Guid criterionItemTypeId, [FromQuery] int yearId, CancellationToken cancellationToken)
    {
        var result = await criterionItemTypeService.DeleteAsync(criterionItemTypeId, yearId, cancellationToken);
        return Ok(new CriterionItemTypeDeleteResponse(result.Success, result.Message));
    }

    private ActionResult ToValidationProblem(CriterionItemTypeValidationException ex)
    {
        foreach (var error in ex.Errors)
        {
            ModelState.AddModelError(error.Field, error.Message);
        }

        return ValidationProblem(ModelState);
    }

    private static CriterionItemTypeResponse ToResponse(PTL.Core.CriterionItemType.CriterionItemType item) =>
        new(item.CriterionItemTypeId, item.Name, item.StartYearId, item.EndYearId, item.NoLongerInUse, item.ForeignInstances, item.ForeignInstancesString);
}
