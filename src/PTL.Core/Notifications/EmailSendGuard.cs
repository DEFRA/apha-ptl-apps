namespace PTL.Core.Notifications;

// Singleton shared by every immediate send (after a decision is saved) and the scheduler, so only one of them sends at a
// time (single PTL.Api instance). The warning set is in-process memory that keeps a failing row from logging on every poll.
public sealed class EmailSendGuard
{
    private readonly Dictionary<Guid, DateTime> warnedOn = [];

    public SemaphoreSlim Lock { get; } = new(1, 1);

    // True the first time per id and day; call only while holding Lock.
    public bool FirstWarningToday(Guid id, DateTime today)
    {
        if (warnedOn.TryGetValue(id, out var day) && day >= today)
        {
            return false;
        }

        warnedOn[id] = today;
        return true;
    }
}
