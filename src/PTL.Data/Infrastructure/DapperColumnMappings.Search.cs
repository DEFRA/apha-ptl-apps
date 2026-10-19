using PTL.Core.Search;

namespace PTL.Data.Infrastructure;

public static partial class DapperColumnMappings
{
    // spgSearchCustomer / spgSearchParticipant result columns (Contracts Admin/Search.aspx).
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
    }
}
