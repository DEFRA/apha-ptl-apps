using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace PTL.InternalWeb.Features.SystemAdministration;

public sealed class CountryManagementViewModel
{
    // Legacy held this list in ViewState until the page-level Save wrote it to the database; here it
    // is round-tripped through the page's form (Rows[i].*) instead.
    public List<CountryManagementRowViewModel> Rows { get; set; } = [];

    // Index into Rows of the one row in edit mode (legacy GridView.EditIndex), with the values typed
    // into it in Edit. Only applied to Rows by the row's own Save (the "update" command).
    public int? EditIndex { get; set; }

    public CountryManagementEditViewModel Edit { get; set; } = new();

    public IEnumerable<SelectListItem> CountryTypeOptions { get; set; } = [];

    // "Add a country" section at the bottom of the page - matches legacy's TextboxNewName/
    // NewDropDownCountryType/ButtonAdd.
    public CountryManagementAddViewModel Add { get; set; } = new();

    public string? Message { get; set; }

    // Set by the controller only - never taken from the posted form.
    [BindNever]
    public bool MessageIsError { get; set; }
}

public sealed class CountryManagementRowViewModel
{
    // Null for a country added on this page that has not been saved yet.
    public Guid? CountryId { get; set; }

    public string Country { get; set; } = string.Empty;

    public Guid CountryTypeId { get; set; }

    // Display name for CountryTypeId, resolved server-side (not posted back).
    public string CountryType { get; set; } = string.Empty;

    // Removed on this page but not yet deleted: the delete happens with the page-level Save.
    public bool IsRemoved { get; set; }

    public bool IsEditing { get; set; }
}

// Posted by the "Add" button at the bottom of the list - matches legacy ButtonAdd_Click.
public sealed class CountryManagementAddViewModel
{
    [Required(ErrorMessage = "Enter a country name")]
    [StringLength(50, ErrorMessage = "Country name must be 50 characters or fewer")]
    public string Country { get; set; } = string.Empty;

    [Required(ErrorMessage = "Select a country type")]
    public Guid? CountryTypeId { get; set; }
}

// Values typed into the row in edit mode - bound with an "Edit" prefix so its ModelState errors never
// bleed into the separate "Add a country" form on the same page, matching legacy's two WebForms
// ValidationGroups ("Edit" vs "New").
public sealed class CountryManagementEditViewModel
{
    [Required(ErrorMessage = "Enter a country name")]
    [StringLength(50, ErrorMessage = "Country name must be 50 characters or fewer")]
    public string Country { get; set; } = string.Empty;

    [Required(ErrorMessage = "Select a country type")]
    public Guid? CountryTypeId { get; set; }
}
