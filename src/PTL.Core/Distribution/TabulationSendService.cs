using PTL.Contracts.Distribution;

namespace PTL.Core.Distribution;

// The three Tabulations "Send" actions - legacy ResultsTabulation.MonthlyDistributionScheme
// SendToTestConsultant / SendForAssessment / SendForResultsSignOff. As in legacy, a send only changes
// the page's state; nothing is written until Save. Each staged send must be available (per
// TabulationActionRules) at the point it is applied, so a stale or crafted post is refused.
public interface ITabulationSendService
{
    // The Tabulations page with the staged sends applied. Null when the month does not exist.
    Task<TabulationsPage?> PreviewAsync(int yearId, int monthId, IReadOnlyList<TabulationStagedSendRequest> staged, CancellationToken cancellationToken = default);

    // Null when the action is not available once the staged sends are applied.
    Task<TabulationSendResponse?> StageAsync(int yearId, int monthId, TabulationStageRequest request, CancellationToken cancellationToken = default);

    // Legacy Save. False when the month does not exist or any staged send is no longer available.
    Task<bool> SaveAsync(int yearId, int monthId, IReadOnlyList<TabulationStagedSendRequest> staged, CancellationToken cancellationToken = default);
}

public sealed class TabulationSendService(
    IDistributionRepository distributionRepository,
    ITabulationEmailService tabulationEmailService,
    TimeProvider timeProvider) : ITabulationSendService
{
    // Legacy confirm() prompts from Tabulations.aspx.vb RowDataBound.
    public const string SendToTestConsultantQuestion = "Are you sure this Distribution is ready to be sent to the Test Consultant for Comments?";
    public const string SendForAssessmentQuestion = "Are you sure this Distribution is ready to be sent for assessment?";
    public const string SendToResultsSignOffQuestion = "Are you sure this Distribution is ready to be sent for Results Sign-Off?";
    public const string SendToResultsSignOffDiscardQuestion = "Test Consultant Comments have not been Submitted for this Distribution. Clicking Ok will Discard any Comments that Test Consultants have Saved. Are you sure it is ready to be sent for Results Sign-Off?";

    public async Task<TabulationsPage?> PreviewAsync(
        int yearId, int monthId, IReadOnlyList<TabulationStagedSendRequest> staged, CancellationToken cancellationToken = default)
    {
        var state = await LoadAsync(yearId, monthId, cancellationToken);
        if (state is null)
        {
            return null;
        }

        // A send that is no longer available is simply not shown as applied.
        state.Apply(staged ?? []);
        return TabulationsService.ToPage(yearId, monthId, state.Schemes, state.Now);
    }

    public async Task<TabulationSendResponse?> StageAsync(int yearId, int monthId, TabulationStageRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var state = await LoadAsync(yearId, monthId, cancellationToken);
        if (state is null || !Enum.TryParse<TabulationCommand>(request.Command, out var command))
        {
            return null;
        }

        state.Apply(request.Staged ?? []);
        var scheme = state.Schemes.Find(s => s.MonthlyDistributionSchemeId == request.MonthlyDistributionSchemeId);
        var action = scheme is null ? null : state.AvailableAction(scheme, command);
        if (scheme is null || action is null)
        {
            return null;
        }

        return new TabulationSendResponse(
            scheme.MonthlyDistributionSchemeId,
            $"{scheme.SchemeIdentifier}: {scheme.SchemeName}",
            $"{scheme.DistributionReference}{scheme.DistributionReferenceSuffix}/{scheme.ScheduleCode}",
            command.ToString(),
            action.Text,
            Question(scheme, command));
    }

    public async Task<bool> SaveAsync(int yearId, int monthId, IReadOnlyList<TabulationStagedSendRequest> staged, CancellationToken cancellationToken = default)
    {
        var state = await LoadAsync(yearId, monthId, cancellationToken);
        if (state is null || !state.Apply(staged ?? []))
        {
            return false;
        }

        if (state.Writes.Count > 0)
        {
            await distributionRepository.SendTabulationSchemesAsync(state.Writes, cancellationToken);
        }

        // Save has committed; a failed email never undoes it and the scheduler retries it.
        if (state.TestConsultantSends.Count > 0)
        {
            await tabulationEmailService.SendFirstEmailsAsync(state.TestConsultantSends, cancellationToken);
        }

        return true;
    }

    private static string Question(TabulationsSchemeEntity scheme, TabulationCommand command) => command switch
    {
        TabulationCommand.SendToTestConsultant => SendToTestConsultantQuestion,
        TabulationCommand.SendForAssessment => SendForAssessmentQuestion,
        _ when scheme.IsCommentSubmitted => SendToResultsSignOffQuestion,
        _ when !scheme.RequiresAssessment => SendToResultsSignOffDiscardQuestion,
        _ => string.Empty,
    };

    private async Task<StagedState?> LoadAsync(int yearId, int monthId, CancellationToken cancellationToken)
    {
        var schedule = await distributionRepository.GetMonthlyDistributionSchedulesAsync(yearId, monthId, cancellationToken);
        if (schedule is null)
        {
            return null;
        }

        var schemes = await distributionRepository.GetTabulationsAsync(schedule.Value.MonthlyDistributionId, cancellationToken);
        return new StagedState([.. schemes], timeProvider.GetLocalNow().DateTime);
    }

    // The in-memory schemes after the staged sends, as legacy's session MonthlyDistribution.
    private sealed class StagedState(List<TabulationsSchemeEntity> schemes, DateTime now)
    {
        private readonly Dictionary<Guid, Dictionary<Guid, int>> originalPublishStates = schemes.ToDictionary(
            s => s.MonthlyDistributionSchemeId, s => s.Tabulations.ToDictionary(t => t.TabulationId, t => t.PublishState));

        private readonly HashSet<Guid> changed = [];
        private readonly HashSet<Guid> submitComments = [];
        private readonly List<Guid> testConsultantSends = [];

        public List<TabulationsSchemeEntity> Schemes => schemes;

        // The schemes newly sent to the Test Consultant, whose first email is due once Save commits.
        public IReadOnlyList<Guid> TestConsultantSends => testConsultantSends;

        public DateTime Now => now;

        public IReadOnlyList<TabulationSendWrite> Writes => [.. schemes
            .Where(s => changed.Contains(s.MonthlyDistributionSchemeId))
            .Select(s => new TabulationSendWrite(
                s.MonthlyDistributionSchemeId,
                s.IsReadyForTestConsultant,
                s.IsReadyForResultsSignOff,
                s.IsReadyForAssessor,
                submitComments.Contains(s.MonthlyDistributionSchemeId),
                [.. s.Tabulations
                    .Where(t => t.PublishState != originalPublishStates[s.MonthlyDistributionSchemeId][t.TabulationId])
                    .Select(t => (t.TabulationId, t.PublishState))]))];

        // True when every staged send could be applied.
        public bool Apply(IReadOnlyList<TabulationStagedSendRequest> staged)
        {
            var allApplied = true;
            foreach (var send in staged)
            {
                var scheme = schemes.Find(s => s.MonthlyDistributionSchemeId == send.MonthlyDistributionSchemeId);
                if (scheme is null
                    || !Enum.TryParse<TabulationCommand>(send.Command, out var command)
                    || AvailableAction(scheme, command) is null)
                {
                    allApplied = false;
                    continue;
                }

                Send(scheme, command);
                changed.Add(scheme.MonthlyDistributionSchemeId);
            }

            return allApplied;
        }

        public TabulationAction? AvailableAction(TabulationsSchemeEntity scheme, TabulationCommand command)
        {
            var actions = TabulationActionRules.Compute(scheme, now);
            var action = command switch
            {
                TabulationCommand.SendToTestConsultant => actions.SendToTestConsultant,
                TabulationCommand.SendForAssessment => actions.SendForAssessment,
                TabulationCommand.SendToResultsSignOff => actions.SendToResultsSignOff,
                _ => null,
            };

            return action is { IsEnabled: true, IsLabel: false } && action.Command == command ? action : null;
        }

        private void Send(TabulationsSchemeEntity scheme, TabulationCommand command)
        {
            var today = now.Date;
            switch (command)
            {
                case TabulationCommand.SendToTestConsultant:
                    scheme.IsReadyForTestConsultant ??= today;
                    testConsultantSends.Add(scheme.MonthlyDistributionSchemeId);
                    break;
                case TabulationCommand.SendForAssessment:
                    scheme.IsReadyForAssessor ??= today;
                    break;
                default:
                    SendForResultsSignOff(scheme, today);
                    break;
            }
        }

        // Legacy SendForResultsSignOff: the Test Consultant's tabulation is never published, every
        // other tabulation awaits sign-off; IsReadyForTestConsultant is cleared and unsubmitted
        // comments are discarded on Save (sppSubmitComment). IsReadyForAssessor keeps its date.
        private void SendForResultsSignOff(TabulationsSchemeEntity scheme, DateTime today)
        {
            scheme.IsReadyForResultsSignOff = today;
            foreach (var tabulation in scheme.Tabulations)
            {
                tabulation.PublishState = (int)(tabulation.TabulationId == scheme.TestConsultantTabulationId
                    ? TabulationPublishState.NotAuthorized
                    : TabulationPublishState.AwaitingSignOff);
            }

            scheme.IsReadyForTestConsultant = null;
            if (!scheme.IsCommentSubmitted)
            {
                submitComments.Add(scheme.MonthlyDistributionSchemeId);
            }
        }
    }
}
