namespace PTL.Core.Contract.Renew;

// Both result sets of spgContractMerge, mirroring legacy
// PtaBusinessObjects.DataAccess.Contracts.ContractMergeDataAccess.GetContractMergeInfoCollection.
public sealed record ContractMergeData(
    IReadOnlyList<RenewableContractEntity> Contracts,
    IReadOnlyList<RenewableContractItemEntity> ParticipantSchemes);

public interface IContractMergeRepository
{
    // spgContractMerge - the single legacy data source for the Renew Contracts screen. It already
    // applies every legacy filter (current year with delay, fldIsRemoved = 0, fldDateOfLeaving IS
    // NULL) and resolves each item's next-year scheme via fldSharedId + fnGetNextYearWithDelayId().
    Task<ContractMergeData> GetContractMergeInfoAsync(Guid customerId, CancellationToken cancellationToken = default);
}
