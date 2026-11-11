using PTL.Core.Audit;
using PTL.Core.LabUsers;

namespace PTL.Api.Tests.LabUsers;

// In-memory ILabUserRepository. The rules that live in the stored procedures (uniqueness, sharing, the email checks) are not
// reproduced here - each test scripts the outcome the procedure would return and asserts what the service did with it.
internal sealed class FakeLabUserRepository : ILabUserRepository
{
    public List<LabUserLink> Links { get; } = [];

    public LabUserAddResult AddResult { get; set; } = new(LabUserAddOutcome.Created, Guid.NewGuid());

    public LabUserUpdateOutcome UpdateOutcome { get; set; } = LabUserUpdateOutcome.Updated;

    public List<(Guid ParticipantId, string Name, string Email, bool IsActive)> AddCalls { get; } = [];

    // The audit entry the service asked to be written for each add, as the repository would have built it from the new person's id.
    public List<AuditEntry> AddAudits { get; } = [];

    public List<(Guid ParticipantId, Guid LabUserId, string Name, string Email, bool IsActive, bool ChangeDetails, AuditEntry Audit)> UpdateCalls { get; } = [];

    public Task<IReadOnlyList<LabUserLink>> GetByParticipantAsync(Guid participantId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<LabUserLink>>(Links.Where(link => link.ParticipantId == participantId).OrderBy(link => link.Name).ToList());

    public Task<LabUserLink?> GetAsync(Guid participantId, Guid labUserId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Links.FirstOrDefault(link => link.ParticipantId == participantId && link.LabUserId == labUserId));

    public Task<LabUserLink?> FindByLinkIdAsync(Guid labUserParticipantId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Links.FirstOrDefault(link => link.LabUserParticipantId == labUserParticipantId));

    public Task<LabUserAddResult> AddAsync(
        Guid participantId, string name, string email, bool isActive, Func<Guid, AuditEntry> auditFor, CancellationToken cancellationToken = default)
    {
        AddCalls.Add((participantId, name, email, isActive));
        if (AddResult.LabUserId is { } labUserId)
        {
            AddAudits.Add(auditFor(labUserId));
        }

        return Task.FromResult(AddResult);
    }

    public Task<LabUserUpdateOutcome> UpdateAsync(
        Guid participantId, Guid labUserId, LabUserEdit edit, AuditEntry audit, CancellationToken cancellationToken = default)
    {
        UpdateCalls.Add((participantId, labUserId, edit.Name, edit.Email, edit.IsActive, edit.ChangeDetails, audit));
        return Task.FromResult(UpdateOutcome);
    }
}
