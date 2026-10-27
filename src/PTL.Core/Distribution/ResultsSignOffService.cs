using PTL.Contracts.Distribution;

namespace PTL.Core.Distribution;

public interface IResultsSignOffRepository
{
    // The tabulations awaiting final approval, oldest month first.
    Task<IReadOnlyList<ResultsSignOffMonthEntity>> GetQueueAsync(CancellationToken cancellationToken = default);

    // Marks the tabulation as not authorised (legacy BtnReject: tab.DontPublish() then Save), but only while it is still
    // awaiting sign-off, so a late reject cannot overwrite another sign-off user's decision.
    Task<ResultsSignOffOutcome> RejectAsync(Guid tabulationId, CancellationToken cancellationToken = default);

    // Legacy BtnAuthorise: copies the draft tabulation into the published tables, queues the "Tabulation Published" emails and
    // marks it authorised - all in one transaction, and only while it is still awaiting sign-off. creationDate is when it is
    // published; signatoryUserId names the user who signs it.
    Task<ResultsSignOffOutcome> AuthoriseAsync(Guid tabulationId, Guid? signatoryUserId, DateTime creationDate, CancellationToken cancellationToken = default);
}

public interface IResultsSignOffService
{
    Task<ResultsSignOffQueueResponse> GetQueueAsync(CancellationToken cancellationToken = default);

    Task<ResultsSignOffOutcome> RejectAsync(Guid tabulationId, CancellationToken cancellationToken = default);

    Task<ResultsSignOffOutcome> AuthoriseAsync(Guid tabulationId, Guid? signatoryUserId, CancellationToken cancellationToken = default);
}

public enum ResultsSignOffOutcome
{
    Done,
    NotFound,

    // Someone has already authorised or rejected the tabulation.
    NotAwaiting,

    // Authorising needs a signed-in user to sign it.
    NoSignatory,
}

// tblDistributionTabulation.fldPublishState - legacy SignOff.Tabulation.PublishStatus.
public static class ResultsSignOffState
{
    public const int AwaitingSignOff = 0;
    public const int Authorized = 1;
    public const int NotAuthorized = 2;
}

// Legacy Results Sign-off/SignOff.aspx.vb BuildTable: the wording and the grouping come from there.
public sealed class ResultsSignOffService(IResultsSignOffRepository repository, TimeProvider timeProvider) : IResultsSignOffService
{
    public const string AwaitingStatus = "Awaiting Sign-off";

    public Task<ResultsSignOffOutcome> RejectAsync(Guid tabulationId, CancellationToken cancellationToken = default) =>
        repository.RejectAsync(tabulationId, cancellationToken);

    // Legacy stamped the publication with DateTime.Now (local time).
    public Task<ResultsSignOffOutcome> AuthoriseAsync(Guid tabulationId, Guid? signatoryUserId, CancellationToken cancellationToken = default) =>
        repository.AuthoriseAsync(tabulationId, signatoryUserId, timeProvider.GetLocalNow().DateTime, cancellationToken);

    public async Task<ResultsSignOffQueueResponse> GetQueueAsync(CancellationToken cancellationToken = default)
    {
        var months = await repository.GetQueueAsync(cancellationToken);

        return new ResultsSignOffQueueResponse(
            [.. months.Select(month => new ResultsSignOffMonthResponse(
                month.YearId,
                month.MonthId,
                [.. month.Schemes.Select(ToResponse)]))]);
    }

    private static ResultsSignOffSchemeResponse ToResponse(ResultsSignOffSchemeEntity scheme) =>
        new(
            scheme.MonthlyDistributionSchemeId,
            $"{scheme.DistributionReference}{scheme.DistributionReferenceSuffix}/{scheme.ScheduleCode} {scheme.SchemeIdentifier}: {scheme.SchemeName}",
            scheme.TargetDate,
            scheme.TestConsultants,
            scheme.Assessors,
            [.. scheme.Tabulations.Select(t => new ResultsSignOffTabulationResponse(
                t.DistributionTabulationId,
                t.Name,
                t.PublishCount,
                t.SingleParticipantTabulation,
                Availability(t),
                AwaitingStatus))]);

    // Anything that is not open to participants reads "Viewers and TCs Only", as in legacy.
    private static string Availability(ResultsSignOffTabulationEntity tabulation) =>
        (tabulation.AvailableToParticipants, tabulation.AvailableToViewers) switch
        {
            (true, true) => "All",
            (true, false) => "Participants Only",
            _ => "Viewers and TCs Only",
        };
}
