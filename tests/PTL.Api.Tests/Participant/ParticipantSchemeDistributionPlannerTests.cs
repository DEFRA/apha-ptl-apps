using PTL.Core.Participant;

namespace PTL.Api.Tests.Participant;

public sealed class ParticipantSchemeDistributionPlannerTests
{
    private static readonly Guid SchemeId = Guid.NewGuid();
    private static readonly Guid ParticipantId = Guid.NewGuid();
    private static readonly Guid ContractId = Guid.NewGuid();
    private static readonly Guid MdsId = Guid.NewGuid();

    private static ParticipantSchemeRecord Record(bool removed = false, params int[] months)
    {
        var record = new ParticipantSchemeRecord { SchemeId = SchemeId, ParticipantId = ParticipantId, ContractId = ContractId, IsRemoved = removed };
        var flags = new bool[12];
        foreach (var month in months)
        {
            flags[month - 1] = true;
        }

        ParticipantSchemeMonths.SetDistribution(record, flags);
        return record;
    }

    private static DistributionSchemeSnapshot Scheme(Guid? schemeId = null, string? suffix = null, params DistributionParticipantSnapshot[] participants) =>
        new(MdsId, schemeId ?? SchemeId, suffix, participants);

    [Fact]
    public void SelectedMonth_ParticipantNotOnDistribution_IsAdded()
    {
        var plan = ParticipantSchemeDistributionPlanner.PlanMonth(Record(false, 5), 5, [Scheme()]);

        Assert.Equal([MdsId], plan.AddToMonthlyDistributionSchemeIds);
        Assert.Empty(plan.RemoveDistributionParticipantIds);
    }

    [Fact]
    public void SelectedMonth_ParticipantAlreadyOnDistribution_NothingToDo()
    {
        var existing = new DistributionParticipantSnapshot(Guid.NewGuid(), ParticipantId, ContractId);

        var plan = ParticipantSchemeDistributionPlanner.PlanMonth(Record(false, 5), 5, [Scheme(participants: existing)]);

        Assert.Same(MonthDistributionPlan.None, plan);
    }

    [Fact]
    public void UnselectedMonth_ParticipantOnDistributionFromThisContract_IsRemoved()
    {
        var existing = new DistributionParticipantSnapshot(Guid.NewGuid(), ParticipantId, ContractId);

        var plan = ParticipantSchemeDistributionPlanner.PlanMonth(Record(false, 6), 5, [Scheme(participants: existing)]);

        Assert.Equal([existing.DistributionParticipantId], plan.RemoveDistributionParticipantIds);
        Assert.Empty(plan.AddToMonthlyDistributionSchemeIds);
    }

    [Fact]
    public void UnselectedMonth_ParticipantOnDistributionFromAnotherContract_IsLeftAlone()
    {
        var existing = new DistributionParticipantSnapshot(Guid.NewGuid(), ParticipantId, Guid.NewGuid());

        var plan = ParticipantSchemeDistributionPlanner.PlanMonth(Record(false, 6), 5, [Scheme(participants: existing)]);

        Assert.Same(MonthDistributionPlan.None, plan);
    }

    [Fact]
    public void RemovedItem_IsTakenOffEveryDistribution()
    {
        var existing = new DistributionParticipantSnapshot(Guid.NewGuid(), ParticipantId, ContractId);

        var plan = ParticipantSchemeDistributionPlanner.PlanMonth(Record(true, 5), 5, [Scheme(participants: existing)]);

        Assert.Equal([existing.DistributionParticipantId], plan.RemoveDistributionParticipantIds);
    }

    [Fact]
    public void ItemWithNoMonths_IsOnEveryDistribution_LikeLegacyAsAvailable()
    {
        var plan = ParticipantSchemeDistributionPlanner.PlanMonth(Record(false), 9, [Scheme()]);

        Assert.Equal([MdsId], plan.AddToMonthlyDistributionSchemeIds);
    }

    [Fact]
    public void OtherSchemesAndRepeatDistributions_AreIgnored()
    {
        var plan = ParticipantSchemeDistributionPlanner.PlanMonth(Record(false, 5), 5, [Scheme(Guid.NewGuid()), Scheme(suffix: "a")]);

        Assert.Same(MonthDistributionPlan.None, plan);
    }

    [Theory]
    [InlineData(4, 4, 2026, 2026)]
    [InlineData(12, 4, 2026, 2026)]
    [InlineData(1, 4, 2026, 2027)]
    [InlineData(3, 4, 2026, 2027)]
    [InlineData(1, 1, 2026, 2026)]
    public void DistributionYear_MonthsBeforeTheContractStartMonth_FallInTheNextYear(int monthId, int startMonth, int contractYear, int expected) =>
        Assert.Equal(expected, ParticipantSchemeDistributionPlanner.DistributionYear(contractYear, monthId, startMonth));
}
