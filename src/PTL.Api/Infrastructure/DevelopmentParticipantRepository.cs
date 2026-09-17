using PTL.Core.Participant;
using PTL.Data.Participant;

namespace PTL.Api.Infrastructure;

// TEMPORARY (Development only): the local dev database is empty or unreachable, so the
// Participant screens would otherwise render empty or throw. This decorator falls back to in-memory
// sample data whenever the real query returns nothing OR throws (e.g. SQL Server not running).
// Safe to delete this file and its single registration in Program.cs at any time - ParticipantRepository,
// ParticipantService, the controllers and views are completely unaffected.
internal sealed class DevelopmentParticipantRepository(ParticipantRepository inner) : IParticipantRepository
{
    private static readonly IReadOnlyList<ParticipantSummaryEntity> DummySummaries =
    [
        new() { ParticipantId = Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111"), CustomerId = Guid.Parse("11111111-1111-1111-1111-111111111111"), LabCode = "LAB/00001", LabName = "Sample Laboratories Ltd - Main Lab", ContactName = "Alex Sample", IsActive = true },
        new() { ParticipantId = Guid.Parse("aaaaaaaa-2222-2222-2222-222222222222"), CustomerId = Guid.Parse("11111111-1111-1111-1111-111111111111"), LabCode = "LAB/00002", LabName = "Sample Laboratories Ltd - Satellite Lab", ContactName = "Jordan Sample", IsActive = true },
        new() { ParticipantId = Guid.Parse("aaaaaaaa-3333-3333-3333-333333333333"), CustomerId = Guid.Parse("22222222-2222-2222-2222-222222222222"), LabCode = "LAB/00003", LabName = "Northfield Veterinary Practice", ContactName = "Sam Northfield", IsActive = true },
        new() { ParticipantId = Guid.Parse("aaaaaaaa-4444-4444-4444-444444444444"), CustomerId = Guid.Parse("33333333-3333-3333-3333-333333333333"), LabCode = "LAB/00004", LabName = "Old Mill Research Institute", ContactName = "Morgan Oldmill", IsActive = false }
    ];

    private static readonly IReadOnlyDictionary<Guid, Participant> DummyParticipants = DummySummaries.ToDictionary(
        s => s.ParticipantId,
        s => new Participant
        {
            ParticipantId = s.ParticipantId,
            SsoId = Guid.NewGuid(),
            CustomerId = s.CustomerId,
            LabCode = s.LabCode,
            LabName = s.LabName,
            LabTypeId = Guid.Empty,
            ContactName = s.ContactName,
            Organisation = s.LabName,
            Address1 = "1 Sample Street",
            Address2 = "Sample District",
            Address3 = string.Empty,
            Address4 = string.Empty,
            Address5 = string.Empty,
            CountryId = Guid.Empty,
            Telephone = "01234 567890",
            Fax = string.Empty,
            Email = "sample.participant@example.com",
            Email2 = string.Empty,
            Comments = string.Empty,
            IsActive = s.IsActive,
            InactiveDate = s.IsActive ? null : new DateTime(2025, 6, 1),
            InactiveError = false,
            InactiveErrorDate = null
        });

    public async Task<Participant?> GetByIdAsync(Guid participantId, CancellationToken cancellationToken = default)
    {
        Participant? participant;
        try
        {
            participant = await inner.GetByIdAsync(participantId, cancellationToken);
        }
        catch (Exception)
        {
            participant = null;
        }

        return participant ?? DummyParticipants.GetValueOrDefault(participantId);
    }

    public async Task<Participant?> GetBySsoIdAsync(Guid ssoId, CancellationToken cancellationToken = default)
    {
        Participant? participant;
        try
        {
            participant = await inner.GetBySsoIdAsync(ssoId, cancellationToken);
        }
        catch (Exception)
        {
            participant = null;
        }

        return participant ?? DummyParticipants.Values.FirstOrDefault(p => p.SsoId == ssoId);
    }

    public async Task<IReadOnlyList<ParticipantSummaryEntity>> GetSummariesAsync(Guid customerId, bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ParticipantSummaryEntity> participants;
        try
        {
            participants = await inner.GetSummariesAsync(customerId, includeInactive, cancellationToken);
        }
        catch (Exception)
        {
            participants = [];
        }

        if (participants.Count > 0)
        {
            return participants;
        }

        return DummySummaries
            .Where(p => p.CustomerId == customerId && (includeInactive || p.IsActive))
            .ToList();
    }

    // Create/Update always go straight to the real database - faking a write would be misleading.
    public Task<Participant> CreateAsync(Participant participant, CancellationToken cancellationToken = default) =>
        inner.CreateAsync(participant, cancellationToken);

    public Task<Participant?> UpdateAsync(Participant participant, CancellationToken cancellationToken = default) =>
        inner.UpdateAsync(participant, cancellationToken);
}
