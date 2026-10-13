using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace PTL.InternalWeb.Features.SystemAdministration;

public sealed class CreateUserViewModel
{
    [Required(ErrorMessage = "Enter an Employee Number, Forename or Surname")]
    public string SearchTerm { get; set; } = string.Empty;

    // Only set once a search has actually been performed - distinguishes "haven't searched yet"
    // from "searched and found nothing", matching legacy's SearchBox/SelectUser view split.
    // Never posted by the view - excluded from binding so a crafted form post can't fake it.
    [BindNever]
    public bool HasSearched { get; set; }

    public List<SelectListItem> ResultOptions { get; set; } = [];

    // Packed "username|email|friendlyname|firstname|lastname" value of the chosen dropdown item -
    // matches legacy DropDownList_Users.SelectedValue's packing.
    public string? SelectedCandidate { get; set; }

    [StringLength(50, ErrorMessage = "Department must be 50 characters or fewer")]
    public string Department { get; set; } = string.Empty;

    public string? Message { get; set; }

    // MessageIsError/IsCreated are server-computed display state, never posted by the view -
    // excluded from binding so a crafted form post can't fake a success state.
    [BindNever]
    public bool MessageIsError { get; set; }

    [BindNever]
    public bool IsCreated { get; set; }
}
