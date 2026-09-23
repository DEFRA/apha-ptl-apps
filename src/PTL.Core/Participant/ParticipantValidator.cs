namespace PTL.Core.Participant;

// Mirrors the domain validation pattern used by CustomerValidator, SchemeValidator, and
// ContractValidator. This keeps participant rules centralized in PTL.Core and preserves
// user-friendly labels instead of exposing raw PascalCase property names in the UI.
public static class ParticipantValidator
{
    private static readonly Dictionary<string, string> FieldLabels = new()
    {
        [nameof(Participant.LabCode)] = "Lab code",
        [nameof(Participant.LabName)] = "Lab name",
        [nameof(Participant.ContactName)] = "Contact name",
        [nameof(Participant.Organisation)] = "Organisation name",
        [nameof(Participant.Address1)] = "Address line 1",
        [nameof(Participant.Address2)] = "Address line 2",
        [nameof(Participant.Address3)] = "Address line 3",
        [nameof(Participant.Address4)] = "Address line 4",
        [nameof(Participant.Address5)] = "Address line 5",
        [nameof(Participant.CountryId)] = "Country",
        [nameof(Participant.Telephone)] = "Telephone",
        [nameof(Participant.Fax)] = "Fax",
        [nameof(Participant.Email)] = "Email",
        [nameof(Participant.Email2)] = "Alternative email",
        [nameof(Participant.Comments)] = "Other Packaging Requirements"
    };

    private static string Label(string field) => FieldLabels.GetValueOrDefault(field, field);

    public static ParticipantValidationResult Validate(Participant participant)
    {
        var errors = new List<ParticipantValidationError>();

        RequireNotEmpty(participant.LabCode, nameof(Participant.LabCode), errors);
        RequireNotEmpty(participant.LabName, nameof(Participant.LabName), errors);
        RequireNotEmpty(participant.ContactName, nameof(Participant.ContactName), errors);
        RequireNotEmpty(participant.Organisation, nameof(Participant.Organisation), errors);
        RequireNotEmpty(participant.Address1, nameof(Participant.Address1), errors);
        RequireNotEmpty(participant.Address2, nameof(Participant.Address2), errors);
        RequireSelected(participant.CountryId, nameof(Participant.CountryId), errors);
        RequireNotEmpty(participant.Telephone, nameof(Participant.Telephone), errors);
        RequireNotEmpty(participant.Email, nameof(Participant.Email), errors);

        if (!string.IsNullOrWhiteSpace(participant.Email))
        {
            try
            {
                _ = new System.Net.Mail.MailAddress(participant.Email);
            }
            catch (FormatException)
            {
                errors.Add(new ParticipantValidationError(nameof(Participant.Email), $"{Label(nameof(Participant.Email))} must be a valid email address"));
            }
        }

        if (!string.IsNullOrWhiteSpace(participant.Email2))
        {
            try
            {
                _ = new System.Net.Mail.MailAddress(participant.Email2);
            }
            catch (FormatException)
            {
                errors.Add(new ParticipantValidationError(nameof(Participant.Email2), $"{Label(nameof(Participant.Email2))} must be a valid email address"));
            }
        }

        return new ParticipantValidationResult(errors.Count == 0, errors);
    }

    private static void RequireNotEmpty(string? value, string field, List<ParticipantValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(new ParticipantValidationError(field, $"{Label(field)} is required"));
        }
    }

    private static void RequireSelected(Guid value, string field, List<ParticipantValidationError> errors)
    {
        if (value == Guid.Empty)
        {
            errors.Add(new ParticipantValidationError(field, $"{Label(field)} must be selected"));
        }
    }
}
