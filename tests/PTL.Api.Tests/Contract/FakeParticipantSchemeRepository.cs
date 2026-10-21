using PTL.Core.Participant;

namespace PTL.Api.Tests.Contract;

// In-memory IParticipantSchemeRepository test double for ContractService.RemoveContractItemAsync.
internal sealed class FakeParticipantSchemeRepository : IParticipantSchemeRepository
{
    private readonly Dictionary<Guid, ParticipantSchemeRecord> _records = [];

    public IReadOnlyDictionary<Guid, ParticipantSchemeRecord> Records => _records;

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

    // Set when the removal was audited (legacy SetAudit).
    public ParticipantSchemeAudit? LastAudit { get; private set; }

    public Task<bool> RemoveAsync(ParticipantSchemeRecord record, ParticipantSchemeAudit audit, CancellationToken cancellationToken = default)
    {
        LastAudit = audit;
        return UpdateAsync(record, cancellationToken);
    }

    // What GetSchemeContextAsync returns, and the nonUk flag it was last asked about.
    public ParticipantSchemeContext Context { get; set; } = new();

    public bool? LastNonUk { get; private set; }

    public List<OtherParticipantScheme> OtherParticipantSchemes { get; } = [];

    public Task<ParticipantSchemeContext> GetSchemeContextAsync(Guid schemeId, Guid contractId, bool nonUk, CancellationToken cancellationToken = default)
    {
        LastNonUk = nonUk;
        return Task.FromResult(Context);
    }

    public Task<IReadOnlyList<OtherParticipantScheme>> GetByParticipantAndSchemeAsync(Guid participantId, Guid schemeId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<OtherParticipantScheme>>([.. OtherParticipantSchemes]);
}
