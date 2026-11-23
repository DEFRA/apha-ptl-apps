namespace PTL.Core.Distribution;

public partial interface IDistributionRepository
{
    // spgTabulations (@SsoIdExt branch, script 0017) - every published tabulation the CIDM contact id may view as an
    // External Test Consultant or Viewer, plus the selected lab's own when participantId is a lab that contact id owns
    // or is mapped to as a Lab User.
    Task<IReadOnlyList<PublishedTabulationSummaryEntity>> GetTabulationsForExternalUserAsync(
        Guid ssoIdExt, Guid? participantId, CancellationToken cancellationToken = default);
}
