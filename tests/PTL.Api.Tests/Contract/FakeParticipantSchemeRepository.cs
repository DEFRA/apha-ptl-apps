using PTL.Core.Participant;

namespace PTL.Api.Tests.Contract;

// In-memory IParticipantSchemeRepository test double for ContractService.RemoveContractItemAsync.
internal sealed class FakeParticipantSchemeRepository : IParticipantSchemeRepository
{
    private readonly Dictionary<Guid, ParticipantSchemeRecord> _records = [];

    public void Add(ParticipantSchemeRecord record) => _records[record.ParticipantSchemeId] = record;

    public Task<ParticipantSchemeRecord?> GetByIdAsync(Guid participantSchemeId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_records.TryGetValue(participantSchemeId, out var record) ? record : null);

    public Task<ParticipantSchemeRecord> CreateAsync(ParticipantSchemeRecord record, CancellationToken cancellationToken = default)
    {
        _records[record.ParticipantSchemeId] = record;
        return Task.FromResult(record);
    }

    public Task<bool> UpdateAsync(ParticipantSchemeRecord record, CancellationToken cancellationToken = default)
    {
        if (!_records.ContainsKey(record.ParticipantSchemeId))
        {
            return Task.FromResult(false);
        }

        _records[record.ParticipantSchemeId] = record;
        return Task.FromResult(true);
    }
}
