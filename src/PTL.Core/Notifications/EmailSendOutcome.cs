namespace PTL.Core.Notifications;

// The result of attempting one queued email in a notifier run.
public enum EmailSendOutcome
{
    Sent,
    Skipped,
    Failed
}
