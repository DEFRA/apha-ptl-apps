namespace PTL.Core.Distribution;

// One call of an existing legacy stored procedure, with the parameters legacy passed it.
public sealed record AssessorCommand(string Procedure, IReadOnlyList<KeyValuePair<string, object?>> Parameters)
{
    // "EXEC dbo.spuAssessmentEntry @ParticipantId = @ParticipantId, ..." - the values travel as parameters.
    public string CommandText => $"EXEC dbo.{Procedure} " + string.Join(", ", Parameters.Select(p => $"@{p.Key} = @{p.Key}"));
}

// CategoryChanges describes what changed in the categories and their criteria (null when nothing did), for the audit log.
public sealed record AssessorEntryPlan(IReadOnlyList<AssessorCommand> Commands, string? CategoryChanges)
{
    public bool HasChanges => Commands.Count > 0;
}

// Legacy Assessor AssessmentEntry.DataPortal_Update with CategoryItemResultCollection.Update: nothing is written when nothing
// changed; otherwise spiAuditAssessmentItem (only when the comment or overall score changed), spuAssessmentEntry stamping the
// assessor and date, then each changed category (inserted the first time, updated after) with its criteria added or removed.
// Pure, so the writes can be checked without a database; the data layer runs the commands in one transaction.
public static class AssessorEntryPlanner
{
    // Legacy AssessmentEntry.DataPortal_Update: SetAuditProperties(assessorName, "Assessment Entry").
    public const string AuditReason = "Assessment Entry";

    // tblSampleReceipt.fldAssessorName and the audit row's fldAuditWho are varchar(50).
    public const int NameLength = 50;

    public static AssessorEntryPlan Plan(AssessorEntryEntity current, AssessorEntrySave save, string assessorName)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(save);
        ArgumentNullException.ThrowIfNull(assessorName);

        var name = assessorName.Length > NameLength ? assessorName[..NameLength] : assessorName;
        var comment = save.Comment ?? string.Empty;
        var commentChanged = !string.Equals(comment, current.Comment, StringComparison.Ordinal);
        var scoreChanged = save.OverallScore != current.Score;
        var assessorChanged = save.AssessorUserId != current.AssessorId || !string.Equals(name, current.AssessorName, StringComparison.Ordinal);

        var (categoryCommands, categoryChanges) = PlanCategories(current, save);
        if (!commentChanged && !scoreChanged && !assessorChanged && categoryCommands.Count == 0)
        {
            return new AssessorEntryPlan([], null);
        }

        List<AssessorCommand> commands = [];
        if (commentChanged || scoreChanged)
        {
            commands.Add(new AssessorCommand(
                "spiAuditAssessmentItem",
                [
                    new("SampleReceiptId", current.SampleReceiptId),
                    new("AuditWho", name),
                    new("AuditWhy", AuditReason),
                    new("AssessmentComment", comment),
                    new("AssessmentCommentUpdate", commentChanged),
                    new("Score", save.OverallScore),
                    new("ScoreUpdate", scoreChanged),
                ]));
        }

        commands.Add(new AssessorCommand(
            "spuAssessmentEntry",
            [
                new("ParticipantId", save.ParticipantId),
                new("SampleTestId", save.SampleTestId),
                new("Score", save.OverallScore),
                new("AssessorId", save.AssessorUserId),
                new("AssessorName", name),
                new("AssessmentDate", save.AssessmentDate),
                new("AssessmentComment", comment),
            ]));
        commands.AddRange(categoryCommands);

        return new AssessorEntryPlan(commands, categoryChanges);
    }

    private static (List<AssessorCommand> Commands, string? Changes) PlanCategories(AssessorEntryEntity current, AssessorEntrySave save)
    {
        var requested = save.Categories.ToDictionary(c => c.CategoryItemId);
        List<AssessorCommand> commands = [];
        List<string> changes = [];

        foreach (var category in current.Categories)
        {
            if (!requested.TryGetValue(category.CategoryItemId, out var request))
            {
                continue;
            }

            var known = category.Criteria.Select(c => c.CriterionItemId).ToHashSet();
            if (request.CriterionItemIds.Any(id => !known.Contains(id)))
            {
                throw new InvalidOperationException($"A criterion is not part of the category {category.CategoryItemId}.");
            }

            var ticked = request.CriterionItemIds.ToHashSet();
            var isNew = category.ActualScore is null;
            var added = category.Criteria.Where(c => ticked.Contains(c.CriterionItemId) && (isNew || !c.IsSelected)).ToList();
            var removed = isNew ? [] : category.Criteria.Where(c => c.IsSelected && !ticked.Contains(c.CriterionItemId)).ToList();
            var scoreChanged = isNew || category.ActualScore != request.Score;
            if (!scoreChanged && added.Count == 0 && removed.Count == 0)
            {
                continue;
            }

            commands.Add(new AssessorCommand(
                isNew ? "spiActualCategoryItemResult" : "spuActualCategoryItemResult",
                [
                    new("ActualResultsId", save.ParticipantId),
                    new("CategoryItemId", category.CategoryItemId),
                    new("SampleTestId", save.SampleTestId),
                    new("CategoryScore", request.Score),
                ]));
            commands.AddRange(added.Select(c => Criterion("spiActualCriterionItemResult", save, c)));
            commands.AddRange(removed.Select(c => Criterion("spdActualCriterionItemResult", save, c)));
            changes.Add(Describe(category, request.Score, scoreChanged, added, removed));
        }

        return (commands, changes.Count == 0 ? null : string.Join("; ", changes));
    }

    private static AssessorCommand Criterion(string procedure, AssessorEntrySave save, AssessorCriterionEntity criterion) =>
        new(
            procedure,
            [
                new("ActualResultsId", save.ParticipantId),
                new("CriterionItemId", criterion.CriterionItemId),
                new("SampleTestId", save.SampleTestId),
            ]);

    private static string Describe(AssessorCategoryEntity category, int score, bool scoreChanged, List<AssessorCriterionEntity> added, List<AssessorCriterionEntity> removed)
    {
        List<string> parts = [];
        if (scoreChanged)
        {
            parts.Add(category.ActualScore is { } old ? $"score {old}->{score}" : $"score {score}");
        }

        if (added.Count > 0)
        {
            parts.Add($"criteria added {string.Join(", ", added.Select(c => c.Name))}");
        }

        if (removed.Count > 0)
        {
            parts.Add($"criteria removed {string.Join(", ", removed.Select(c => c.Name))}");
        }

        return $"{category.Name}: {string.Join(", ", parts)}";
    }
}
