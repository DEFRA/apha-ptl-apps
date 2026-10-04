namespace PTL.Core.Labels;

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
    public static AddressLabel CustomerAddress(
        string? contactName,
        string? organisation,
        string? address1,
        string? address2,
        string? address3,
        string? address4,
        string? address5,
        string? country) =>
        AddressLabel.FromFields(contactName, organisation, address1, address2, address3, address4, address5, country);

    // Legacy LabelParticipantAddress.SetLabelString - identical to the customer label except that
    // the telephone number is appended as a final line after the country.
    public static AddressLabel ParticipantAddress(
        string? contactName,
        string? organisation,
        string? address1,
        string? address2,
        string? address3,
        string? address4,
        string? address5,
        string? country,
        string? telephone) =>
        AddressLabel.FromFields(contactName, organisation, address1, address2, address3, address4, address5, country, telephone);
}
