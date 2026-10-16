using System.Globalization;

namespace PTL.Core.Distribution;

// Singleton shared by the immediate send after Save and the scheduler, so only one of them sends at a time.
// The sets below are in-process memory (single PTL.Api instance): they stop a retry re-sending an email that
// already went out, and keep the same warning from being logged on every poll.
public sealed class TabulationEmailGuard
{
    private readonly HashSet<Guid> firstSent = [];
    private readonly HashSet<(Guid MonthlyDistributionSchemeId, int Slot)> chasersSent = [];
    private readonly Dictionary<(Guid Id, int Slot), DateTime> warnedOn = [];

    public SemaphoreSlim Lock { get; } = new(1, 1);

    public bool FirstSent(Guid monthlyDistributionSchemeId) => firstSent.Contains(monthlyDistributionSchemeId);

    public void MarkFirstSent(Guid monthlyDistributionSchemeId) => firstSent.Add(monthlyDistributionSchemeId);

    public void ClearFirst(Guid monthlyDistributionSchemeId) => firstSent.Remove(monthlyDistributionSchemeId);

    public bool ChaserSent(Guid monthlyDistributionSchemeId, int slot) => chasersSent.Contains((monthlyDistributionSchemeId, slot));

    public void MarkChaserSent(Guid monthlyDistributionSchemeId, int slot) => chasersSent.Add((monthlyDistributionSchemeId, slot));

    public void ClearChasers(Guid monthlyDistributionSchemeId) => chasersSent.RemoveWhere(s => s.MonthlyDistributionSchemeId == monthlyDistributionSchemeId);

    // True the first time per id, slot and day.
    public bool FirstWarningToday(Guid id, int slot, DateTime today)
    {
        if (warnedOn.TryGetValue((id, slot), out var day) && day >= today)
        {
            return false;
        }

        warnedOn[(id, slot)] = today;
        return true;
    }
}
