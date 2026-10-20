namespace PTL.Contracts.Search;

// Legacy Contracts Admin/Search.aspx - Customers tab. Every criterion is optional; an empty value
// (or no country) does not filter, exactly as dbo.spgSearchCustomer treats '' and the empty Guid.
public sealed record CustomerSearchCriteriaRequest(
    string? QalNumber = null,
    string? AccountNumber = null,
    string? Name = null,
    string? ContactName = null,
    string? Organisation = null,
    Guid? CountryId = null);

public sealed record CustomerSearchResultResponse(
    Guid CustomerId,
    string QalNumber,
    string AccountNumber,
    string Name,
    string ContactName,
    string Organisation,
    string Country);

// Legacy Contracts Admin/Search.aspx - Participants tab (dbo.spgSearchParticipant).
public sealed record ParticipantSearchCriteriaRequest(
    string? LabCode = null,
    string? LabName = null,
    string? ContactName = null);

public sealed record ParticipantSearchResultResponse(
    Guid ParticipantId,
    Guid CustomerId,
    string LabCode,
    string OldLabCode,
    string LabName,
    string ContactName);
