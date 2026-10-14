namespace PTL.Core.Distribution;

public interface IPackagingService
{
    // Returns null when the month has not been initialised or no packaging header is found.
    Task<PackagingSheet?> GetPackagingAsync(
        int yearId, int monthId, PackagingMode mode, int week, CancellationToken cancellationToken = default);

    Task<PackagingSaveOutcome> SavePackagingAsync(
        int yearId, int monthId, IReadOnlyList<PackagingStageUpdate> updates, CancellationToken cancellationToken = default);
}

public sealed class PackagingService(
    IDistributionRepository distributionRepository,
    IDistributionPostedNotifier postedNotifier,
    TimeProvider timeProvider) : IPackagingService
{
    private sealed record ProgressRow(
        Guid ParticipantId,
        Guid MonthlyDistributionSchemeId,
        bool PreparationComplete,
        DateTime? PackagedDate,
        DateTime? PostedDate,
        string Distribution,
        string SchemeName);

    public async Task<PackagingSheet?> GetPackagingAsync(
        int yearId, int monthId, PackagingMode mode, int week, CancellationToken cancellationToken = default)
    {
        var raw = await LoadRawAsync(yearId, monthId, mode, week, cancellationToken);
        return raw is null
            ? null
            : PackagingSheetBuilder.Build(mode, week, raw, timeProvider.GetUtcNow().UtcDateTime);
    }

    // Legacy ButtonSave_Click/ButtonApply_Click: every sheet the user touched is saved. Ticking
    // Packed or Posted stamps today's date on a date that is still empty (never overwritten);
    // Posted also stamps Packed and queues the Distribution Posted email.
    public async Task<PackagingSaveOutcome> SavePackagingAsync(
        int yearId, int monthId, IReadOnlyList<PackagingStageUpdate> updates, CancellationToken cancellationToken = default)
    {
        var schedule = await distributionRepository.GetMonthlyDistributionSchedulesAsync(yearId, monthId, cancellationToken);
        if (schedule is null)
        {
            return new PackagingSaveOutcome(false);
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var progress = new List<PackagingProgressUpdate>();
        var newlyPosted = new List<PostedParticipant>();

        var sheets = updates
            .Where(u => u.Packed || u.Posted)
            .GroupBy(u => (u.Mode, Week: u.Mode == PackagingMode.Daily ? 0 : u.Week));

        foreach (var sheet in sheets)
        {
            var raw = await distributionRepository.GetPackagingSheetAsync(schedule.Value.MonthlyDistributionId, sheet.Key.Mode, sheet.Key.Week, cancellationToken);
            if (raw is null)
            {
                continue;
            }

            CollectProgress(sheet, ProgressRows(raw), now, progress, newlyPosted);
        }

        var emails = await postedNotifier.BuildEmailsAsync(newlyPosted, cancellationToken);
        if (progress.Count > 0 || emails.Count > 0)
        {
            await distributionRepository.UpdatePackagingAsync(progress, emails, cancellationToken);
            await postedNotifier.DispatchAsync(emails, cancellationToken);
        }

        return new PackagingSaveOutcome(true);
    }

    private static void CollectProgress(
        IEnumerable<PackagingStageUpdate> updates,
        Dictionary<Guid, ProgressRow> rows,
        DateTime now,
        List<PackagingProgressUpdate> progress,
        List<PostedParticipant> newlyPosted)
    {
        foreach (var update in updates.DistinctBy(u => u.DistributionParticipantId))
        {
            // Legacy only offers the checkboxes once the Scheme's preparation is complete.
            if (!rows.TryGetValue(update.DistributionParticipantId, out var row) || !row.PreparationComplete)
            {
                continue;
            }

            var newlyPackaged = row.PackagedDate is null;
            var newlyPostedNow = update.Posted && row.PostedDate is null;

            if (newlyPostedNow)
            {
                newlyPosted.Add(new PostedParticipant(row.ParticipantId, row.MonthlyDistributionSchemeId, row.Distribution, row.SchemeName));
            }

            if (newlyPackaged || newlyPostedNow)
            {
                progress.Add(new PackagingProgressUpdate(
                    update.DistributionParticipantId, row.PackagedDate ?? now, newlyPostedNow ? now : row.PostedDate));
            }
        }
    }

    private async Task<PackagingRawData?> LoadRawAsync(
        int yearId, int monthId, PackagingMode mode, int week, CancellationToken cancellationToken)
    {
        var schedule = await distributionRepository.GetMonthlyDistributionSchedulesAsync(yearId, monthId, cancellationToken);
        return schedule is null
            ? null
            : await distributionRepository.GetPackagingSheetAsync(schedule.Value.MonthlyDistributionId, mode, week, cancellationToken);
    }

    private static Dictionary<Guid, ProgressRow> ProgressRows(PackagingRawData raw)
    {
        var rows = new Dictionary<Guid, ProgressRow>();

        foreach (var s in raw.SchemeItems)
        {
            rows.TryAdd(s.DistributionParticipantId, new ProgressRow(
                s.ParticipantId, s.MonthlyDistributionSchemeId, s.PreparationComplete, s.PackagedDate, s.PostedDate,
                $"{s.DistributionReference}{s.DistributionReferenceSuffix}/{s.ScheduleCode}", $"{s.SchemeIdentifier}: {s.SchemeName}"));
        }

        foreach (var p in raw.Participants)
        {
            var item = raw.Items.FirstOrDefault(i => i.MonthlyDistributionSchemeId == p.MonthlyDistributionSchemeId);
            if (item is null)
            {
                continue;
            }

            rows.TryAdd(p.DistributionParticipantId, new ProgressRow(
                p.ParticipantId, p.MonthlyDistributionSchemeId, item.PreparationComplete, p.PackagedDate, p.PostedDate,
                $"{item.DistributionReference}{item.DistributionReferenceSuffix}/{item.ScheduleCode}", $"{item.SchemeIdentifier}: {item.SchemeName}"));
        }

        return rows;
    }
}
