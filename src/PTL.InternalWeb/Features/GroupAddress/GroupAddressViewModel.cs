using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using CoreGroupAddress = PTL.Core.GroupAddress.GroupAddress;

namespace PTL.InternalWeb.Features.GroupAddress;

public sealed class GroupAddressListViewModel(int page, int pageSize, int totalCount, IReadOnlyList<GroupAddressSummaryViewModel> items)
{
    public int Page { get; } = page;
    public int PageSize { get; } = pageSize;
    public int TotalCount { get; } = totalCount;
    public IReadOnlyList<GroupAddressSummaryViewModel> Items { get; } = items;
}

public sealed record GroupAddressSummaryViewModel(Guid GroupAddressId, string Identifier, string Address1);

public sealed record GroupAddressDetailsViewModel(PTL.Contracts.GroupAddress.GroupAddressResponse GroupAddress, string CountryName);

public sealed class GroupAddressFormViewModel : IValidatableObject
{
    public Guid GroupAddressId { get; set; }
    public string? Identifier { get; set; }
    public string? Address1 { get; set; }
    public string? Address2 { get; set; }
    public string? Address3 { get; set; }
    public string? Address4 { get; set; }
    public string? Address5 { get; set; }
    public Guid CountryId { get; set; }
    public string? Telephone { get; set; }
    public string? PackingInstructions { get; set; }
    public IReadOnlyList<SelectListItem> CountryOptions { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var groupAddress = new CoreGroupAddress
        {
            GroupAddressId = GroupAddressId,
            Identifier = Identifier ?? string.Empty,
            Address1 = Address1 ?? string.Empty,
            Address2 = Address2 ?? string.Empty,
            Address3 = Address3 ?? string.Empty,
            Address4 = Address4 ?? string.Empty,
            Address5 = Address5 ?? string.Empty,
            CountryId = CountryId,
            Telephone = Telephone ?? string.Empty,
            PackingInstructions = PackingInstructions ?? string.Empty
        };

        var result = PTL.Core.GroupAddress.GroupAddressValidator.Validate(groupAddress);
        foreach (var error in result.Errors)
        {
            yield return new ValidationResult(error.Message, [error.Field]);
        }
    }
}
