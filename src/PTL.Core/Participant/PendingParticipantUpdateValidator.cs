namespace PTL.Core.Participant;

// Participant-facing validation for EditParticipantDetails.aspx's submission - mirrors legacy's
// AddBusinessRules()/ASPX RequiredFieldValidator flags exactly (ContactName, Organisation,
// Address1, Address2, Country, Telephone, Email are mandatory; Address3-5 and Email2 are not).
// The contact/address rules shared with the customer screen live in PendingUpdateFieldChecks.
public static class PendingParticipantUpdateValidator
{
    private static readonly Dictionary<string, string> FieldLabels = new()
    {
        [nameof(PendingParticipantUpdate.ContactName)] = "Contact Name",
        [nameof(PendingParticipantUpdate.Organisation)] = "Organisation Name",
        [nameof(PendingParticipantUpdate.Address1)] = "Address 1",
        [nameof(PendingParticipantUpdate.Address2)] = "Address 2",
        [nameof(PendingParticipantUpdate.Address3)] = "Address 3",
        [nameof(PendingParticipantUpdate.Address4)] = "Address 4",
        [nameof(PendingParticipantUpdate.Address5)] = "Address 5",
        [nameof(PendingParticipantUpdate.CountryId)] = "Country",
        [nameof(PendingParticipantUpdate.Telephone)] = "Telephone",
        [nameof(PendingParticipantUpdate.Email)] = "Email (Primary)",
        [nameof(PendingParticipantUpdate.Email2)] = "Email (Secondary)"
    };

    public static ParticipantValidationResult Validate(PendingParticipantUpdate update)
    {
        var checks = new PendingUpdateFieldChecks(FieldLabels);

        checks.ValidateContactDetails(update);
        checks.MaxLength(update.Email2, 150, nameof(update.Email2));
        checks.RequireValidEmail(update.Email2, nameof(update.Email2));

        var errors = checks.Errors.Select(e => new ParticipantValidationError(e.Field, e.Message)).ToList();
        return new ParticipantValidationResult(errors.Count == 0, errors);
    }
}
