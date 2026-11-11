using PTL.Contracts.ExternalUser;

namespace PTL.Core.ExternalUser;

/// <summary>
/// One row of <c>vwExternalUser</c>: a PTL external user record, plus - for a Participant or Lab User - the
/// lab (participant record) the row stands for. A Participant or Lab User with several labs yields several rows.
/// </summary>
public sealed record ExternalUserMatch
{
    public ExternalUserType UserType { get; init; }

    /// <summary>The participant, viewer, test consultant or lab user id, depending on <see cref="UserType"/>.</summary>
    public Guid UserId { get; init; }

    public string? Email { get; init; }

    /// <summary>The CIDM contact id the record is linked to, or <see langword="null"/> while unlinked.</summary>
    public Guid? SsoIdExt { get; init; }

    /// <summary>Whether this row currently gives access (an inactive participant/test consultant, or an inactive lab link).</summary>
    public bool IsActive { get; init; }

    public Guid? LabParticipantId { get; init; }

    public string? LabCode { get; init; }

    public string? LabName { get; init; }
}
