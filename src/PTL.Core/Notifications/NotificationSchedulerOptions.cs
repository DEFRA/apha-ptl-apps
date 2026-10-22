namespace PTL.Core.Notifications;

// Timing shared by every email job the NotificationBackgroundService runs; each job keeps its own Enabled switch.
public sealed class NotificationSchedulerOptions
{
    public const string SectionName = "NotificationScheduler";

    public TimeSpan PollInterval { get; set; } = TimeSpan.FromMinutes(5);

    public TimeSpan StartupDelay { get; set; } = TimeSpan.FromSeconds(30);
}
