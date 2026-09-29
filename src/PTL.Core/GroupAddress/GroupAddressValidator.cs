using System.Text.RegularExpressions;

namespace PTL.Core.GroupAddress;

public static partial class GroupAddressValidator
{
    [GeneratedRegex(@"^[ 0-9\+\-\(\)\*\#]*$", RegexOptions.None, 1000)]
    private static partial Regex PhonePattern();

    private static readonly Dictionary<string, string> FieldLabels = new()
    {
        [nameof(GroupAddress.Identifier)] = "Identifier",
        [nameof(GroupAddress.Address1)] = "Address 1",
        [nameof(GroupAddress.Address2)] = "Address 2",
        [nameof(GroupAddress.Address3)] = "Address 3",
        [nameof(GroupAddress.Address4)] = "Address 4",
        [nameof(GroupAddress.Address5)] = "Address 5",
        [nameof(GroupAddress.CountryId)] = "Country",
        [nameof(GroupAddress.Telephone)] = "Telephone",
        [nameof(GroupAddress.PackingInstructions)] = "Packing Instructions"
    };

    private static string Label(string field) => FieldLabels.GetValueOrDefault(field, field);

    public static GroupAddressValidationResult Validate(GroupAddress groupAddress)
    {
        var errors = new List<GroupAddressValidationError>();

        RequireNotEmpty(groupAddress.Identifier, nameof(GroupAddress.Identifier), errors);
        MaxLength(groupAddress.Identifier, 50, nameof(GroupAddress.Identifier), errors);

        RequireNotEmpty(groupAddress.Address1, nameof(GroupAddress.Address1), errors);
        MaxLength(groupAddress.Address1, 100, nameof(GroupAddress.Address1), errors);

        RequireNotEmpty(groupAddress.Address2, nameof(GroupAddress.Address2), errors);
        MaxLength(groupAddress.Address2, 100, nameof(GroupAddress.Address2), errors);

        MaxLength(groupAddress.Address3, 100, nameof(GroupAddress.Address3), errors);
        MaxLength(groupAddress.Address4, 100, nameof(GroupAddress.Address4), errors);
        MaxLength(groupAddress.Address5, 100, nameof(GroupAddress.Address5), errors);

        if (groupAddress.CountryId == Guid.Empty)
        {
            errors.Add(new GroupAddressValidationError(nameof(GroupAddress.CountryId), $"{Label(nameof(GroupAddress.CountryId))} must be selected"));
        }

        MaxLength(groupAddress.Telephone, 20, nameof(GroupAddress.Telephone), errors);
        RegexMatch(groupAddress.Telephone, PhonePattern(), nameof(GroupAddress.Telephone), errors);

        MaxLength(groupAddress.PackingInstructions, 500, nameof(GroupAddress.PackingInstructions), errors);

        return new GroupAddressValidationResult(errors.Count == 0, errors);
    }

    private static void RequireNotEmpty(string value, string field, List<GroupAddressValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(new GroupAddressValidationError(field, $"{Label(field)} is required"));
        }
    }

    private static void MaxLength(string value, int max, string field, List<GroupAddressValidationError> errors)
    {
        if (value is not null && value.Length > max)
        {
            errors.Add(new GroupAddressValidationError(field, $"{Label(field)} must not exceed {max} characters"));
        }
    }

    private static void RegexMatch(string value, Regex pattern, string field, List<GroupAddressValidationError> errors)
    {
        if (!string.IsNullOrEmpty(value) && !pattern.IsMatch(value))
        {
            errors.Add(new GroupAddressValidationError(field, $"{Label(field)} contains characters that are not allowed"));
        }
    }
}

public sealed record GroupAddressValidationResult(bool IsValid, IReadOnlyList<GroupAddressValidationError> Errors);
