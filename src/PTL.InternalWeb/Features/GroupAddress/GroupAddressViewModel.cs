using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using PTL.InternalWeb.ValidationAttributes;

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

// Every GroupAddressValidator (PTL.Core) rule is an unconditional primitive check (required/length/
// select/format) with no cross-field, conditional, or domain logic, so DataAnnotations here fully
// mirror it - no IValidatableObject/Core delegation is needed. PTL.Core.GroupAddress.GroupAddressValidator
// remains the authoritative check enforced by the API (GroupAddressService), unchanged.
public sealed class GroupAddressFormViewModel
{
    public Guid? GroupAddressId { get; set; }

    [Required(ErrorMessage = "Enter an identifier")]
    [StringLength(50, ErrorMessage = "Identifier must not exceed 50 characters")]
    public string? Identifier { get; set; }

    [Required(ErrorMessage = "Enter address line 1")]
    [StringLength(100, ErrorMessage = "Address line 1 must not exceed 100 characters")]
    public string? Address1 { get; set; }

    [Required(ErrorMessage = "Enter address line 2")]
    [StringLength(100, ErrorMessage = "Address line 2 must not exceed 100 characters")]
    public string? Address2 { get; set; }

    [StringLength(100, ErrorMessage = "Address line 3 must not exceed 100 characters")]
    public string? Address3 { get; set; }

    [StringLength(100, ErrorMessage = "Address line 4 must not exceed 100 characters")]
    public string? Address4 { get; set; }

    [StringLength(100, ErrorMessage = "Address line 5 must not exceed 100 characters")]
    public string? Address5 { get; set; }

    [NotEmptyGuid(ErrorMessage = "Select a country")]
    public Guid? CountryId { get; set; }

    [StringLength(20, ErrorMessage = "Telephone must not exceed 20 characters")]
    [RegularExpression(@"^[ 0-9\+\-\(\)\*\#]*$", ErrorMessage = "Telephone contains characters that are not allowed")]
    public string? Telephone { get; set; }

    [StringLength(500, ErrorMessage = "Packing instructions must not exceed 500 characters")]
    public string? PackingInstructions { get; set; }

    public IReadOnlyList<SelectListItem> CountryOptions { get; set; } = [];
}
