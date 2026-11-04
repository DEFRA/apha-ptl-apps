using PTL.Core.Contract.ImportPermit;

namespace PTL.Data.Infrastructure;

public static partial class DapperColumnMappings
{
    // The two result sets of spgaContractOrderImportPermitForEmail.
    private static void RegisterImportPermitReminderMappings()
    {
        Map<ImportPermitCustomerEntity>(new(StringComparer.OrdinalIgnoreCase)
        {
            [ColContractId] = nameof(ImportPermitCustomerEntity.ContractId),
            [ColCustomerId] = nameof(ImportPermitCustomerEntity.CustomerId),
            ["fldMonthlyDistributionSchemeId"] = nameof(ImportPermitCustomerEntity.MonthlyDistributionSchemeId),
            ["fldCustomerName"] = nameof(ImportPermitCustomerEntity.CustomerName),
            ["fldCustomerEmail"] = nameof(ImportPermitCustomerEntity.CustomerEmail),
            [ColQalNumber] = nameof(ImportPermitCustomerEntity.QalNumber),
            ["fldDistributionReference"] = nameof(ImportPermitCustomerEntity.DistributionReference),
            ["fldIdentifier"] = nameof(ImportPermitCustomerEntity.SchemeIdentifier),
            ["fldCustomerReminderSent"] = nameof(ImportPermitCustomerEntity.ReminderSent),
        });

        Map<ImportPermitParticipantEntity>(new(StringComparer.OrdinalIgnoreCase)
        {
            [ColContractId] = nameof(ImportPermitParticipantEntity.ContractId),
            [ColCustomerId] = nameof(ImportPermitParticipantEntity.CustomerId),
            [ColParticipantId] = nameof(ImportPermitParticipantEntity.ParticipantId),
            ["fldParticipantSchemeId"] = nameof(ImportPermitParticipantEntity.ParticipantSchemeId),
            [ColSchemeId] = nameof(ImportPermitParticipantEntity.SchemeId),
            ["fldMonthlyDistributionSchemeId"] = nameof(ImportPermitParticipantEntity.MonthlyDistributionSchemeId),
            [ColSchemeName] = nameof(ImportPermitParticipantEntity.SchemeName),
            [ColQalNumber] = nameof(ImportPermitParticipantEntity.QalNumber),
            ["fldParticipantEmail"] = nameof(ImportPermitParticipantEntity.ParticipantEmail),
            ["fldLabCode"] = nameof(ImportPermitParticipantEntity.LabCode),
            ["fldDistributionReference"] = nameof(ImportPermitParticipantEntity.DistributionReference),
            ["fldIdentifier"] = nameof(ImportPermitParticipantEntity.SchemeIdentifier),
            ["fldImportPermitReceived"] = nameof(ImportPermitParticipantEntity.ImportPermitReceived),
            ["fldImportPermitExpiry"] = nameof(ImportPermitParticipantEntity.ImportPermitExpiry),
            [ColDistributionDate] = nameof(ImportPermitParticipantEntity.DistributionDate),
            ["fldOverseasPostingDate"] = nameof(ImportPermitParticipantEntity.OverseasPostingDate),
        });
    }
}
