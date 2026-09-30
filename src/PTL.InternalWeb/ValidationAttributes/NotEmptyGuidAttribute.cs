using System.ComponentModel.DataAnnotations;

namespace PTL.InternalWeb.ValidationAttributes;

// Plain [Required] never fires for a bound Guid/Guid? dropdown value, because a "- Please Select -"
// option posts Guid.Empty (a non-null value), not an absent/null one. This attribute treats both
// null and Guid.Empty as "nothing selected", giving GOV.UK-friendly required-select feedback
// without needing a round trip through a PTL.Core validator for a purely primitive check.
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed class NotEmptyGuidAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) => value is Guid guid && guid != Guid.Empty;
}
