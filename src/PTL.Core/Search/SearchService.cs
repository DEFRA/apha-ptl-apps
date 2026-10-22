using Microsoft.Extensions.Logging;
using PTL.Contracts.Search;

namespace PTL.Core.Search;

// Legacy Search.aspx passes the raw textbox text to the stored procedure: no trimming, no result
// limit, no paging, no status filter. Matching ('%text%', case-insensitive by database collation)
// and ordering are done by the procedures themselves, so nothing is re-filtered or re-sorted here.
// Search text is never logged - it is customer/participant personal data.
public sealed class SearchService(ISearchRepository searchRepository, ILogger<SearchService> logger) : ISearchService
{
    private static readonly Action<ILogger, int, Exception?> LogCustomerSearchMessage =
        LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(1, nameof(LogCustomerSearchMessage)),
            "Customer search returned {ResultCount} result(s)");

    private static readonly Action<ILogger, int, Exception?> LogParticipantSearchMessage =
        LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(2, nameof(LogParticipantSearchMessage)),
            "Participant search returned {ResultCount} result(s)");

    private static readonly Action<ILogger, int, Exception?> LogDistributionNumberSearchMessage =
        LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(3, nameof(LogDistributionNumberSearchMessage)),
            "Distribution number search returned {ResultCount} result(s)");

    private static readonly Action<ILogger, int, Exception?> LogSampleNumberSearchMessage =
        LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(4, nameof(LogSampleNumberSearchMessage)),
            "Sample number search returned {ResultCount} result(s)");

    public async Task<IReadOnlyList<CustomerSearchRow>> SearchCustomersAsync(CustomerSearchCriteriaRequest criteria, CancellationToken cancellationToken = default)
    {
        var results = await searchRepository.SearchCustomersAsync(
            new CustomerSearchCriteria(
                criteria.QalNumber ?? string.Empty,
                criteria.AccountNumber ?? string.Empty,
                criteria.Name ?? string.Empty,
                criteria.ContactName ?? string.Empty,
                criteria.Organisation ?? string.Empty,
                criteria.CountryId ?? Guid.Empty),
            cancellationToken);

        LogCustomerSearchMessage(logger, results.Count, null);
        return results;
    }

    public async Task<IReadOnlyList<ParticipantSearchRow>> SearchParticipantsAsync(ParticipantSearchCriteriaRequest criteria, CancellationToken cancellationToken = default)
    {
        var results = await searchRepository.SearchParticipantsAsync(
            new ParticipantSearchCriteria(
                criteria.LabCode ?? string.Empty,
                criteria.LabName ?? string.Empty,
                criteria.ContactName ?? string.Empty),
            cancellationToken);

        LogParticipantSearchMessage(logger, results.Count, null);
        return results;
    }

    public async Task<IReadOnlyList<DistributionNumberSearchRow>> SearchByDistributionNumberAsync(DistributionNumberSearchCriteriaRequest criteria, CancellationToken cancellationToken = default)
    {
        var results = await searchRepository.SearchByDistributionNumberAsync(
            new DistributionNumberSearchCriteria(criteria.DistributionNumber ?? string.Empty),
            cancellationToken);

        LogDistributionNumberSearchMessage(logger, results.Count, null);
        return results;
    }

    public async Task<IReadOnlyList<SampleNumberSearchRow>> SearchBySampleNumberAsync(SampleNumberSearchCriteriaRequest criteria, CancellationToken cancellationToken = default)
    {
        var results = await searchRepository.SearchBySampleNumberAsync(
            new SampleNumberSearchCriteria(criteria.SampleNumber ?? string.Empty),
            cancellationToken);

        LogSampleNumberSearchMessage(logger, results.Count, null);
        return results;
    }
}
