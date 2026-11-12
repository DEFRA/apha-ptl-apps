namespace PTL.ExternalWeb.Infrastructure;

/// <summary>Wording for the session timeout dialog; session-timeout.js formats the live countdown the same way.</summary>
public static class SessionTimeoutText
{
    /// <summary>Whole seconds as words, for example "2 minutes", "1 minute 30 seconds" or "45 seconds".</summary>
    public static string Duration(int totalSeconds)
    {
        var minutes = totalSeconds / 60;
        var seconds = totalSeconds % 60;

        var parts = new List<string>(2);
        if (minutes > 0)
        {
            parts.Add(Plural(minutes, "minute"));
        }

        if (seconds > 0)
        {
            parts.Add(Plural(seconds, "second"));
        }

        return string.Join(' ', parts);
    }

    private static string Plural(int count, string unit) => count == 1 ? $"{count} {unit}" : $"{count} {unit}s";
}
