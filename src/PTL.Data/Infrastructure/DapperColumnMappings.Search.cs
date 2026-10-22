using PTL.Core.Search;

namespace PTL.Data.Infrastructure;

public static partial class DapperColumnMappings
{
    // spgSearchCustomer / spgSearchParticipant result columns (Contracts Admin/Search.aspx) and
    // spgSearchSchemeByDistribution / spgSearchSchemeBySampleNumber result columns
    // (Distributions/Search.aspx).
    private static void RegisterSearchMappings()
    {
        Map<CustomerSearchRow>(new(StringComparer.OrdinalIgnoreCase)
        {
            [ColCustomerId] = nameof(CustomerSearchRow.CustomerId),
            [ColQalNumber] = nameof(CustomerSearchRow.QalNumber),
            [ColAccountNumber] = nameof(CustomerSearchRow.AccountNumber),
            [ColName] = nameof(CustomerSearchRow.Name),
            [ColContactName] = nameof(CustomerSearchRow.ContactName),
            [ColOrganisation] = nameof(CustomerSearchRow.Organisation),
            [ColCountry] = nameof(CustomerSearchRow.Country),
        });

        Map<ParticipantSearchRow>(new(StringComparer.OrdinalIgnoreCase)
        {
            [ColParticipantId] = nameof(ParticipantSearchRow.ParticipantId),
            [ColCustomerId] = nameof(ParticipantSearchRow.CustomerId),
            [ColLabCode] = nameof(ParticipantSearchRow.LabCode),
            ["fldOldLabCode"] = nameof(ParticipantSearchRow.OldLabCode),
            [ColLabName] = nameof(ParticipantSearchRow.LabName),
            [ColContactName] = nameof(ParticipantSearchRow.ContactName),
        });

        Map<DistributionNumberSearchRow>(new(StringComparer.OrdinalIgnoreCase)
        {
            [ColMonthlyDistributionSchemeId] = nameof(DistributionNumberSearchRow.MonthlyDistributionSchemeId),
            [ColSchemeId] = nameof(DistributionNumberSearchRow.SchemeId),
            [ColSchemeName] = nameof(DistributionNumberSearchRow.SchemeName),
            ["fldSchemeIdentifier"] = nameof(DistributionNumberSearchRow.SchemeIdentifier),
            ["fldDistributionNumber"] = nameof(DistributionNumberSearchRow.DistributionNumber),
            [ColMonthId] = nameof(DistributionNumberSearchRow.MonthId),
            ["fldYear"] = nameof(DistributionNumberSearchRow.Year),
        });

        Map<SampleNumberSearchRow>(new(StringComparer.OrdinalIgnoreCase)
        {
            [ColSampleId] = nameof(SampleNumberSearchRow.SampleId),
            ["fldSampleNumber"] = nameof(SampleNumberSearchRow.SampleNumber),
            [ColSchemeId] = nameof(SampleNumberSearchRow.SchemeId),
            [ColSchemeName] = nameof(SampleNumberSearchRow.SchemeName),
            ["fldSchemeIdentifier"] = nameof(SampleNumberSearchRow.SchemeIdentifier),
            ["fldDistributionNumber"] = nameof(SampleNumberSearchRow.DistributionNumber),
            [ColMonthId] = nameof(SampleNumberSearchRow.MonthId),
            ["fldYear"] = nameof(SampleNumberSearchRow.Year),
        });
    }
}
