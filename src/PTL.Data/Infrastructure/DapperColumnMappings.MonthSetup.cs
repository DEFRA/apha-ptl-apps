using PTL.Core.Distribution;

namespace PTL.Data.Infrastructure;

public static partial class DapperColumnMappings
{
    // sppSetupMonthlyDistribution's two result sets. Only the columns the plan needs are mapped;
    // Dapper skips the rest (fldIsCancelled, fldCustomerId, fldExternalReference, fldContactName).
    private static void RegisterMonthSetupMappings()
    {
        Map<MonthSetupSchemeRow>(new(StringComparer.OrdinalIgnoreCase)
        {
            [ColSchemeId] = nameof(MonthSetupSchemeRow.SchemeId),
            ["fldWeekNumber"] = nameof(MonthSetupSchemeRow.WeekNumber),
            ["fldDeadline"] = nameof(MonthSetupSchemeRow.DeadlineDays),
            ["fldCommentsRequired"] = nameof(MonthSetupSchemeRow.CommentsRequired),
            ["DistributionReference"] = nameof(MonthSetupSchemeRow.DistributionReference),
            [ColScheduleCode] = nameof(MonthSetupSchemeRow.ScheduleCode),
            ["fldDayOfWeek"] = nameof(MonthSetupSchemeRow.DayOffset),
            [ColName] = nameof(MonthSetupSchemeRow.SchemeName),
            [ColIdentifier] = nameof(MonthSetupSchemeRow.SchemeIdentifier),
            ["fldNumberOfSamples"] = nameof(MonthSetupSchemeRow.NumberOfSamples),
            ["fldIsAsAvailable"] = nameof(MonthSetupSchemeRow.IsAsAvailable),
            ["fldStoreRatings"] = nameof(MonthSetupSchemeRow.StoreRatings),
            ["fldCombinedPackaging"] = nameof(MonthSetupSchemeRow.CombinedPackaging),
            ["fldVersionDate"] = nameof(MonthSetupSchemeRow.VersionDate),
        });

        Map<MonthSetupParticipantRow>(new(StringComparer.OrdinalIgnoreCase)
        {
            ["SchemeId"] = nameof(MonthSetupParticipantRow.SchemeId),
            [ColParticipantId] = nameof(MonthSetupParticipantRow.ParticipantId),
            [ColLabCode] = nameof(MonthSetupParticipantRow.LabCode),
            [ColLabName] = nameof(MonthSetupParticipantRow.LabName),
            [ColNumberOfSetsRequired] = nameof(MonthSetupParticipantRow.NumberOfSetsRequired),
            [ColContractId] = nameof(MonthSetupParticipantRow.ContractId),
        });
    }
}
