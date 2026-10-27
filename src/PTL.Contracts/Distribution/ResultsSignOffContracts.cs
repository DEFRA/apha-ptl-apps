namespace PTL.Contracts.Distribution;

// Results Sign-off queue (legacy Results Sign-off/SignOff.aspx): every tabulation awaiting final approval, grouped by
// month and then by distribution scheme. Months run oldest first, as legacy listed them.
public sealed record ResultsSignOffQueueResponse(IReadOnlyList<ResultsSignOffMonthResponse> Months);

public sealed record ResultsSignOffMonthResponse(int YearId, int MonthId, IReadOnlyList<ResultsSignOffSchemeResponse> Schemes);

// DistributionTitle reads "{reference}{suffix}/{schedule} {scheme identifier}: {scheme name}". TestConsultants and
// Assessors are comma-separated names (empty when none is allocated).
public sealed record ResultsSignOffSchemeResponse(
    Guid MonthlyDistributionSchemeId,
    string DistributionTitle,
    DateTime TargetDate,
    string TestConsultants,
    string Assessors,
    IReadOnlyList<ResultsSignOffTabulationResponse> Tabulations);

// The draft tabulation under review (legacy Results Sign-off/Tabulation.aspx): the report drawn without the columns the reviewer
// un-ticked, and the check boxes to offer - every sample number of the distribution, and whether there are method items.
public sealed record ResultsSignOffReviewResponse(
    TabulationReportResponse Report,
    bool HasMethodItems,
    IReadOnlyList<string> SampleNumbers);

// PublishCount is how many times the tabulation has already been published. Availability is legacy's wording:
// "All", "Participants Only" or "Viewers and TCs Only".
public sealed record ResultsSignOffTabulationResponse(
    Guid TabulationId,
    string Name,
    int PublishCount,
    bool IsSingleParticipant,
    string Availability,
    string Status);
