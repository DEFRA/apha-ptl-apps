namespace PTL.Core.Search;

// Criteria as dbo.spgSearchCustomer takes them: '' means "do not filter", Guid.Empty means "any country".
public sealed record CustomerSearchCriteria(
    string QalNumber,
    string AccountNumber,
    string Name,
    string ContactName,
    string Organisation,
    Guid CountryId);

public sealed record ParticipantSearchCriteria(string LabCode, string LabName, string ContactName);

// Legacy Distributions/Search.aspx - Distribution Number tab (dbo.spgSearchSchemeByDistribution).
// '' means "do not filter" (returns every distribution), exactly as the stored procedure treats it.
public sealed record DistributionNumberSearchCriteria(string DistributionNumber);

// Legacy Distributions/Search.aspx - Sample Number tab (dbo.spgSearchSchemeBySampleNumber). Sample
// Number is mandatory in the UI, so '' is never actually sent here, but the procedure itself treats
// '' the same way as the Distribution Number search (no filter).
public sealed record SampleNumberSearchCriteria(string SampleNumber);

// Keyless projections of dbo.spgSearchCustomer / dbo.spgSearchParticipant result rows.
public class CustomerSearchRow
{
    public Guid CustomerId { get; set; }
    public string? QalNumber { get; set; }
    public string? AccountNumber { get; set; }
    public string? Name { get; set; }
    public string? ContactName { get; set; }
    public string? Organisation { get; set; }
    public string? Country { get; set; }
}

public class ParticipantSearchRow
{
    public Guid ParticipantId { get; set; }
    public Guid CustomerId { get; set; }
    public string? LabCode { get; set; }
    public string? OldLabCode { get; set; }
    public string? LabName { get; set; }
    public string? ContactName { get; set; }
}

// Keyless projection of a dbo.spgSearchSchemeByDistribution result row. MonthId/Year are the raw
// fldMonthId (1-12) and fldYear (4-digit calendar year) columns - legacy's MonthId property applied
// VB's MonthName() to the former at read time; that conversion is done where the row is turned into
// a response, not here.
public class DistributionNumberSearchRow
{
    public Guid MonthlyDistributionSchemeId { get; set; }
    public Guid SchemeId { get; set; }
    public string? SchemeName { get; set; }
    public string? SchemeIdentifier { get; set; }
    public string? DistributionNumber { get; set; }
    public int MonthId { get; set; }
    public string? Year { get; set; }
}

// Keyless projection of a dbo.spgSearchSchemeBySampleNumber result row.
public class SampleNumberSearchRow
{
    public Guid SampleId { get; set; }
    public Guid SchemeId { get; set; }
    public string? SampleNumber { get; set; }
    public string? SchemeName { get; set; }
    public string? SchemeIdentifier { get; set; }
    public string? DistributionNumber { get; set; }
    public int MonthId { get; set; }
    public string? Year { get; set; }
}
