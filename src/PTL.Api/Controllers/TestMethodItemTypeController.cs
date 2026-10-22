using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.TestMethodItemType;
using PTL.Core.TestMethodItemType;

namespace PTL.Api.Controllers;

// [NEEDS INVESTIGATION] Not yet protected with authorization: authentication/authorization are
// out of scope for this phase, same as TestResultItemTypeController.
[ApiController]
[Route("api/test-method-item-types")]
public sealed class TestMethodItemTypeController(ITestMethodItemTypeService testMethodItemTypeService) : ControllerBase
{
    // search matches legacy TextboxSearchMethodItemName + ButtonSearchByMethodItemName - when
    // non-blank, only items whose Name contains search (case-insensitive) are returned.
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TestMethodItemTypeResponse>>> GetByYear([FromQuery] int yearId, [FromQuery] string? search, CancellationToken cancellationToken)
    {
        var items = await testMethodItemTypeService.GetByYearAsync(yearId, search, cancellationToken);
        return Ok(items.Select(ToResponse).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<TestMethodItemTypeResponse>> Create([FromBody] TestMethodItemTypeSaveRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var created = await testMethodItemTypeService.CreateAsync(request.Name, request.TestReturnValueTypeId, request.NoLongerInUse, request.YearId, cancellationToken);
            return Ok(ToResponse(created));
        }
        catch (TestMethodItemTypeValidationException ex)
        {
            return ToValidationProblem(ex);
        }
    }

    [HttpPut("{testMethodItemTypeId:guid}")]
    public async Task<ActionResult<TestMethodItemTypeResponse>> Update(Guid testMethodItemTypeId, [FromBody] TestMethodItemTypeSaveRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var updated = await testMethodItemTypeService.UpdateAsync(testMethodItemTypeId, request.Name, request.TestReturnValueTypeId, request.NoLongerInUse, request.YearId, cancellationToken);
            return updated is null ? NotFound() : Ok(ToResponse(updated));
        }
        catch (TestMethodItemTypeValidationException ex)
        {
            return ToValidationProblem(ex);
        }
        catch (TestMethodItemTypeInUseException ex)
        {
            return Conflict(new TestMethodItemTypeSaveResult(false, null, new Dictionary<string, string[]> { [string.Empty] = [ex.Message] }));
        }
    }

    [HttpPatch("{testMethodItemTypeId:guid}/no-longer-in-use")]
    public async Task<ActionResult<TestMethodItemTypeResponse>> SetNoLongerInUse(Guid testMethodItemTypeId, [FromBody] TestMethodItemTypeNoLongerInUseRequest request, CancellationToken cancellationToken)
    {
        var updated = await testMethodItemTypeService.SetNoLongerInUseAsync(testMethodItemTypeId, request.NoLongerInUse, request.YearId, cancellationToken);
        return updated is null ? NotFound() : Ok(ToResponse(updated));
    }

    // Returns 200 OK with Success=false (not 400/404/409) when removal is blocked - a legitimate
    // business outcome, not an invalid request, matching TestResultItemTypeController.Delete.
    // yearId is required because TestMethodItemTypeService's delete semantics are year-scoped,
    // not unconditional - see TestMethodItemTypeService.DeleteAsync.
    [HttpDelete("{testMethodItemTypeId:guid}")]
    public async Task<ActionResult<TestMethodItemTypeDeleteResponse>> Delete(Guid testMethodItemTypeId, [FromQuery] int yearId, CancellationToken cancellationToken)
    {
        var result = await testMethodItemTypeService.DeleteAsync(testMethodItemTypeId, yearId, cancellationToken);
        return Ok(new TestMethodItemTypeDeleteResponse(result.Success, result.Message));
    }

    private ActionResult ToValidationProblem(TestMethodItemTypeValidationException ex)
    {
        foreach (var error in ex.Errors)
        {
            ModelState.AddModelError(error.Field, error.Message);
        }

        return ValidationProblem(ModelState);
    }

    private static TestMethodItemTypeResponse ToResponse(PTL.Core.TestMethodItemType.TestMethodItemType item) =>
        new(item.TestMethodItemTypeId, item.SharedId, item.Name, item.TestReturnValueTypeId, item.TestReturnValueTypeName, item.StartYearId, item.EndYearId, item.NoLongerInUse, item.ForeignInstances, item.ForeignInstancesString);
}
