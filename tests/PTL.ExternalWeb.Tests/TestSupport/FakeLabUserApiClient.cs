using PTL.ApiClient;
using PTL.Contracts.LabUsers;
using PTL.Contracts.LoginInstructions;

namespace PTL.ExternalWeb.Tests.TestSupport;

// Test double for ILabUserApiClient: holds one lab's people and records every call, so tests can assert what the controller sent.
internal sealed class FakeLabUserApiClient : ILabUserApiClient
{
    public string LabCode { get; set; } = "0142";

    public string LabName { get; set; } = "Bristol Veterinary Diagnostics";

    public List<LabUserResponse> Users { get; } = [];

    /// <summary>When false every call answers null (the lab or person is not found).</summary>
    public bool Found { get; set; } = true;

    public LabUserSaveResult SaveResult { get; set; } = LabUserSaveResult.Succeeded(LabUserSaveOutcome.Added);

    public LoginInstructionsResponse InstructionsResponse { get; set; } = new(true);

    public List<Guid> ListCalls { get; } = [];

    public List<(Guid ParticipantId, LabUserSaveRequest Request)> AddCalls { get; } = [];

    public List<(Guid ParticipantId, Guid LabUserId, LabUserSaveRequest Request)> UpdateCalls { get; } = [];

    public List<(Guid ParticipantId, Guid LabUserId)> InstructionCalls { get; } = [];

    public Task<LabUserListResponse?> GetListAsync(Guid participantId, CancellationToken cancellationToken = default)
    {
        ListCalls.Add(participantId);
        return Task.FromResult(Found ? new LabUserListResponse(LabCode, LabName, Users.ToList()) : null);
    }

    public Task<LabUserDetailsResponse?> GetAsync(Guid participantId, Guid labUserId, CancellationToken cancellationToken = default)
    {
        var user = Users.FirstOrDefault(candidate => candidate.LabUserId == labUserId);
        return Task.FromResult(Found && user is not null ? new LabUserDetailsResponse(LabCode, LabName, user) : null);
    }

    public Task<LabUserSaveResult?> AddAsync(Guid participantId, LabUserSaveRequest request, CancellationToken cancellationToken = default)
    {
        AddCalls.Add((participantId, request));
        return Task.FromResult(Found ? SaveResult : null);
    }

    public Task<LabUserSaveResult?> UpdateAsync(Guid participantId, Guid labUserId, LabUserSaveRequest request, CancellationToken cancellationToken = default)
    {
        UpdateCalls.Add((participantId, labUserId, request));
        return Task.FromResult(Found ? SaveResult : null);
    }

    public Task<LoginInstructionsResponse?> SendLoginInstructionsAsync(Guid participantId, Guid labUserId, CancellationToken cancellationToken = default)
    {
        InstructionCalls.Add((participantId, labUserId));
        return Task.FromResult(Found ? InstructionsResponse : null);
    }
}
