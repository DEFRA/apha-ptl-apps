namespace PTL.Contracts.Contract;

// One scheme row inside a sample address letter's FeePayingSchemes / NonFeePayingSchemes region.
public sealed record SampleAddressSchemeResponse(
    Guid ParticipantSchemeId,
    string SchemeName,
    string SchemeIdentifier,
    string MonthsActive,
    int WeekNumber);
