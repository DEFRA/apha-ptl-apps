using PTL.Core.Contract.CreateOrder;

namespace PTL.Data.Infrastructure;

public static partial class DapperColumnMappings
{
    // spgaAvailableSchemeInfoByYearId and spgPastParticipantSchemeByParticipantSchemeId return many more
    // columns than the order page needs; Dapper skips the unmapped ones.
    private static void RegisterCreateOrderMappings()
    {
        Map<AvailableSchemeEntity>(new(StringComparer.OrdinalIgnoreCase)
        {
            [ColSchemeId] = nameof(AvailableSchemeEntity.SchemeId),
            [ColIdentifier] = nameof(AvailableSchemeEntity.Identifier),
            [ColName] = nameof(AvailableSchemeEntity.Name),
            ["fldSchedule"] = nameof(AvailableSchemeEntity.Schedule),
            ["fldComerciallyAvailable"] = nameof(AvailableSchemeEntity.ComerciallyAvailable),
            ["fldPostage"] = nameof(AvailableSchemeEntity.Postage),
            ["fldCombinedPackaging"] = nameof(AvailableSchemeEntity.CombinedPackaging),
            ["fldDayOfWeekId"] = nameof(AvailableSchemeEntity.DayOfWeekId),
            ["fldWeekNumber"] = nameof(AvailableSchemeEntity.WeekNumber),
            ["fldDistributionMonthJan"] = nameof(AvailableSchemeEntity.DistributionMonthJan),
            ["fldDistributionMonthFeb"] = nameof(AvailableSchemeEntity.DistributionMonthFeb),
            ["fldDistributionMonthMar"] = nameof(AvailableSchemeEntity.DistributionMonthMar),
            ["fldDistributionMonthApr"] = nameof(AvailableSchemeEntity.DistributionMonthApr),
            ["fldDistributionMonthMay"] = nameof(AvailableSchemeEntity.DistributionMonthMay),
            ["fldDistributionMonthJun"] = nameof(AvailableSchemeEntity.DistributionMonthJun),
            ["fldDistributionMonthJul"] = nameof(AvailableSchemeEntity.DistributionMonthJul),
            ["fldDistributionMonthAug"] = nameof(AvailableSchemeEntity.DistributionMonthAug),
            ["fldDistributionMonthSep"] = nameof(AvailableSchemeEntity.DistributionMonthSep),
            ["fldDistributionMonthOct"] = nameof(AvailableSchemeEntity.DistributionMonthOct),
            ["fldDistributionMonthNov"] = nameof(AvailableSchemeEntity.DistributionMonthNov),
            ["fldDistributionMonthDec"] = nameof(AvailableSchemeEntity.DistributionMonthDec),
            ["fldCanEditJan"] = nameof(AvailableSchemeEntity.CanEditJan),
            ["fldCanEditFeb"] = nameof(AvailableSchemeEntity.CanEditFeb),
            ["fldCanEditMar"] = nameof(AvailableSchemeEntity.CanEditMar),
            ["fldCanEditApr"] = nameof(AvailableSchemeEntity.CanEditApr),
            ["fldCanEditMay"] = nameof(AvailableSchemeEntity.CanEditMay),
            ["fldCanEditJun"] = nameof(AvailableSchemeEntity.CanEditJun),
            ["fldCanEditJul"] = nameof(AvailableSchemeEntity.CanEditJul),
            ["fldCanEditAug"] = nameof(AvailableSchemeEntity.CanEditAug),
            ["fldCanEditSep"] = nameof(AvailableSchemeEntity.CanEditSep),
            ["fldCanEditOct"] = nameof(AvailableSchemeEntity.CanEditOct),
            ["fldCanEditNov"] = nameof(AvailableSchemeEntity.CanEditNov),
            ["fldCanEditDec"] = nameof(AvailableSchemeEntity.CanEditDec),
        });

        Map<ParticipatingSchemeEntity>(new(StringComparer.OrdinalIgnoreCase)
        {
            [ColParticipantId] = nameof(ParticipatingSchemeEntity.ParticipantId),
            [ColSchemeId] = nameof(ParticipatingSchemeEntity.SchemeId),
            [ColIdentifier] = nameof(ParticipatingSchemeEntity.Identifier),
            [ColName] = nameof(ParticipatingSchemeEntity.Name),
            ["fldSchedule"] = nameof(ParticipatingSchemeEntity.Schedule),
            ["fldDistributionMonthJan"] = nameof(ParticipatingSchemeEntity.DistributionMonthJan),
            ["fldDistributionMonthFeb"] = nameof(ParticipatingSchemeEntity.DistributionMonthFeb),
            ["fldDistributionMonthMar"] = nameof(ParticipatingSchemeEntity.DistributionMonthMar),
            ["fldDistributionMonthApr"] = nameof(ParticipatingSchemeEntity.DistributionMonthApr),
            ["fldDistributionMonthMay"] = nameof(ParticipatingSchemeEntity.DistributionMonthMay),
            ["fldDistributionMonthJun"] = nameof(ParticipatingSchemeEntity.DistributionMonthJun),
            ["fldDistributionMonthJul"] = nameof(ParticipatingSchemeEntity.DistributionMonthJul),
            ["fldDistributionMonthAug"] = nameof(ParticipatingSchemeEntity.DistributionMonthAug),
            ["fldDistributionMonthSep"] = nameof(ParticipatingSchemeEntity.DistributionMonthSep),
            ["fldDistributionMonthOct"] = nameof(ParticipatingSchemeEntity.DistributionMonthOct),
            ["fldDistributionMonthNov"] = nameof(ParticipatingSchemeEntity.DistributionMonthNov),
            ["fldDistributionMonthDec"] = nameof(ParticipatingSchemeEntity.DistributionMonthDec),
        });
    }
}
