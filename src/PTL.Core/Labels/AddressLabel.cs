namespace PTL.Core.Labels;

// The ordered fields a label is built from. Mirrors legacy's CustomerAddress/ParticipantAddress
// constructor arguments; Telephone is only populated for participant labels.
public sealed record AddressLabelFields
{
    public string? ContactName { get; init; }
    public string? Organisation { get; init; }
    public string? Address1 { get; init; }
    public string? Address2 { get; init; }
    public string? Address3 { get; init; }
    public string? Address4 { get; init; }
    public string? Address5 { get; init; }
    public string? Country { get; init; }
    public string? Telephone { get; init; }
}

// Mirrors legacy LabelBase.AutoGenerateLabel (PtaBusinessObjects/Labels/LabelBase.vb): fields are
// emitted one per line in the order supplied and any blank or whitespace-only field is omitted
// entirely. Values are deliberately not trimmed, matching legacy.
public sealed record AddressLabel(IReadOnlyList<string> Lines)
{
    public static AddressLabel FromFields(params string?[] fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        return new AddressLabel([.. fields.Where(field => !string.IsNullOrWhiteSpace(field)).Select(field => field!)]);
    }
}

// Field content and ordering are fixed by the legacy label classes and must not be changed.
public static class AddressLabelComposer
{
    // Legacy LabelCustomerAddress.SetLabelString. Customer.aspx.vb uses this same class for both
    // the contact label and the invoice label, passing the invoice fields in place of the contact
    // ones - hence a single method rather than two.
    public static AddressLabel CustomerAddress(AddressLabelFields fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        return AddressLabel.FromFields(
            fields.ContactName, fields.Organisation,
            fields.Address1, fields.Address2, fields.Address3, fields.Address4, fields.Address5,
            fields.Country);
    }

    // Legacy LabelParticipantAddress.SetLabelString - identical to the customer label except that
    // the telephone number is appended as a final line after the country.
    public static AddressLabel ParticipantAddress(AddressLabelFields fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        return AddressLabel.FromFields(
            fields.ContactName, fields.Organisation,
            fields.Address1, fields.Address2, fields.Address3, fields.Address4, fields.Address5,
            fields.Country, fields.Telephone);
    }
}
