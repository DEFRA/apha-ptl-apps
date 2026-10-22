namespace PTL.Core.Participant;

// What spgMonthlyDistribution returns for one month - only the columns the sync needs.
public sealed record DistributionParticipantSnapshot(Guid DistributionParticipantId, Guid ParticipantId, Guid ContractId);

public sealed record DistributionSchemeSnapshot(
    Guid MonthlyDistributionSchemeId,
    Guid SchemeId,
    string? ReferenceSuffix,
    IReadOnlyList<DistributionParticipantSnapshot> Participants);

public sealed record MonthDistributionPlan(
    IReadOnlyList<Guid> AddToMonthlyDistributionSchemeIds,
    IReadOnlyList<Guid> RemoveDistributionParticipantIds)
{
    public static MonthDistributionPlan None { get; } = new([], []);
}

// Port of the decision logic in legacy ParticipantScheme.UpdateDistributions (PtaBusinessObjects/
// Business Objects/Contracts/ParticipantScheme.vb): which monthly distributions a contract item's
// participant has to be added to or removed from. Pure, so the rules are unit-testable without SQL.
public static class ParticipantSchemeDistributionPlanner
{
    // Legacy IsAsAvailable(): an item with no month ticked is treated as on every month's distribution.
    public static bool ShouldBeOnDistribution(ParticipantSchemeRecord record, int monthId)
    {
        if (record.IsRemoved)
        {
            return false;
        }

        var months = ParticipantSchemeMonths.Distribution(record);
        return !months.Any(m => m) || months[monthId - 1];
    }

    // Months before the contract year's start month fall in the following calendar year.
    public static int DistributionYear(int contractYearId, int monthId, int contractStartMonth) =>
        monthId < contractStartMonth ? contractYearId + 1 : contractYearId;

    public static MonthDistributionPlan PlanMonth(ParticipantSchemeRecord record, int monthId, IReadOnlyList<DistributionSchemeSnapshot> schemes)
    {
        var shouldBeOn = ShouldBeOnDistribution(record, monthId);
        var add = new List<Guid>();
        var remove = new List<Guid>();

        // Repeat distributions (suffix a, b...) are managed on the Participants screen, never here.
        foreach (var scheme in schemes.Where(s => s.SchemeId == record.SchemeId && string.IsNullOrEmpty(s.ReferenceSuffix)))
        {
            var existing = scheme.Participants.FirstOrDefault(p => p.ParticipantId == record.ParticipantId);
            if (existing is not null)
            {
                // Another contract's participation in the same distribution is left alone.
                if (!shouldBeOn && existing.ContractId == record.ContractId)
                {
                    remove.Add(existing.DistributionParticipantId);
                }
            }
            else if (shouldBeOn)
            {
                add.Add(scheme.MonthlyDistributionSchemeId);
            }
        }

        return add.Count == 0 && remove.Count == 0 ? MonthDistributionPlan.None : new MonthDistributionPlan(add, remove);
    }
}
