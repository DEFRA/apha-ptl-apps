namespace PTL.Core.Distribution;

public interface ICurrentDistributionsService
{
    Task<IReadOnlyList<CurrentDistributionData>> GetCurrentDistributionsAsync(Guid participantId, CancellationToken cancellationToken = default);
}

public sealed class CurrentDistributionsService(IDistributionRepository distributionRepository) : ICurrentDistributionsService
{
    public Task<IReadOnlyList<CurrentDistributionData>> GetCurrentDistributionsAsync(Guid participantId, CancellationToken cancellationToken = default) =>
        distributionRepository.GetCurrentDistributionsAsync(participantId, cancellationToken);
}
