using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace PTL.InternalWeb.Features.SystemAdministration;

public sealed class CountryManagementViewModel
{
    public List<CountryManagementRowViewModel> Rows { get; set; } = [];

    public IEnumerable<SelectListItem> CountryTypeOptions { get; set; } = [];

    // "Add a new country" section at the bottom of the page - matches legacy's TextboxNewName/
    // NewDropDownCountryType/ButtonAdd.
    public CountryManagementAddViewModel Add { get; set; } = new();

    public string? Message { get; set; }

    public bool MessageIsError { get; set; }
}

public sealed class CountryManagementRowViewModel
{
    public Guid CountryId { get; set; }

    public string Country { get; set; } = string.Empty;

    public string CountryType { get; set; } = string.Empty;

    public Guid CountryTypeId { get; set; }

    // Number of Customer/Participant/GroupAddress records referencing this country - matches
    // legacy's AllocationCount, used to block Remove.
    public int AllocationCount { get; set; }

    // Only one row is ever in edit mode at a time - toggled via the editId query/route value,
    // same convention as Postage Pricing Plan.
    public bool IsEditing { get; set; }
}

// Posted by the single "Add" button at the bottom of the list - matches legacy ButtonAdd_Click.
public sealed class CountryManagementAddViewModel
{
    [Required(ErrorMessage = "Enter a country name")]
    [StringLength(50, ErrorMessage = "Country name must be 50 characters or fewer")]
    public string Country { get; set; } = string.Empty;

    [Required(ErrorMessage = "Select a country type")]
    public Guid? CountryTypeId { get; set; }
}

// Posted by the single "Save" button against whichever row is in edit mode - matches legacy
// GridViewCountry_Updating, which only ever updates the one row with EditIndex set. Bound with
// an "Edit" prefix (see Views/CountryManagement.cshtml) so its ModelState errors never bleed into
// the separate "Add a new country" form rendered on the same page - the modern equivalent of
// legacy's two WebForms ValidationGroups ("Edit" vs "New").
public sealed class CountryManagementEditViewModel
{
    [Required]
    public Guid? CountryId { get; set; }

    [Required(ErrorMessage = "Enter a country name")]
    [StringLength(50, ErrorMessage = "Country name must be 50 characters or fewer")]
    public string Country { get; set; } = string.Empty;

    [Required(ErrorMessage = "Select a country type")]
    public Guid? CountryTypeId { get; set; }
}
