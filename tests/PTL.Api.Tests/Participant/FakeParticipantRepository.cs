using PTL.Core.Participant;
using ParticipantEntity = PTL.Core.Participant.Participant;

namespace PTL.Api.Tests.Participant;

internal sealed class FakeParticipantRepository : IParticipantRepository
{
    private readonly Dictionary<Guid, ParticipantEntity> _participants = [];

    public Task<ParticipantEntity?> GetByIdAsync(Guid participantId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_participants.TryGetValue(participantId, out var participant) ? Clone(participant) : null);

    public Task<IReadOnlyList<ParticipantSummaryEntity>> GetSummariesAsync(Guid customerId, bool includeInactive, CancellationToken cancellationToken = default)
    {
        var filtered = _participants.Values.Where(p => p.CustomerId == customerId && (includeInactive || p.IsActive));
        IReadOnlyList<ParticipantSummaryEntity> summaries = filtered
            .Select(p => new ParticipantSummaryEntity
            {
                ParticipantId = p.ParticipantId,
                CustomerId = p.CustomerId,
                LabCode = p.LabCode,
                LabName = p.LabName,
                ContactName = p.ContactName,
                IsActive = p.IsActive
            })
            .ToList();

        return Task.FromResult(summaries);
    }

    public Task<ParticipantEntity?> GetBySsoIdAsync(Guid ssoId, CancellationToken cancellationToken = default)
    {
        var participant = _participants.Values.FirstOrDefault(p => p.SsoId == ssoId);
        return Task.FromResult(participant is null ? null : Clone(participant));
    }

    public Task<ParticipantEntity> CreateAsync(ParticipantEntity participant, CancellationToken cancellationToken = default)
    {
        participant.ParticipantId = participant.ParticipantId == Guid.Empty ? Guid.NewGuid() : participant.ParticipantId;
        _participants[participant.ParticipantId] = Clone(participant);
        return Task.FromResult(Clone(participant));
    }

    public Task<ParticipantEntity?> UpdateAsync(ParticipantEntity participant, CancellationToken cancellationToken = default)
    {
        if (!_participants.ContainsKey(participant.ParticipantId))
        {
            return Task.FromResult<ParticipantEntity?>(null);
        }

        _participants[participant.ParticipantId] = Clone(participant);
        return Task.FromResult<ParticipantEntity?>(Clone(participant));
    }

    private static ParticipantEntity Clone(ParticipantEntity source) => new()
    {
        ParticipantId = source.ParticipantId,
        SsoId = source.SsoId,
        CustomerId = source.CustomerId,
        LabCode = source.LabCode,
        LabName = source.LabName,
        LabTypeId = source.LabTypeId,
        ContactName = source.ContactName,
        Organisation = source.Organisation,
        Address1 = source.Address1,
        Address2 = source.Address2,
        Address3 = source.Address3,
        Address4 = source.Address4,
        Address5 = source.Address5,
        CountryId = source.CountryId,
        Telephone = source.Telephone,
        Fax = source.Fax,
        Email = source.Email,
        Email2 = source.Email2,
        Comments = source.Comments,
        IsActive = source.IsActive,
        InactiveDate = source.InactiveDate,
        InactiveError = source.InactiveError,
        InactiveErrorDate = source.InactiveErrorDate
    };
}
