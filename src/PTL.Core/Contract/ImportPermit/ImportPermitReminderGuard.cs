namespace PTL.Core.Contract.ImportPermit;

// Singleton (single PTL.Api instance). The job runs on every poll, so this keeps the same warning or send failure
// from being logged on every poll - legacy LogOnlyOncePerDay.
public sealed class ImportPermitReminderGuard
{
    private readonly Dictionary<string, DateOnly> loggedOn = [];

    // True the first time per key and day.
    public bool FirstLogToday(string key, DateOnly today)
    {
        if (loggedOn.TryGetValue(key, out var day) && day >= today)
        {
            return false;
        }

        loggedOn[key] = today;
        return true;
    }
}
