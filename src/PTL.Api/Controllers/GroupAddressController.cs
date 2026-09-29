using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.GroupAddress;
using PTL.Core.GroupAddress;

namespace PTL.Api.Controllers;

[ApiController]
[Route("api/group-addresses")]
public sealed class GroupAddressController(IGroupAddressService groupAddressService, ILogger<GroupAddressController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<GroupAddressResponse>>> GetGroupAddresses(CancellationToken cancellationToken)
    {
        var groupAddresses = await groupAddressService.GetAllAsync(cancellationToken);
        return Ok(groupAddresses.Select(ToResponse).ToList());
    }

    // GET /api/group-addresses/search?page=1&pageSize=20
    [HttpGet("search")]
    public async Task<ActionResult<GroupAddressSearchResponse>> SearchGroupAddresses([FromQuery] GroupAddressSearchRequest request, CancellationToken cancellationToken)
    {
        var result = await groupAddressService.SearchAsync(request.Page, request.PageSize, cancellationToken);
        return Ok(new GroupAddressSearchResponse(result.Items.Select(ToResponse).ToList(), result.TotalCount, request.Page, request.PageSize));
    }

    [HttpGet("{groupAddressId:guid}")]
    public async Task<ActionResult<GroupAddressResponse>> GetGroupAddress(Guid groupAddressId, CancellationToken cancellationToken)
    {
        var groupAddress = await groupAddressService.GetByIdAsync(groupAddressId, cancellationToken);
        if (groupAddress is null)
        {
            return NotFound();
        }

        return Ok(ToResponse(groupAddress));
    }

    [HttpPost]
    public async Task<ActionResult<GroupAddressResponse>> CreateGroupAddress([FromBody] GroupAddressSaveRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var created = await groupAddressService.CreateAsync(ToEntity(Guid.Empty, request), cancellationToken);
            return CreatedAtAction(nameof(GetGroupAddress), new { groupAddressId = created.GroupAddressId }, ToResponse(created));
        }
        catch (GroupAddressValidationException ex)
        {
            return ToValidationProblem(ex);
        }
    }

    [HttpPut("{groupAddressId:guid}")]
    public async Task<ActionResult<GroupAddressResponse>> UpdateGroupAddress(Guid groupAddressId, [FromBody] GroupAddressSaveRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var updated = await groupAddressService.UpdateAsync(groupAddressId, ToEntity(groupAddressId, request), cancellationToken);
            return updated is null ? NotFound() : Ok(ToResponse(updated));
        }
        catch (GroupAddressValidationException ex)
        {
            return ToValidationProblem(ex);
        }
    }

    private ActionResult ToValidationProblem(GroupAddressValidationException ex)
    {
        foreach (var error in ex.Errors)
        {
            ModelState.AddModelError(error.Field, error.Message);
        }

        return ValidationProblem(ModelState);
    }

    private static PTL.Core.GroupAddress.GroupAddress ToEntity(Guid groupAddressId, GroupAddressSaveRequest request) => new()
    {
        GroupAddressId = groupAddressId,
        Identifier = request.Identifier,
        Address1 = request.Address1,
        Address2 = request.Address2,
        Address3 = request.Address3,
        Address4 = request.Address4,
        Address5 = request.Address5,
        CountryId = request.CountryId,
        Telephone = request.Telephone,
        PackingInstructions = request.PackingInstructions
    };

    private static GroupAddressResponse ToResponse(PTL.Core.GroupAddress.GroupAddress groupAddress) => new(
        groupAddress.GroupAddressId,
        groupAddress.Identifier,
        groupAddress.Address1,
        groupAddress.Address2,
        groupAddress.Address3,
        groupAddress.Address4,
        groupAddress.Address5,
        groupAddress.CountryId,
        groupAddress.Telephone,
        groupAddress.PackingInstructions);
}
