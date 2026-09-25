using PTL.ApiClient;
using PTL.Contracts.Participant;

namespace PTL.InternalWeb.Tests.TestSupport;

internal sealed class FakeParticipantApiClient : IParticipantApiClient
{
    public ParticipantSearchResponse SearchResponse { get; set; } = new([], 0, 1, 20);
    public ParticipantResponse? ParticipantResponse { get; set; }
    public ParticipantResponse? CreatedOrUpdatedResponse { get; set; }
    public ParticipantSaveResult? SaveResult { get; set; }
    public bool UpdateReturnsNull { get; set; }
    public Exception? ExceptionToThrow { get; set; }

    public Task<IReadOnlyList<ParticipantSummaryResponse>> GetParticipantsAsync(Guid customerId, bool includeInactive = false, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ParticipantSummaryResponse>>(SearchResponse.Items);

    public Task<ParticipantResponse?> GetParticipantAsync(Guid participantId, CancellationToken cancellationToken = default) =>
        Task.FromResult(ParticipantResponse);

    public Task<ParticipantSearchResponse> SearchParticipantsAsync(ParticipantSearchRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(SearchResponse);

    public Task<ParticipantSaveResult> CreateParticipantAsync(ParticipantRequest request, CancellationToken cancellationToken = default)
    {
        if (ExceptionToThrow is not null)
        {
            return Task.FromResult(new ParticipantSaveResult(false, null, new Dictionary<string, string[]> { [string.Empty] = [ExceptionToThrow.Message] }));
        }

        if (SaveResult is not null)
        {
            return Task.FromResult(SaveResult);
        }

        var participant = CreatedOrUpdatedResponse ?? new ParticipantResponse(
            Guid.NewGuid(), request.SsoId, request.CustomerId, request.LabCode, request.LabName, request.LabTypeId,
            request.ContactName, request.Organisation, request.Address1, request.Address2, request.Address3,
            request.Address4, request.Address5, request.CountryId, request.Telephone, request.Fax, request.Email,
            request.Email2, request.Comments, request.IsActive, null, false, null);
        return Task.FromResult(new ParticipantSaveResult(true, participant, new Dictionary<string, string[]>()));
    }

    public Task<ParticipantSaveResult> UpdateParticipantAsync(Guid participantId, ParticipantRequest request, CancellationToken cancellationToken = default)
    {
        if (ExceptionToThrow is not null)
        {
            return Task.FromResult(new ParticipantSaveResult(false, null, new Dictionary<string, string[]> { [string.Empty] = [ExceptionToThrow.Message] }));
        }

        if (SaveResult is not null)
        {
            return Task.FromResult(SaveResult);
        }

        if (UpdateReturnsNull)
        {
            return Task.FromResult(new ParticipantSaveResult(false, null, new Dictionary<string, string[]>()));
        }

        var participant = CreatedOrUpdatedResponse ?? new ParticipantResponse(
            participantId, request.SsoId, request.CustomerId, request.LabCode, request.LabName, request.LabTypeId,
            request.ContactName, request.Organisation, request.Address1, request.Address2, request.Address3,
            request.Address4, request.Address5, request.CountryId, request.Telephone, request.Fax, request.Email,
            request.Email2, request.Comments, request.IsActive, null, false, null);
        return Task.FromResult(new ParticipantSaveResult(true, participant, new Dictionary<string, string[]>()));
    }
}
