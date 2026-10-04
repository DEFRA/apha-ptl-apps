using PTL.Core.Customer;

namespace PTL.Api.Tests.Customer;

// In-memory IPendingCustomerUpdateRepository test double so CustomerService can be tested without
// a real database or the spgaPendingCustomerDetailsEditInfo/spgPendingCustomerDetailsEditByCustomerID/
// spdPendingCustomerDetailsEditByCustomerID stored procedures.
internal sealed class FakePendingCustomerUpdateRepository : IPendingCustomerUpdateRepository
{
    private readonly Dictionary<Guid, PendingCustomerUpdate> _byCustomerId = [];

    public void Seed(PendingCustomerUpdate pendingUpdate) => _byCustomerId[pendingUpdate.CustomerId] = pendingUpdate;

    public Task<IReadOnlyList<PendingCustomerUpdateSummaryEntity>> GetSummariesAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<PendingCustomerUpdateSummaryEntity> summaries = _byCustomerId.Values
            .Where(p => p.IsSubmitted && !p.IsDeleted)
            .Select(p => new PendingCustomerUpdateSummaryEntity { CustomerId = p.CustomerId, PendingCustomerUpdateId = p.PendingCustomerUpdateId, QalNumber = "QAL/00001", Name = p.ContactName })
            .ToList();
        return Task.FromResult(summaries);
    }

    public Task<PendingCustomerUpdate?> GetByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_byCustomerId.TryGetValue(customerId, out var pending) && pending.IsSubmitted && !pending.IsDeleted ? pending : null);

    public Task<bool> MarkDecidedAsync(Guid customerId, Guid pendingCustomerUpdateId, CancellationToken cancellationToken = default)
    {
        if (!_byCustomerId.TryGetValue(customerId, out var pending) || pending.PendingCustomerUpdateId != pendingCustomerUpdateId)
        {
            return Task.FromResult(false);
        }

        pending.IsDeleted = true;
        return Task.FromResult(true);
    }
}
