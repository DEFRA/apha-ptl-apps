using PTL.ApiClient;
using PTL.Contracts.Participant;

namespace PTL.InternalWeb.Tests.TestSupport;

// Test double for IParticipantSchemeApiClient so ParticipantSchemeController tests don't need a
// real HTTP call to PTL.Api.
internal sealed class FakeParticipantSchemeApiClient : IParticipantSchemeApiClient
{
    public ParticipantSchemeResponse? ParticipantSchemeResponse { get; set; }
    public ParticipantSchemeSaveResult SaveResult { get; set; } = new(true, null, new Dictionary<string, string[]>());

    public Task<ParticipantSchemeResponse?> GetParticipantSchemeAsync(Guid participantSchemeId, CancellationToken cancellationToken = default) =>
        Task.FromResult(ParticipantSchemeResponse);

    public Task<ParticipantSchemeSaveResult> CreateParticipantSchemeAsync(CreateParticipantSchemeRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(SaveResult);

    public Task<ParticipantSchemeSaveResult> UpdateParticipantSchemeAsync(Guid participantSchemeId, UpdateParticipantSchemeRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(SaveResult);

    public Task<ParticipantSchemeSaveResult> DeleteParticipantSchemeAsync(Guid participantSchemeId, CancellationToken cancellationToken = default) =>
        Task.FromResult(SaveResult);
}
