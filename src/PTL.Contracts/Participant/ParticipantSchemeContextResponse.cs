namespace PTL.Contracts.Participant;

// Per-month facts about a Scheme that a Contract Item needs before it is saved (legacy
// ParticipantScheme.SchemeId setter). Index 0 = January. CanEdit[i] is false once that month's
// distribution has been posted (the month is locked unless overridden); InitialisedMonthIds are the
// months (1-12) whose distribution is initialised but not yet distributed (ParticipantSchemeWarning).
public sealed record ParticipantSchemeContextResponse(IReadOnlyList<bool> CanEdit, IReadOnlyList<int> InitialisedMonthIds);
