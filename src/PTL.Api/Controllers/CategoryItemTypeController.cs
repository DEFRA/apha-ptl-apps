using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.CategoryItemType;
using PTL.Core.CategoryItemType;

namespace PTL.Api.Controllers;

// [NEEDS INVESTIGATION] Not yet protected with authorization: authentication/authorization are
// out of scope for this phase, same as the System Administration controllers (see
// CountryController remarks).
[ApiController]
[Route("api/category-item-types")]
public sealed class CategoryItemTypeController(ICategoryItemTypeService categoryItemTypeService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CategoryItemTypeResponse>>> GetByYear([FromQuery] int yearId, CancellationToken cancellationToken)
    {
        var items = await categoryItemTypeService.GetByYearAsync(yearId, cancellationToken);
        return Ok(items.Select(ToResponse).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<CategoryItemTypeResponse>> Create([FromBody] CategoryItemTypeSaveRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var created = await categoryItemTypeService.CreateAsync(request.Name, request.NoLongerInUse, request.YearId, cancellationToken);
            return Ok(ToResponse(created));
        }
        catch (CategoryItemTypeValidationException ex)
        {
            return ToValidationProblem(ex);
        }
    }

    [HttpPut("{categoryItemTypeId:guid}")]
    public async Task<ActionResult<CategoryItemTypeResponse>> Update(Guid categoryItemTypeId, [FromBody] CategoryItemTypeSaveRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var updated = await categoryItemTypeService.UpdateAsync(categoryItemTypeId, request.Name, request.NoLongerInUse, request.YearId, cancellationToken);
            return updated is null ? NotFound() : Ok(ToResponse(updated));
        }
        catch (CategoryItemTypeValidationException ex)
        {
            return ToValidationProblem(ex);
        }
        catch (CategoryItemTypeInUseException ex)
        {
            return Conflict(new CategoryItemTypeSaveResult(false, null, new Dictionary<string, string[]> { [string.Empty] = [ex.Message] }));
        }
    }

    [HttpPatch("{categoryItemTypeId:guid}/no-longer-in-use")]
    public async Task<ActionResult<CategoryItemTypeResponse>> SetNoLongerInUse(Guid categoryItemTypeId, [FromBody] CategoryItemTypeNoLongerInUseRequest request, CancellationToken cancellationToken)
    {
        var updated = await categoryItemTypeService.SetNoLongerInUseAsync(categoryItemTypeId, request.NoLongerInUse, request.YearId, cancellationToken);
        return updated is null ? NotFound() : Ok(ToResponse(updated));
    }

    // Returns 200 OK with Success=false (not 400/404/409) when removal is blocked - a legitimate
    // business outcome, not an invalid request, matching CountryController.DeleteCountry.
    [HttpDelete("{categoryItemTypeId:guid}")]
    public async Task<ActionResult<CategoryItemTypeDeleteResponse>> Delete(Guid categoryItemTypeId, CancellationToken cancellationToken)
    {
        var result = await categoryItemTypeService.DeleteAsync(categoryItemTypeId, cancellationToken);
        return Ok(new CategoryItemTypeDeleteResponse(result.Success, result.Message));
    }

    private ActionResult ToValidationProblem(CategoryItemTypeValidationException ex)
    {
        foreach (var error in ex.Errors)
        {
            ModelState.AddModelError(error.Field, error.Message);
        }

        return ValidationProblem(ModelState);
    }

    private static CategoryItemTypeResponse ToResponse(PTL.Core.CategoryItemType.CategoryItemType item) =>
        new(item.CategoryItemTypeId, item.Name, item.StartYearId, item.EndYearId, item.NoLongerInUse, item.ForeignInstances, item.ForeignInstancesString);
}
