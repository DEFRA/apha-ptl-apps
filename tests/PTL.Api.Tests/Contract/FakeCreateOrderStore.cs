using PTL.Core.Contract.CreateOrder;
using PTL.Core.Contract.PendingOrder;

namespace PTL.Api.Tests.Contract;

// In-memory stand-in for the pending-order tables behind both ICreateOrderRepository and IPendingOrderRepository, so
// CreateOrderService can be tested end to end. A line's Price is 10 per ticked month, standing in for
// dbo.fnGetPendingParticipantSchemePrice.
internal sealed class FakeCreateOrderStore : ICreateOrderRepository, IPendingOrderRepository
{
    public const decimal PricePerMonth = 10m;

    private readonly List<PendingOrderEntity> _orders = [];
    private readonly Dictionary<Guid, List<PendingOrderSchemeEntity>> _lines = [];

    public List<AvailableSchemeEntity> AvailableSchemes { get; } = [];
    public List<(int YearId, ParticipatingSchemeEntity Scheme)> Participating { get; } = [];
    public string CurrencySymbol { get; set; } = "£";
    public int CreatedOrders { get; private set; }

    public IReadOnlyList<PendingOrderEntity> Orders => _orders;

    public void SeedSubmittedOrder(Guid customerId, int yearId) =>
        _orders.Add(new PendingOrderEntity { PendingContractId = Guid.NewGuid(), CustomerId = customerId, YearId = yearId, IsSubmitted = true });

    public IReadOnlyList<PendingOrderSchemeEntity> LinesFor(Guid pendingContractId) => _lines.GetValueOrDefault(pendingContractId, []);

    public void SeedLine(Guid pendingContractId, PendingOrderSchemeEntity line)
    {
        line.PendingContractId = pendingContractId;
        if (!_lines.TryGetValue(pendingContractId, out var lines))
        {
            _lines[pendingContractId] = lines = [];
        }

        lines.Add(line);
    }

    public Task<PendingOrderEntity?> GetPendingOrderAsync(Guid customerId, int yearId, bool isSubmitted, CancellationToken cancellationToken = default) =>
        Task.FromResult(_orders.FirstOrDefault(o => o.CustomerId == customerId && o.YearId == yearId && o.IsSubmitted == isSubmitted && !o.IsDeleted));

    public Task CreatePendingOrderAsync(Guid pendingContractId, Guid customerId, int yearId, CancellationToken cancellationToken = default)
    {
        CreatedOrders++;
        _orders.Add(new PendingOrderEntity { PendingContractId = pendingContractId, CustomerId = customerId, YearId = yearId, CurrencySymbol = CurrencySymbol });
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AvailableSchemeEntity>> GetAvailableSchemesAsync(int yearId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AvailableSchemeEntity>>(AvailableSchemes);

    public Task<IReadOnlyList<ParticipatingSchemeEntity>> GetParticipatingSchemesAsync(Guid participantId, int yearId, int participatingYearId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ParticipatingSchemeEntity>>(Participating.Where(p => p.YearId == yearId).Select(p => Copy(p.Scheme)).ToList());

    public Task InsertSchemeAsync(PendingOrderSchemeEntity scheme, bool nonUk, CancellationToken cancellationToken = default)
    {
        SeedLine(scheme.PendingContractId, scheme);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PendingOrderSchemeEntity>> GetSchemesAsync(Guid pendingContractId, CancellationToken cancellationToken = default)
    {
        var lines = _lines.GetValueOrDefault(pendingContractId, []);
        foreach (var line in lines)
        {
            line.Price = line.Months().Count(m => m.Selected) * PricePerMonth;
        }

        return Task.FromResult<IReadOnlyList<PendingOrderSchemeEntity>>([.. lines]);
    }

    public Task UpdateSchemeAsync(PendingOrderSchemeEntity scheme, bool dataConsentDeclarationGiven, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task DeleteSchemeAsync(Guid pendingParticipantSchemeId, CancellationToken cancellationToken = default)
    {
        foreach (var lines in _lines.Values)
        {
            lines.RemoveAll(l => l.PendingParticipantSchemeId == pendingParticipantSchemeId);
        }

        return Task.CompletedTask;
    }

    public Task<bool> MarkDecidedAsync(Guid customerId, Guid pendingContractId, CancellationToken cancellationToken = default)
    {
        _orders.Single(o => o.PendingContractId == pendingContractId).IsDeleted = true;
        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<PendingOrderSummaryEntity>> GetSummariesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<PendingOrderEntity?> GetByIdAsync(Guid pendingContractId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<PendingOrderSchemeEntity?> GetSchemeByIdAsync(Guid pendingParticipantSchemeId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task UpdatePurchaseOrderNumberAsync(Guid pendingContractId, string purchaseOrderNumber, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task QueueNotificationAsync(PendingOrderNotification notification, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<IReadOnlyList<OnlineOrderEmailEntity>> GetUnsentNotificationsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task MarkNotificationSentAsync(Guid notificationId, DateTime sentAt, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    // The service merges months into the row it is given, so hand it a copy rather than the seeded instance.
    private static ParticipatingSchemeEntity Copy(ParticipatingSchemeEntity source)
    {
        var copy = new ParticipatingSchemeEntity
        {
            ParticipantId = source.ParticipantId,
            SchemeId = source.SchemeId,
            Identifier = source.Identifier,
            Name = source.Name,
            Schedule = source.Schedule
        };
        copy.MergeMonthsFrom(source);
        return copy;
    }
}
