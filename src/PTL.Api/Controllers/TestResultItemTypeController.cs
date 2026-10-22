using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.TestResultItemType;
using PTL.Core.TestResultItemType;

namespace PTL.Api.Controllers;

// [NEEDS INVESTIGATION] Not yet protected with authorization: authentication/authorization are
// out of scope for this phase, same as TestTypeController.
[ApiController]
[Route("api/test-result-item-types")]
public sealed class TestResultItemTypeController(ITestResultItemTypeService testResultItemTypeService) : ControllerBase
{
    // search matches legacy TextboxSearchResultItemName + ButtonSearchByResultItemName - when
    // non-blank, only items whose Name contains search (case-insensitive) are returned.
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TestResultItemTypeResponse>>> GetByYear([FromQuery] int yearId, [FromQuery] string? search, CancellationToken cancellationToken)
    {
        var items = await testResultItemTypeService.GetByYearAsync(yearId, search, cancellationToken);
        return Ok(items.Select(ToResponse).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<TestResultItemTypeResponse>> Create([FromBody] TestResultItemTypeSaveRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var created = await testResultItemTypeService.CreateAsync(request.Name, request.TestReturnValueTypeId, request.NoLongerInUse, request.YearId, cancellationToken);
            return Ok(ToResponse(created));
        }
        catch (TestResultItemTypeValidationException ex)
        {
            return ToValidationProblem(ex);
        }
    }

    [HttpPut("{testResultItemTypeId:guid}")]
    public async Task<ActionResult<TestResultItemTypeResponse>> Update(Guid testResultItemTypeId, [FromBody] TestResultItemTypeSaveRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var updated = await testResultItemTypeService.UpdateAsync(testResultItemTypeId, request.Name, request.TestReturnValueTypeId, request.NoLongerInUse, request.YearId, cancellationToken);
            return updated is null ? NotFound() : Ok(ToResponse(updated));
        }
        catch (TestResultItemTypeValidationException ex)
        {
            return ToValidationProblem(ex);
        }
        catch (TestResultItemTypeInUseException ex)
        {
            return Conflict(new TestResultItemTypeSaveResult(false, null, new Dictionary<string, string[]> { [string.Empty] = [ex.Message] }));
        }
    }

    [HttpPatch("{testResultItemTypeId:guid}/no-longer-in-use")]
    public async Task<ActionResult<TestResultItemTypeResponse>> SetNoLongerInUse(Guid testResultItemTypeId, [FromBody] TestResultItemTypeNoLongerInUseRequest request, CancellationToken cancellationToken)
    {
        var updated = await testResultItemTypeService.SetNoLongerInUseAsync(testResultItemTypeId, request.NoLongerInUse, request.YearId, cancellationToken);
        return updated is null ? NotFound() : Ok(ToResponse(updated));
    }

    // Returns 200 OK with Success=false (not 400/404/409) when removal is blocked - a legitimate
    // business outcome, not an invalid request, matching TestTypeController.Delete. yearId is
    // required because TestResultItemTypeService's delete semantics are year-scoped, not
    // unconditional - see TestResultItemTypeService.DeleteAsync.
    [HttpDelete("{testResultItemTypeId:guid}")]
    public async Task<ActionResult<TestResultItemTypeDeleteResponse>> Delete(Guid testResultItemTypeId, [FromQuery] int yearId, CancellationToken cancellationToken)
    {
        var result = await testResultItemTypeService.DeleteAsync(testResultItemTypeId, yearId, cancellationToken);
        return Ok(new TestResultItemTypeDeleteResponse(result.Success, result.Message));
    }

    private ActionResult ToValidationProblem(TestResultItemTypeValidationException ex)
    {
        foreach (var error in ex.Errors)
        {
            ModelState.AddModelError(error.Field, error.Message);
        }

        return ValidationProblem(ModelState);
    }

    private static TestResultItemTypeResponse ToResponse(PTL.Core.TestResultItemType.TestResultItemType item) =>
        new(item.TestResultItemTypeId, item.SharedId, item.Name, item.TestReturnValueTypeId, item.TestReturnValueTypeName, item.StartYearId, item.EndYearId, item.NoLongerInUse, item.ForeignInstances, item.ForeignInstancesString);
}
