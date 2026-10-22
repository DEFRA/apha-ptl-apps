using Microsoft.Extensions.Logging;
using PTL.Core.Search;

namespace PTL.Api.Tests.Search;

// Records the criteria the service hands over and returns canned rows (in the order given, as the
// stored procedures would); can be told to throw.
internal sealed class FakeSearchRepository : ISearchRepository
{
    public IReadOnlyList<CustomerSearchRow> CustomerRows { get; set; } = [];

    public IReadOnlyList<ParticipantSearchRow> ParticipantRows { get; set; } = [];

    public Exception? Failure { get; set; }

    public List<CustomerSearchCriteria> CustomerCalls { get; } = [];

    public List<ParticipantSearchCriteria> ParticipantCalls { get; } = [];

    public CancellationToken LastCancellationToken { get; private set; }

    public Task<IReadOnlyList<CustomerSearchRow>> SearchCustomersAsync(CustomerSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        CustomerCalls.Add(criteria);
        LastCancellationToken = cancellationToken;
        return Failure is null ? Task.FromResult(CustomerRows) : Task.FromException<IReadOnlyList<CustomerSearchRow>>(Failure);
    }

    public Task<IReadOnlyList<ParticipantSearchRow>> SearchParticipantsAsync(ParticipantSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        ParticipantCalls.Add(criteria);
        LastCancellationToken = cancellationToken;
        return Failure is null ? Task.FromResult(ParticipantRows) : Task.FromException<IReadOnlyList<ParticipantSearchRow>>(Failure);
    }

    public IReadOnlyList<DistributionNumberSearchRow> DistributionNumberRows { get; set; } = [];

    public IReadOnlyList<SampleNumberSearchRow> SampleNumberRows { get; set; } = [];

    public List<DistributionNumberSearchCriteria> DistributionNumberCalls { get; } = [];

    public List<SampleNumberSearchCriteria> SampleNumberCalls { get; } = [];

    public Task<IReadOnlyList<DistributionNumberSearchRow>> SearchByDistributionNumberAsync(DistributionNumberSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        DistributionNumberCalls.Add(criteria);
        LastCancellationToken = cancellationToken;
        return Failure is null ? Task.FromResult(DistributionNumberRows) : Task.FromException<IReadOnlyList<DistributionNumberSearchRow>>(Failure);
    }

    public Task<IReadOnlyList<SampleNumberSearchRow>> SearchBySampleNumberAsync(SampleNumberSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        SampleNumberCalls.Add(criteria);
        LastCancellationToken = cancellationToken;
        return Failure is null ? Task.FromResult(SampleNumberRows) : Task.FromException<IReadOnlyList<SampleNumberSearchRow>>(Failure);
    }
}

// Keeps the fully formatted message so tests can assert that search text is never logged.
internal sealed class MessageCapturingLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, string Message)> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        Entries.Add((logLevel, formatter(state, exception)));
}
