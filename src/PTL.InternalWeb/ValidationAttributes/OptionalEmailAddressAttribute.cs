using System.ComponentModel.DataAnnotations;

namespace PTL.InternalWeb.ValidationAttributes;

// Plain [EmailAddress] fails on an empty string (only null is treated as valid), but ASP.NET Core
// model binding posts "" - not null - for a blank text input, so an optional email field would be
// wrongly flagged invalid on a normal empty submission. This treats null/blank the same way
// PTL.Core's format checks already do (skip when empty, validate only when a value is present).
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed class OptionalEmailAddressAttribute : ValidationAttribute
{
    private static readonly EmailAddressAttribute InnerAttribute = new();

    public override bool IsValid(object? value) =>
        value is not string text || string.IsNullOrWhiteSpace(text) || InnerAttribute.IsValid(value);
}
