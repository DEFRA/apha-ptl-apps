using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.TestType;
using PTL.Core.TestType;

namespace PTL.Api.Controllers;

// [NEEDS INVESTIGATION] Not yet protected with authorization: authentication/authorization are
// out of scope for this phase, same as CategoryItemTypeController.
[ApiController]
[Route("api/test-types")]
public sealed class TestTypeController(ITestTypeService testTypeService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TestTypeResponse>>> GetByYear([FromQuery] int yearId, CancellationToken cancellationToken)
    {
        var items = await testTypeService.GetByYearAsync(yearId, cancellationToken);
        return Ok(items.Select(ToResponse).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<TestTypeResponse>> Create([FromBody] TestTypeSaveRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var created = await testTypeService.CreateAsync(request.Name, request.NoLongerInUse, request.YearId, cancellationToken);
            return Ok(ToResponse(created));
        }
        catch (TestTypeValidationException ex)
        {
            return ToValidationProblem(ex);
        }
    }

    [HttpPut("{testTypeId:guid}")]
    public async Task<ActionResult<TestTypeResponse>> Update(Guid testTypeId, [FromBody] TestTypeSaveRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var updated = await testTypeService.UpdateAsync(testTypeId, request.Name, request.NoLongerInUse, request.YearId, cancellationToken);
            return updated is null ? NotFound() : Ok(ToResponse(updated));
        }
        catch (TestTypeValidationException ex)
        {
            return ToValidationProblem(ex);
        }
        catch (TestTypeInUseException ex)
        {
            return Conflict(new TestTypeSaveResult(false, null, new Dictionary<string, string[]> { [string.Empty] = [ex.Message] }));
        }
    }

    [HttpPatch("{testTypeId:guid}/no-longer-in-use")]
    public async Task<ActionResult<TestTypeResponse>> SetNoLongerInUse(Guid testTypeId, [FromBody] TestTypeNoLongerInUseRequest request, CancellationToken cancellationToken)
    {
        var updated = await testTypeService.SetNoLongerInUseAsync(testTypeId, request.NoLongerInUse, request.YearId, cancellationToken);
        return updated is null ? NotFound() : Ok(ToResponse(updated));
    }

    // Returns 200 OK with Success=false (not 400/404/409) when removal is blocked - a legitimate
    // business outcome, not an invalid request, matching CategoryItemTypeController.Delete.
    // yearId is required here (unlike CategoryItemTypeController.Delete) because TestTypeService's
    // delete semantics are year-scoped, not unconditional - see TestTypeService.DeleteAsync.
    [HttpDelete("{testTypeId:guid}")]
    public async Task<ActionResult<TestTypeDeleteResponse>> Delete(Guid testTypeId, [FromQuery] int yearId, CancellationToken cancellationToken)
    {
        var result = await testTypeService.DeleteAsync(testTypeId, yearId, cancellationToken);
        return Ok(new TestTypeDeleteResponse(result.Success, result.Message));
    }

    private ActionResult ToValidationProblem(TestTypeValidationException ex)
    {
        foreach (var error in ex.Errors)
        {
            ModelState.AddModelError(error.Field, error.Message);
        }

        return ValidationProblem(ModelState);
    }

    private static TestTypeResponse ToResponse(PTL.Core.TestType.TestType item) =>
        new(item.TestTypeId, item.SharedId, item.Name, item.StartYearId, item.EndYearId, item.NoLongerInUse, item.ForeignInstances, item.ForeignInstancesString);
}
