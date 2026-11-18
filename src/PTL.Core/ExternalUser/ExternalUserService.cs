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

        // The lowest user type is the first table that matched; anything else under the same contact id is ignored,
        // except that Viewer and Test Consultant records of the same person are combined.
        var userType = matches.Min(match => match.UserType);
        if (!justLinked && IsViewerOrTestConsultant(userType))
        {
            matches = await TopUpViewerTestConsultantAsync(userType, matches, ssoIdExt, cancellationToken);
            userType = matches.Min(match => match.UserType);
        }

        var rows = matches.Where(match => match.UserType == userType || (IsViewerOrTestConsultant(userType) && IsViewerOrTestConsultant(match.UserType))).ToList();

        if (!justLinked)
        {
            LogIfEmailChanged(normalisedEmail, userType, rows);
        }

        return await BuildResultAsync(userType, rows, displayName, cancellationToken);
    }

    // Email is only a bootstrap: it finds the record to link, and is never consulted for a record that already carries
    // the contact id (apart from picking up a missing Viewer/Test Consultant partner). A record already linked to any
    // identity is left alone, so a changed or re-used email can never move an account to another person.
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

        if (userType == ExternalUserType.Viewer)
        {
            linked = await LinkCompanionAsync(ExternalUserType.TestConsultant, candidates, email, ssoIdExt, linked, cancellationToken);
        }

        return (linked, null);
    }

    // A returning Viewer-only or Test Consultant-only user may have gained the other record since signing in. The email
    // used is the one on the already-linked PTL record (never the token's), so a changed CIDM email cannot attach
    // anything new.
    private async Task<IReadOnlyList<ExternalUserMatch>> TopUpViewerTestConsultantAsync(
        ExternalUserType userType,
        IReadOnlyList<ExternalUserMatch> matches,
        Guid ssoIdExt,
        CancellationToken cancellationToken)
    {
        var companionType = userType == ExternalUserType.Viewer ? ExternalUserType.TestConsultant : ExternalUserType.Viewer;
        if (matches.Any(match => match.UserType == companionType))
        {
            return matches;
        }

        var email = matches.First(match => match.UserType == userType).Email?.Trim();
        if (string.IsNullOrEmpty(email))
        {
            return matches;
        }

        var candidates = await externalUserRepository.FindByEmailAsync(email, cancellationToken);
        return await LinkCompanionAsync(companionType, candidates, email, ssoIdExt, matches, cancellationToken);
    }

    // A person who is both a Viewer and a Test Consultant holds both records under one email. The second record is
    // linked too, but never at the cost of the sign-in: if it is claimed by another identity or is ambiguous, the user
    // still signs in with the record they already had.
    private async Task<IReadOnlyList<ExternalUserMatch>> LinkCompanionAsync(
        ExternalUserType companionType,
        IReadOnlyList<ExternalUserMatch> candidates,
        string email,
        Guid ssoIdExt,
        IReadOnlyList<ExternalUserMatch> current,
        CancellationToken cancellationToken)
    {
        var companions = candidates.Where(candidate => candidate.UserType == companionType).ToList();
        if (companions.Count == 0)
        {
            return current;
        }

        if (companions.Any(row => row.SsoIdExt is not null) || companions.Select(row => row.UserId).Distinct().Count() > 1)
        {
            LogCompanionNotLinked(logger, companionType, companions[0].UserId);
            return current;
        }

        var linkedWithCompanion = await externalUserRepository.LinkAsync(companionType, email, ssoIdExt, cancellationToken);
        if (linkedWithCompanion.Count == 0)
        {
            return current;
        }

        LogLinked(logger, companionType, companions[0].UserId, ssoIdExt);
        return linkedWithCompanion;
    }

    private static bool IsViewerOrTestConsultant(ExternalUserType userType) =>
        userType is ExternalUserType.Viewer or ExternalUserType.TestConsultant;

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

        // Only a Viewer + Test Consultant pair can span two types; an inactive Test Consultant leaves just the Viewer.
        var activeTypes = activeRows.Select(row => row.UserType).Distinct().ToList();
        var resolvedType = activeTypes.Min();
        var roles = activeTypes.SelectMany(type => RolesByUserType[type]).Distinct().ToList();

        return new ExternalUserResolutionResult(
            ExternalUserResolutionStatus.Resolved,
            displayName,
            resolvedType,
            roles,
            labs,
            ParticipantId: singleLab?.ParticipantId,
            LabCode: singleLab?.LabCode,
            ViewerId: UserIdOfType(activeRows, ExternalUserType.Viewer),
            TestConsultantId: UserIdOfType(activeRows, ExternalUserType.TestConsultant),
            LabUserId: UserIdOfType(activeRows, ExternalUserType.LabUser),
            CanOrderOnline: singleLab is not null && await CanOrderOnlineAsync(singleLab.ParticipantId, cancellationToken),
            CanEditDetails: singleLab is not null && await CanEditDetailsAsync(singleLab.ParticipantId, cancellationToken));
    }

    private static Guid? UserIdOfType(List<ExternalUserMatch> rows, ExternalUserType userType) =>
        rows.Where(row => row.UserType == userType).Select(row => (Guid?)row.UserId).FirstOrDefault();

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
        if (lab is null)
        {
            return null;
        }

        return new ExternalLabContext(
            lab.ParticipantId,
            lab.LabCode,
            lab.LabName,
            await CanOrderOnlineAsync(lab.ParticipantId, cancellationToken),
            await CanEditDetailsAsync(lab.ParticipantId, cancellationToken));
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

    // Legacy CanEditCustomer/CanEditParticipant: only the customer's single active participant may edit the details.
    private async Task<bool> CanEditDetailsAsync(Guid participantId, CancellationToken cancellationToken)
    {
        var participant = await participantRepository.GetByIdAsync(participantId, cancellationToken);
        return participant is not null && await SoleActiveParticipant.IsAsync(participantRepository, participant, cancellationToken);
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

    [LoggerMessage(EventId = 125, Level = LogLevel.Warning,
        Message = "{UserType} record not linked alongside the user's other Viewer/Test Consultant record: it is already linked to a different CIDM contact or the email matches several of them. UserId={UserId}")]
    private static partial void LogCompanionNotLinked(ILogger logger, ExternalUserType userType, Guid userId);

    [LoggerMessage(EventId = 124, Level = LogLevel.Warning,
        Message = "CIDM email differs from the PTL record's email; signed in by contact id. UserType={UserType} UserId={UserId}")]
    private static partial void LogEmailChanged(ILogger logger, ExternalUserType userType, Guid userId);
}
