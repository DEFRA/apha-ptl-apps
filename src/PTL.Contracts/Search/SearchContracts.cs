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

// Legacy Distributions/Search.aspx - Distribution Number tab (dbo.spgSearchSchemeByDistribution).
// An empty/omitted value does not filter - legacy returns every distribution in that case.
public sealed record DistributionNumberSearchCriteriaRequest(string? DistributionNumber = null);

public sealed record DistributionNumberSearchResultResponse(
    Guid MonthlyDistributionSchemeId,
    Guid SchemeId,
    string DistributionNumber,
    string SchemeName,
    string SchemeIdentifier,
    string Year,
    string Month);

// Legacy Distributions/Search.aspx - Sample Number tab (dbo.spgSearchSchemeBySampleNumber).
// Sample Number is mandatory in the UI ("A Sample Number must be supplied."), enforced client-side.
public sealed record SampleNumberSearchCriteriaRequest(string? SampleNumber = null);

public sealed record SampleNumberSearchResultResponse(
    Guid SampleId,
    Guid SchemeId,
    string SampleNumber,
    string SchemeName,
    string SchemeIdentifier,
    string DistributionNumber,
    string Year,
    string Month);
