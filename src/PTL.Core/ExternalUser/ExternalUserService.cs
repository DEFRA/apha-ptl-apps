using Microsoft.Extensions.Logging;
using PTL.Contracts.ExternalUser;
using PTL.Core.Contract.CreateOrder;
using PTL.Core.Customer;
using PTL.Core.Participant;

namespace PTL.Core.ExternalUser;

public sealed partial class ExternalUserService(
    IExternalUserRepository externalUserRepository,
    IParticipantRepository participantRepository,
    ICustomerRepository customerRepository,
    ILogger<ExternalUserService> logger) : IExternalUserService
{
    private static readonly IReadOnlyList<ExternalUserMatch> NoMatches = [];

    // Legacy Generate Login always granted a Participant (and an External Test Consultant) the Viewer SSO role as
    // well - "View reports" only ever checked IsViewer - so those roles imply Viewer without a Viewer record. A Lab
    // User works like a Participant for now (the lab-selection screen will narrow what they can do), so it carries
    // Participant too and every existing role-gated page keeps working for them.
    private static readonly Dictionary<ExternalUserType, IReadOnlyList<string>> RolesByUserType =
        new()
        {
            [ExternalUserType.Participant] = [ExternalRoleNames.Participant, ExternalRoleNames.Viewer],
            [ExternalUserType.Viewer] = [ExternalRoleNames.Viewer],
            [ExternalUserType.TestConsultant] = [ExternalRoleNames.TestConsultant, ExternalRoleNames.Viewer],
            [ExternalUserType.LabUser] = [ExternalRoleNames.LabUser, ExternalRoleNames.Participant, ExternalRoleNames.Viewer]
        };

    public async Task<ExternalUserResolutionResult> ResolveAsync(
        Guid ssoIdExt,
        string email,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        var normalisedEmail = email.Trim();
        var matches = await externalUserRepository.FindBySsoIdExtAsync(ssoIdExt, cancellationToken);
        var justLinked = false;

        if (matches.Count == 0)
        {
            var (linked, denial) = await LinkByEmailAsync(ssoIdExt, normalisedEmail, cancellationToken);
            if (denial is { } status)
            {
                return ExternalUserResolutionResult.Denied(status, displayName);
            }

            matches = linked;
            justLinked = true;
        }

        // The lowest user type is the first table that matched; anything else under the same contact id is ignored.
        var userType = matches.Min(match => match.UserType);
        var rows = matches.Where(match => match.UserType == userType).ToList();

        if (!justLinked)
        {
            LogIfEmailChanged(normalisedEmail, userType, rows);
        }

        return await BuildResultAsync(userType, rows, displayName, cancellationToken);
    }

    // Email is only a one-time bootstrap: it finds the record to link, and is never consulted again once the
    // contact id is on it. A record already linked to any identity is left alone, so a changed or re-used email
    // can never move an account to another person.
    private async Task<(IReadOnlyList<ExternalUserMatch> Matches, ExternalUserResolutionStatus? Denial)> LinkByEmailAsync(
        Guid ssoIdExt, string email, CancellationToken cancellationToken)
    {
        if (email.Length == 0)
        {
            return (NoMatches, ExternalUserResolutionStatus.NotFound);
        }

        var candidates = await externalUserRepository.FindByEmailAsync(email, cancellationToken);
        if (candidates.Count == 0)
        {
            return (NoMatches, ExternalUserResolutionStatus.NotFound);
        }

        var userType = candidates.Min(candidate => candidate.UserType);
        var rows = candidates.Where(candidate => candidate.UserType == userType).ToList();

        if (rows.Any(row => row.SsoIdExt is not null))
        {
            LogLinkedToAnotherIdentity(logger, userType, rows[0].UserId);
            return (NoMatches, ExternalUserResolutionStatus.LinkedToAnotherIdentity);
        }

        // A Participant's email legitimately appears on several lab rows; for every other type it identifies a person.
        var userCount = rows.Select(row => row.UserId).Distinct().Count();
        if (userType != ExternalUserType.Participant && userCount > 1)
        {
            LogAmbiguousEmail(logger, userType, userCount);
            return (NoMatches, ExternalUserResolutionStatus.Ambiguous);
        }

        var linked = await externalUserRepository.LinkAsync(userType, email, ssoIdExt, cancellationToken);
        if (linked.Count == 0)
        {
            return (NoMatches, ExternalUserResolutionStatus.NotFound);
        }

        LogLinked(logger, userType, linked[0].UserId, ssoIdExt);
        return (linked, null);
    }

    private async Task<ExternalUserResolutionResult> BuildResultAsync(
        ExternalUserType userType,
        List<ExternalUserMatch> rows,
        string displayName,
        CancellationToken cancellationToken)
    {
        var activeRows = rows.Where(row => row.IsActive).ToList();
        if (activeRows.Count == 0)
        {
            var status = userType == ExternalUserType.LabUser
                ? ExternalUserResolutionStatus.NoActiveLab
                : ExternalUserResolutionStatus.Inactive;
            return ExternalUserResolutionResult.Denied(status, displayName);
        }

        var labs = ActiveLabs(activeRows);

        // With one lab there is nothing to choose, so the user acts for it straight away; with several, the
        // lab-selection screen sets the lab and nothing is scoped to a lab until then.
        var singleLab = labs.Count == 1 ? labs[0] : null;
        var userId = rows[0].UserId;

        return new ExternalUserResolutionResult(
            ExternalUserResolutionStatus.Resolved,
            displayName,
            userType,
            RolesByUserType[userType],
            labs,
            ParticipantId: singleLab?.ParticipantId,
            LabCode: singleLab?.LabCode,
            ViewerId: userType == ExternalUserType.Viewer ? userId : null,
            TestConsultantId: userType == ExternalUserType.TestConsultant ? userId : null,
            LabUserId: userType == ExternalUserType.LabUser ? userId : null,
            CanOrderOnline: singleLab is not null && await CanOrderOnlineAsync(singleLab.ParticipantId, cancellationToken));
    }

    public async Task<IReadOnlyList<ExternalLab>> GetLabsAsync(Guid ssoIdExt, CancellationToken cancellationToken = default)
    {
        var matches = await externalUserRepository.FindBySsoIdExtAsync(ssoIdExt, cancellationToken);
        if (matches.Count == 0)
        {
            return [];
        }

        // The same user type sign-in resolved to: the lowest one, so a lab is never offered from a lesser match.
        var userType = matches.Min(match => match.UserType);
        return ActiveLabs(matches.Where(match => match.UserType == userType && match.IsActive));
    }

    public async Task<ExternalLabContext?> GetLabAsync(Guid ssoIdExt, Guid participantId, CancellationToken cancellationToken = default)
    {
        var labs = await GetLabsAsync(ssoIdExt, cancellationToken);
        var lab = labs.FirstOrDefault(candidate => candidate.ParticipantId == participantId);
        return lab is null
            ? null
            : new ExternalLabContext(lab.ParticipantId, lab.LabCode, lab.LabName, await CanOrderOnlineAsync(lab.ParticipantId, cancellationToken));
    }

    // Only Participant and Lab User rows carry a lab; a lab seen twice (a link listed again) counts once.
    private static List<ExternalLab> ActiveLabs(IEnumerable<ExternalUserMatch> activeRows) => activeRows
        .Where(row => row.LabParticipantId.HasValue)
        .Select(row => new ExternalLab(row.LabParticipantId.GetValueOrDefault(), row.LabCode ?? string.Empty, row.LabName ?? string.Empty))
        .DistinctBy(lab => lab.ParticipantId)
        .ToList();

    private async Task<bool> CanOrderOnlineAsync(Guid participantId, CancellationToken cancellationToken)
    {
        var participant = await participantRepository.GetByIdAsync(participantId, cancellationToken);
        if (participant is null)
        {
            return false;
        }

        var customer = await customerRepository.GetByIdAsync(participant.CustomerId, cancellationToken);
        return await OnlineOrderingEligibility.CanOrderOnlineAsync(participantRepository, participant, customer, cancellationToken);
    }

    // Signing in never depends on the emails still matching - the contact id decides - but a difference
    // means the CIDM account or the PTL record was edited, which an administrator may need to know about.
    private void LogIfEmailChanged(string email, ExternalUserType userType, List<ExternalUserMatch> rows)
    {
        if (email.Length > 0 && !rows.Any(row => string.Equals(row.Email, email, StringComparison.OrdinalIgnoreCase)))
        {
            LogEmailChanged(logger, userType, rows[0].UserId);
        }
    }

    [LoggerMessage(EventId = 121, Level = LogLevel.Information,
        Message = "External user linked to CIDM contact. UserType={UserType} UserId={UserId} ContactId={ContactId}")]
    private static partial void LogLinked(ILogger logger, ExternalUserType userType, Guid userId, Guid contactId);

    [LoggerMessage(EventId = 122, Level = LogLevel.Warning,
        Message = "CIDM sign-in denied: the email matches a record already linked to a different CIDM contact. UserType={UserType} UserId={UserId}")]
    private static partial void LogLinkedToAnotherIdentity(ILogger logger, ExternalUserType userType, Guid userId);

    [LoggerMessage(EventId = 123, Level = LogLevel.Warning,
        Message = "CIDM sign-in denied: the email matches {UserCount} {UserType} records, so no single user can be chosen")]
    private static partial void LogAmbiguousEmail(ILogger logger, ExternalUserType userType, int userCount);

    [LoggerMessage(EventId = 124, Level = LogLevel.Warning,
        Message = "CIDM email differs from the PTL record's email; signed in by contact id. UserType={UserType} UserId={UserId}")]
    private static partial void LogEmailChanged(ILogger logger, ExternalUserType userType, Guid userId);
}
