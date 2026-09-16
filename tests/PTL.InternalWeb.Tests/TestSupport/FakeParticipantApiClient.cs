using PTL.ApiClient;
using PTL.Contracts.Participant;

namespace PTL.InternalWeb.Tests.TestSupport;

internal sealed class FakeParticipantApiClient : IParticipantApiClient
{
    public ParticipantSearchResponse SearchResponse { get; set; } = new([], 0, 1, 20);
    public ParticipantResponse? ParticipantResponse { get; set; }
    public ParticipantResponse? CreatedOrUpdatedResponse { get; set; }

    public Task<IReadOnlyList<ParticipantSummaryResponse>> GetParticipantsAsync(Guid customerId, bool includeInactive = false, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ParticipantSummaryResponse>>(SearchResponse.Items);

    public Task<ParticipantResponse?> GetParticipantAsync(Guid participantId, CancellationToken cancellationToken = default) =>
        Task.FromResult(ParticipantResponse);

    public Task<ParticipantSearchResponse> SearchParticipantsAsync(ParticipantSearchRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(SearchResponse);

    public Task<ParticipantResponse> CreateParticipantAsync(CreateParticipantRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(CreatedOrUpdatedResponse ?? new ParticipantResponse(
            Guid.NewGuid(), request.SsoId, request.CustomerId, request.LabCode, request.LabName, request.LabTypeId,
            request.ContactName, request.Organisation, request.Address1, request.Address2, request.Address3,
            request.Address4, request.Address5, request.CountryId, request.Telephone, request.Fax, request.Email,
            request.Email2, request.Comments, request.IsActive, null, false, null));

    public Task<ParticipantResponse?> UpdateParticipantAsync(Guid participantId, UpdateParticipantRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(CreatedOrUpdatedResponse ?? new ParticipantResponse(
            participantId, request.SsoId, request.CustomerId, request.LabCode, request.LabName, request.LabTypeId,
            request.ContactName, request.Organisation, request.Address1, request.Address2, request.Address3,
            request.Address4, request.Address5, request.CountryId, request.Telephone, request.Fax, request.Email,
            request.Email2, request.Comments, request.IsActive, null, false, null));

    public Task<ParticipantResponse?> DeactivateParticipantAsync(Guid participantId, CancellationToken cancellationToken = default) =>
        Task.FromResult(CreatedOrUpdatedResponse ?? new ParticipantResponse(
            participantId, Guid.NewGuid(), Guid.NewGuid(), "LAB-01", "Sample Lab", Guid.NewGuid(), "Contact", "Org",
            "1 Street", string.Empty, string.Empty, string.Empty, string.Empty, Guid.NewGuid(), "01234 567890",
            string.Empty, "contact@example.com", string.Empty, string.Empty, false, DateTime.UtcNow, false, null));

    public Task<ParticipantResponse?> ReactivateParticipantAsync(Guid participantId, CancellationToken cancellationToken = default) =>
        Task.FromResult(CreatedOrUpdatedResponse ?? new ParticipantResponse(
            participantId, Guid.NewGuid(), Guid.NewGuid(), "LAB-01", "Sample Lab", Guid.NewGuid(), "Contact", "Org",
            "1 Street", string.Empty, string.Empty, string.Empty, string.Empty, Guid.NewGuid(), "01234 567890",
            string.Empty, "contact@example.com", string.Empty, string.Empty, true, null, false, null));
}
