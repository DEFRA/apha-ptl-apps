using System.Globalization;
using PTL.Contracts.Distribution;

namespace PTL.Core.Distribution;

// Amend Results - legacy Scheduling/AmendResults.aspx(.vb) over AmendedResults.MonthlyDistributionScheme.
public interface IAmendResultsService
{
    Task<AmendResultsResponse?> GetAsync(Guid monthlyDistributionSchemeId, CancellationToken cancellationToken = default);

    Task<AmendResultsSaveResult?> SaveAsync(
        Guid monthlyDistributionSchemeId, AmendResultsSaveRequest request, string auditWho, CancellationToken cancellationToken = default);
}

public sealed class AmendResultsService(IDistributionRepository distributionRepository) : IAmendResultsService
{
    private const string NotScoredText = "Not Scored";

    public async Task<AmendResultsResponse?> GetAsync(Guid monthlyDistributionSchemeId, CancellationToken cancellationToken = default)
    {
        var source = await distributionRepository.GetAmendResultsAsync(monthlyDistributionSchemeId, cancellationToken);
        if (source is null)
        {
            return null;
        }

        var ratingOptions = await distributionRepository.GetRatingOptionsAsync(cancellationToken);
        var ratingSamples = RatingSamples(source);

        return new AmendResultsResponse(
            source.MonthlyDistributionSchemeId,
            $"{source.DistributionReferenceFull} {source.SchemeIdentifier}: {source.SchemeName}",
            source.RequiresAssessment,
            source.StoreRatings,
            [.. source.Tests.Where(t => t.Samples.Count > 0).Select(t => ToTest(source, t))],
            [.. source.Participants.Select(p => ToParticipant(source, p))],
            [.. ratingOptions.Select(r => new AmendRatingOptionResponse(r.RatingId, r.Rating))],
            ratingSamples,
            source.StoreRatings ? [.. ratingSamples.Select(s => Statistics(source, ratingOptions, s.SampleId))] : []);
    }

    public async Task<AmendResultsSaveResult?> SaveAsync(
        Guid monthlyDistributionSchemeId, AmendResultsSaveRequest request, string auditWho, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var source = await distributionRepository.GetAmendResultsAsync(monthlyDistributionSchemeId, cancellationToken);
        if (source is null)
        {
            return null;
        }

        var ratingIds = (await distributionRepository.GetRatingOptionsAsync(cancellationToken)).Select(r => r.RatingId).ToHashSet();
        var changes = new ChangeSet(source, ratingIds, request);
        changes.Collect();

        if (changes.Errors.Count > 0)
        {
            return new AmendResultsSaveResult(false, false, string.Empty, changes.Errors);
        }

        if (!changes.HasChanges)
        {
            return Saved();
        }

        // Legacy BtnSave/BtnApply: IsAuditDirty diverts through Audit.aspx for a reason first.
        if (changes.IsAuditDirty && string.IsNullOrWhiteSpace(request.AuditWhy))
        {
            return new AmendResultsSaveResult(false, true, AuditObjectName(source), new Dictionary<string, string>());
        }

        await distributionRepository.SaveAmendResultsAsync(changes.ToWrite(auditWho, changes.IsAuditDirty ? request.AuditWhy! : string.Empty), cancellationToken);
        return Saved();
    }

    // Legacy MonthlyDistributionScheme.AuditObjectName.
    private static string AuditObjectName(AmendResultsSource source) =>
        $"Amended Results for {source.DistributionReferenceFull} {source.SchemeIdentifier}: {source.SchemeName}";

    private static AmendResultsSaveResult Saved() => new(true, false, string.Empty, new Dictionary<string, string>());

    private static AmendTestResponse ToTest(AmendResultsSource source, AmendSourceTest test) => new(
        test.TestId,
        test.Name,
        test.IsAssessmentSubmitted,
        [.. test.MethodItems.Select(ToItem)],
        [.. test.ResultItems.Select(ToItem)],
        [.. test.CategoryItems.Select(c => new AmendCategoryItemResponse(c.ItemId, c.Name))],
        [.. test.Samples.Select(s => new AmendSampleResponse(s.SampleId, s.SampleNumber))],
        [.. test.MethodItems
            .Where(m => source.IntendedMethodResults.ContainsKey(m.ItemId))
            .Select(m => new AmendIntendedValueResponse(m.ItemId, Guid.Empty, source.IntendedMethodResults[m.ItemId], string.Empty))],
        [.. test.ResultItems.SelectMany(r => test.Samples
            .Where(s => source.IntendedResultItemResults.ContainsKey((s.SampleId, r.ItemId)))
            .Select(s =>
            {
                var intended = source.IntendedResultItemResults[(s.SampleId, r.ItemId)];
                return new AmendIntendedValueResponse(r.ItemId, s.SampleId, intended.Value, intended.Value2);
            }))]);

    private static AmendItemResponse ToItem(AmendSourceItem item) => new(item.ItemId, item.Name, item.FormatString, item.Message, item.IsRange);

    private static AmendParticipantResponse ToParticipant(AmendResultsSource source, AmendSourceParticipant participant)
    {
        Guid TestOf(Func<AmendSourceTest, bool> owns) => source.Tests.Find(t => owns(t))?.TestId ?? Guid.Empty;

        return new AmendParticipantResponse(
            participant.DistributionParticipantId,
            participant.LabCode,
            participant.NilReturnReason,
            HasResults(participant),
            HasRatingResults(participant),
            [.. participant.MethodResults.Select(m => new AmendParticipantValueResponse(
                TestOf(t => t.MethodItems.Exists(i => i.ItemId == m.Key)), m.Key, Guid.Empty, m.Value))],
            [.. participant.ResultItemResults.Select(r => new AmendParticipantValueResponse(
                TestOf(t => t.ResultItems.Exists(i => i.ItemId == r.Key.ResultItemId)), r.Key.ResultItemId, r.Key.SampleId, r.Value))],
            [.. participant.CategoryResults.Select(c => new AmendParticipantScoreResponse(
                TestOf(t => t.CategoryItems.Exists(i => i.ItemId == c.Key.CategoryItemId)), c.Key.CategoryItemId, c.Key.SampleId, c.Value.Score))],
            [.. participant.Assessments.Select(a => new AmendAssessmentResponse(a.Key.TestId, a.Key.SampleId, a.Value.Comment, a.Value.Score))],
            [.. participant.Ratings.Select(r => new AmendRatingResponse(r.Key, r.Value.RatingId))]);
    }

    // Legacy: a participant with no method/result/assessment rows gets the editable Nil Return cell.
    private static bool HasResults(AmendSourceParticipant participant) =>
        participant.MethodResults.Count > 0 || participant.ResultItemResults.Count > 0 || participant.Assessments.Count > 0;

    // Legacy: the rating drop-down is only drawn when the participant returned method or result items.
    private static bool HasRatingResults(AmendSourceParticipant participant) =>
        participant.MethodResults.Count > 0 || participant.ResultItemResults.Count > 0;

    private static List<AmendSampleResponse> RatingSamples(AmendResultsSource source)
    {
        var samples = new List<AmendSourceSample>();
        foreach (var sample in source.Tests.SelectMany(t => t.Samples))
        {
            if (!samples.Exists(s => s.SampleId == sample.SampleId))
            {
                samples.Add(sample);
            }
        }

        samples.Sort((a, b) => string.CompareOrdinal(a.SampleNumber, b.SampleNumber));
        return [.. samples.Select(s => new AmendSampleResponse(s.SampleId, s.SampleNumber))];
    }

    // Port of the page's CalculateStats() JavaScript, which runs when the page loads.
    internal static AmendSampleStatisticsResponse Statistics(AmendResultsSource source, IReadOnlyList<AmendRatingOption> ratingOptions, Guid sampleId)
    {
        var ratingText = ratingOptions.ToDictionary(r => r.RatingId, r => r.Rating);
        var participantCount = 0;
        var labsReporting = 0;
        var sum = 0;
        var isNotScored = false;

        foreach (var participant in source.Participants.Where(HasRatingResults))
        {
            if (!participant.Ratings.TryGetValue(sampleId, out var rating) || !ratingText.TryGetValue(rating.RatingId, out var text))
            {
                continue;
            }

            if (text == "2")
            {
                labsReporting++;
            }

            if (text is "-1" or "0" or "1" or "2")
            {
                participantCount++;
                sum += int.Parse(text, CultureInfo.InvariantCulture);
            }
            else if (text == NotScoredText)
            {
                isNotScored = true;
                participantCount++;
            }
        }

        var count = participantCount.ToString(CultureInfo.InvariantCulture);
        if (isNotScored)
        {
            return new AmendSampleStatisticsResponse(sampleId, NotScoredText, NotScoredText, count, NotScoredText);
        }

        var mean = participantCount != 0 && sum != 0 ? ToFixed1((double)sum / participantCount) : "0.0";
        var percentage = participantCount != 0 && labsReporting != 0 ? ToFixed1((double)labsReporting / participantCount * 100) : "0.0";
        return new AmendSampleStatisticsResponse(sampleId, mean, labsReporting.ToString(CultureInfo.InvariantCulture), count, percentage + "%");
    }

    private static string ToFixed1(double value) =>
        Math.Round(value, 1, MidpointRounding.AwayFromZero).ToString("0.0", CultureInfo.InvariantCulture);

    // Works out what the posted grid changed, validating only the values that changed.
    private sealed class ChangeSet(AmendResultsSource source, HashSet<Guid> ratingIds, AmendResultsSaveRequest request)
    {
        private const string FormatPrefix = "Input must be of format: ";

        private readonly Dictionary<Guid, AmendSourceTest> testByMethodItem = source.Tests
            .SelectMany(t => t.MethodItems.Select(m => (m.ItemId, Test: t))).ToDictionary(x => x.ItemId, x => x.Test);

        private readonly Dictionary<Guid, (AmendSourceTest Test, AmendSourceItem Item)> resultItems = source.Tests
            .SelectMany(t => t.ResultItems.Select(r => (r.ItemId, Value: (t, r)))).ToDictionary(x => x.ItemId, x => x.Value);

        private readonly Dictionary<Guid, AmendSourceTest> testByCategoryItem = source.Tests
            .SelectMany(t => t.CategoryItems.Select(c => (c.ItemId, Test: t))).ToDictionary(x => x.ItemId, x => x.Test);

        private readonly Dictionary<Guid, AmendSourceParticipant> participants = source.Participants.ToDictionary(p => p.DistributionParticipantId);

        private readonly List<AmendRangeWrite> ranges = [];
        private readonly List<AmendIntendedResultWrite> intendedResults = [];
        private readonly List<AmendIntendedMethodWrite> intendedMethods = [];
        private readonly Dictionary<Guid, ParticipantChanges> participantChanges = [];

        public Dictionary<string, string> Errors { get; } = [];

        public bool IsAuditDirty { get; private set; }

        public bool HasChanges => ranges.Count > 0 || intendedResults.Count > 0 || intendedMethods.Count > 0 || participantChanges.Count > 0;

        public void Collect()
        {
            var isRange = resultItems.ToDictionary(r => r.Key, r => r.Value.Item.IsRange);
            foreach (var range in request.Ranges ?? [])
            {
                if (resultItems.TryGetValue(range.ResultItemId, out var item) && range.IsRange != item.Item.IsRange)
                {
                    isRange[range.ResultItemId] = range.IsRange;
                    ranges.Add(new AmendRangeWrite(item.Item.ItemId, item.Test.TestId, item.Item.ResultItemTypeId, item.Item.Order, range.IsRange));
                }
            }

            CollectIntendedMethods();
            CollectIntendedResults(isRange);
            CollectParticipantValues();
            CollectAssessments();
            CollectNilReturnsAndRatings();
        }

        public AmendResultsWrite ToWrite(string auditWho, string auditWhy) => new(
            auditWho,
            auditWhy,
            ranges,
            [.. source.Participants
                .Where(p => participantChanges.ContainsKey(p.DistributionParticipantId))
                .Select(p => participantChanges[p.DistributionParticipantId].ToWrite(p))],
            intendedResults,
            intendedMethods);

        private void CollectIntendedMethods()
        {
            foreach (var posted in request.IntendedMethodResults ?? [])
            {
                if (!testByMethodItem.TryGetValue(posted.ItemId, out var test)
                    || !source.IntendedMethodResults.TryGetValue(posted.ItemId, out var current))
                {
                    continue;
                }

                var value = posted.Value ?? string.Empty;
                var item = test.MethodItems.First(m => m.ItemId == posted.ItemId);
                if (value != current && Validate(AmendResultsFieldKeys.IntendedMethod(posted.ItemId), item, value))
                {
                    intendedMethods.Add(new AmendIntendedMethodWrite(test.TestId, posted.ItemId, value));
                    IsAuditDirty = true;
                }
            }
        }

        private void CollectIntendedResults(Dictionary<Guid, bool> isRange)
        {
            foreach (var posted in request.IntendedResultItemResults ?? [])
            {
                if (!resultItems.TryGetValue(posted.ItemId, out var item)
                    || !source.IntendedResultItemResults.TryGetValue((posted.SampleId, posted.ItemId), out var current))
                {
                    continue;
                }

                var value = posted.Value ?? string.Empty;
                var value2 = isRange[posted.ItemId] && posted.Value2 is not null ? posted.Value2 : current.Value2;
                var valueChanged = value != current.Value
                    && Validate(AmendResultsFieldKeys.IntendedResult(posted.SampleId, posted.ItemId, 1), item.Item, value);
                var value2Changed = value2 != current.Value2
                    && Validate(AmendResultsFieldKeys.IntendedResult(posted.SampleId, posted.ItemId, 2), item.Item, value2);

                if (valueChanged || value2Changed)
                {
                    intendedResults.Add(new AmendIntendedResultWrite(
                        item.Test.TestId, posted.SampleId, posted.ItemId,
                        valueChanged ? value : current.Value, valueChanged,
                        value2Changed ? value2 : current.Value2, value2Changed));
                    IsAuditDirty = true;
                }
            }
        }

        private void CollectParticipantValues()
        {
            foreach (var posted in request.MethodResults ?? [])
            {
                if (participants.TryGetValue(posted.DistributionParticipantId, out var participant)
                    && testByMethodItem.TryGetValue(posted.ItemId, out var test)
                    && participant.MethodResults.TryGetValue(posted.ItemId, out var current))
                {
                    var value = posted.Value ?? string.Empty;
                    var item = test.MethodItems.First(m => m.ItemId == posted.ItemId);
                    if (value != current && Validate(AmendResultsFieldKeys.ParticipantMethod(posted.DistributionParticipantId, posted.ItemId), item, value))
                    {
                        For(participant).MethodResults.Add(new AmendMethodResultWrite(test.TestId, posted.ItemId, value));
                        IsAuditDirty = true;
                    }
                }
            }

            foreach (var posted in request.ResultItemResults ?? [])
            {
                if (participants.TryGetValue(posted.DistributionParticipantId, out var participant)
                    && resultItems.TryGetValue(posted.ItemId, out var item)
                    && participant.ResultItemResults.TryGetValue((posted.SampleId, posted.ItemId), out var current))
                {
                    var value = posted.Value ?? string.Empty;
                    if (value != current && Validate(AmendResultsFieldKeys.ParticipantResult(posted.DistributionParticipantId, posted.SampleId, posted.ItemId), item.Item, value))
                    {
                        For(participant).ResultItemResults.Add(new AmendResultItemWrite(item.Test.TestId, posted.SampleId, posted.ItemId, value));
                        IsAuditDirty = true;
                    }
                }
            }

            foreach (var posted in request.CategoryResults ?? [])
            {
                if (participants.TryGetValue(posted.DistributionParticipantId, out var participant)
                    && testByCategoryItem.TryGetValue(posted.CategoryItemId, out var test)
                    && participant.CategoryResults.TryGetValue((posted.SampleId, posted.CategoryItemId), out var current)
                    && posted.Score is >= 1 and <= 5
                    && posted.Score != current.Score)
                {
                    For(participant).CategoryResults.Add(new AmendCategoryWrite(test.TestId, posted.SampleId, posted.CategoryItemId, current.SampleTestId, posted.Score));
                    IsAuditDirty = true;
                }
            }
        }

        private void CollectAssessments()
        {
            foreach (var posted in request.Assessments ?? [])
            {
                if (!participants.TryGetValue(posted.DistributionParticipantId, out var participant)
                    || !participant.Assessments.TryGetValue((posted.TestId, posted.SampleId), out var current)
                    || !source.Tests.Exists(t => t.TestId == posted.TestId && t.IsAssessmentSubmitted))
                {
                    continue;
                }

                var comment = posted.Comment ?? string.Empty;
                var commentChanged = comment != current.Comment;
                if (commentChanged && comment.Length > 2000)
                {
                    Errors[AmendResultsFieldKeys.AssessmentComment(posted.DistributionParticipantId, posted.TestId, posted.SampleId)] =
                        FormatPrefix + "The assessment comment cannot exceed 2000 characters";
                    continue;
                }

                var scoreChanged = posted.Score is >= 1 and <= 5 && posted.Score != current.Score;
                if (commentChanged || scoreChanged)
                {
                    For(participant).Assessments.Add(new AmendAssessmentWrite(
                        current.SampleReceiptId, comment, commentChanged, scoreChanged ? posted.Score : current.Score, scoreChanged));
                    IsAuditDirty = true;
                }
            }
        }

        private void CollectNilReturnsAndRatings()
        {
            // The Nil Return box repeats on every test; as in legacy the last changed one wins.
            foreach (var posted in request.NilReturns ?? [])
            {
                if (!participants.TryGetValue(posted.DistributionParticipantId, out var participant) || HasResults(participant))
                {
                    continue;
                }

                var reason = posted.NilReturnReason ?? string.Empty;
                if (reason == participant.NilReturnReason)
                {
                    continue;
                }

                if (reason.Length > 100)
                {
                    Errors[AmendResultsFieldKeys.NilReturn(posted.DistributionParticipantId)] = FormatPrefix + "Less than 100 characters";
                    continue;
                }

                For(participant).NilReturnReason = reason;
            }

            foreach (var posted in request.Ratings ?? [])
            {
                if (participants.TryGetValue(posted.DistributionParticipantId, out var participant)
                    && HasRatingResults(participant)
                    && participant.Ratings.TryGetValue(posted.SampleId, out var current)
                    && ratingIds.Contains(posted.RatingId)
                    && posted.RatingId != current.RatingId)
                {
                    For(participant).Ratings.Add(new AmendRatingWrite(current.SampleRatingId, posted.RatingId));
                }
            }
        }

        private bool Validate(string key, AmendSourceItem item, string value)
        {
            var error = IntendedResultsValidator.ValidateValue(item.FormatString, item.Message, value);
            if (error is null)
            {
                return true;
            }

            Errors[key] = error;
            return false;
        }

        private ParticipantChanges For(AmendSourceParticipant participant)
        {
            if (!participantChanges.TryGetValue(participant.DistributionParticipantId, out var changes))
            {
                changes = new ParticipantChanges();
                participantChanges[participant.DistributionParticipantId] = changes;
            }

            return changes;
        }
    }

    private sealed class ParticipantChanges
    {
        public string? NilReturnReason { get; set; }
        public List<AmendMethodResultWrite> MethodResults { get; } = [];
        public List<AmendResultItemWrite> ResultItemResults { get; } = [];
        public List<AmendAssessmentWrite> Assessments { get; } = [];
        public List<AmendRatingWrite> Ratings { get; } = [];
        public List<AmendCategoryWrite> CategoryResults { get; } = [];

        public AmendParticipantWrite ToWrite(AmendSourceParticipant participant) => new(
            participant.DistributionParticipantId,
            NilReturnReason ?? participant.NilReturnReason,
            MethodResults,
            ResultItemResults,
            Assessments,
            Ratings,
            CategoryResults);
    }
}
