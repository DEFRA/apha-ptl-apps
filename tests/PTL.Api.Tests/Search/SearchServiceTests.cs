using Microsoft.Extensions.Logging;
using PTL.Contracts.Search;
using PTL.Core.Search;

namespace PTL.Api.Tests.Search;

public class SearchServiceTests
{
    private static (SearchService Service, FakeSearchRepository Repository, MessageCapturingLogger<SearchService> Logger) CreateService()
    {
        var repository = new FakeSearchRepository();
        var logger = new MessageCapturingLogger<SearchService>();
        return (new SearchService(repository, logger), repository, logger);
    }

    [Fact]
    public async Task SearchCustomersAsync_NullCriteria_AreNormalisedToEmptyStringsAndEmptyGuid()
    {
        var (service, repository, _) = CreateService();

        await service.SearchCustomersAsync(new CustomerSearchCriteriaRequest());

        var call = Assert.Single(repository.CustomerCalls);
        Assert.Equal(string.Empty, call.QalNumber);
        Assert.Equal(string.Empty, call.AccountNumber);
        Assert.Equal(string.Empty, call.Name);
        Assert.Equal(string.Empty, call.ContactName);
        Assert.Equal(string.Empty, call.Organisation);
        Assert.Equal(Guid.Empty, call.CountryId);
    }

    [Fact]
    public async Task SearchCustomersAsync_EmptyCriteria_StillSearchesAndReturnsEverything()
    {
        var (service, repository, _) = CreateService();
        repository.CustomerRows = [new CustomerSearchRow { Name = "Alpha" }, new CustomerSearchRow { Name = "Beta" }];

        var results = await service.SearchCustomersAsync(new CustomerSearchCriteriaRequest(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, Guid.Empty));

        Assert.Equal(2, results.Count);
        Assert.Equal(string.Empty, Assert.Single(repository.CustomerCalls).Name);
    }

    [Fact]
    public async Task SearchCustomersAsync_ValuesArePassedThroughUntrimmedAndUnchanged()
    {
        var (service, repository, _) = CreateService();
        var countryId = Guid.NewGuid();

        await service.SearchCustomersAsync(new CustomerSearchCriteriaRequest("QAL/1", "ACC-1", " Alpha ", "Alice", "Org", countryId));

        var call = Assert.Single(repository.CustomerCalls);
        Assert.Equal("QAL/1", call.QalNumber);
        Assert.Equal("ACC-1", call.AccountNumber);
        Assert.Equal(" Alpha ", call.Name);
        Assert.Equal("Alice", call.ContactName);
        Assert.Equal("Org", call.Organisation);
        Assert.Equal(countryId, call.CountryId);
    }

    [Fact]
    public async Task SearchCustomersAsync_ReturnsRepositoryRowsInRepositoryOrder()
    {
        var (service, repository, _) = CreateService();
        repository.CustomerRows =
        [
            new CustomerSearchRow { Name = "Zulu" },
            new CustomerSearchRow { Name = "Alpha" },
            new CustomerSearchRow { Name = "Mike" },
        ];

        var results = await service.SearchCustomersAsync(new CustomerSearchCriteriaRequest());

        Assert.Same(repository.CustomerRows, results);
        Assert.Equal("Zulu,Alpha,Mike", string.Join(',', results.Select(r => r.Name)));
    }

    [Fact]
    public async Task SearchCustomersAsync_NoMatches_ReturnsEmptyList()
    {
        var (service, _, _) = CreateService();

        var results = await service.SearchCustomersAsync(new CustomerSearchCriteriaRequest(Name: "nothing"));

        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchCustomersAsync_ForwardsTheCancellationToken()
    {
        var (service, repository, _) = CreateService();
        using var source = new CancellationTokenSource();

        await service.SearchCustomersAsync(new CustomerSearchCriteriaRequest(), source.Token);

        Assert.Equal(source.Token, repository.LastCancellationToken);
    }

    [Fact]
    public async Task SearchCustomersAsync_RepositoryFailure_Propagates()
    {
        var (service, repository, logger) = CreateService();
        repository.Failure = new InvalidOperationException("database unavailable");

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SearchCustomersAsync(new CustomerSearchCriteriaRequest(Name: "Alpha")));

        Assert.Equal("database unavailable", failure.Message);
        Assert.Empty(logger.Entries);
    }

    [Fact]
    public async Task SearchCustomersAsync_LogsOnlyTheResultCount_NeverTheSearchText()
    {
        var (service, repository, logger) = CreateService();
        repository.CustomerRows = [new CustomerSearchRow { Name = "Sensitive Customer Ltd" }, new CustomerSearchRow()];

        await service.SearchCustomersAsync(new CustomerSearchCriteriaRequest("QAL-SECRET", "ACC-SECRET", "NAME-SECRET", "CONTACT-SECRET", "ORG-SECRET", Guid.NewGuid()));

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Contains("2", entry.Message, StringComparison.Ordinal);
        foreach (var secret in new[] { "QAL-SECRET", "ACC-SECRET", "NAME-SECRET", "CONTACT-SECRET", "ORG-SECRET", "Sensitive Customer Ltd" })
        {
            Assert.DoesNotContain(secret, entry.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task SearchParticipantsAsync_NullCriteria_AreNormalisedToEmptyStrings()
    {
        var (service, repository, _) = CreateService();

        await service.SearchParticipantsAsync(new ParticipantSearchCriteriaRequest());

        var call = Assert.Single(repository.ParticipantCalls);
        Assert.Equal(string.Empty, call.LabCode);
        Assert.Equal(string.Empty, call.LabName);
        Assert.Equal(string.Empty, call.ContactName);
    }

    [Fact]
    public async Task SearchParticipantsAsync_ValuesArePassedThroughUnchanged()
    {
        var (service, repository, _) = CreateService();

        await service.SearchParticipantsAsync(new ParticipantSearchCriteriaRequest("LAB1", " Lab One ", "Bob"));

        var call = Assert.Single(repository.ParticipantCalls);
        Assert.Equal("LAB1", call.LabCode);
        Assert.Equal(" Lab One ", call.LabName);
        Assert.Equal("Bob", call.ContactName);
    }

    [Fact]
    public async Task SearchParticipantsAsync_ReturnsRepositoryRowsInRepositoryOrder_IncludingOnePerOldLabCode()
    {
        var (service, repository, _) = CreateService();
        var participantId = Guid.NewGuid();
        repository.ParticipantRows =
        [
            new ParticipantSearchRow { ParticipantId = participantId, LabCode = "Z1", OldLabCode = "OLD2" },
            new ParticipantSearchRow { ParticipantId = participantId, LabCode = "Z1", OldLabCode = "OLD1" },
            new ParticipantSearchRow { LabCode = "A1" },
        ];

        var results = await service.SearchParticipantsAsync(new ParticipantSearchCriteriaRequest());

        Assert.Same(repository.ParticipantRows, results);
        Assert.Equal("OLD2,OLD1,", string.Join(',', results.Select(r => r.OldLabCode)));
    }

    [Fact]
    public async Task SearchParticipantsAsync_NoMatches_ReturnsEmptyList()
    {
        var (service, _, _) = CreateService();

        var results = await service.SearchParticipantsAsync(new ParticipantSearchCriteriaRequest(LabCode: "none"));

        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchParticipantsAsync_ForwardsTheCancellationToken()
    {
        var (service, repository, _) = CreateService();
        using var source = new CancellationTokenSource();

        await service.SearchParticipantsAsync(new ParticipantSearchCriteriaRequest(), source.Token);

        Assert.Equal(source.Token, repository.LastCancellationToken);
    }

    [Fact]
    public async Task SearchParticipantsAsync_RepositoryFailure_Propagates()
    {
        var (service, repository, logger) = CreateService();
        repository.Failure = new InvalidOperationException("database unavailable");

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SearchParticipantsAsync(new ParticipantSearchCriteriaRequest(LabCode: "LAB1")));

        Assert.Empty(logger.Entries);
    }

    [Fact]
    public async Task SearchParticipantsAsync_LogsOnlyTheResultCount_NeverTheSearchText()
    {
        var (service, repository, logger) = CreateService();
        repository.ParticipantRows = [new ParticipantSearchRow { LabName = "Sensitive Lab" }];

        await service.SearchParticipantsAsync(new ParticipantSearchCriteriaRequest("CODE-SECRET", "LABNAME-SECRET", "CONTACT-SECRET"));

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Contains("1", entry.Message, StringComparison.Ordinal);
        foreach (var secret in new[] { "CODE-SECRET", "LABNAME-SECRET", "CONTACT-SECRET", "Sensitive Lab" })
        {
            Assert.DoesNotContain(secret, entry.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void SearchRows_DefaultToNullTextAndEmptyIds()
    {
        var customer = new CustomerSearchRow();
        var participant = new ParticipantSearchRow();

        Assert.Equal(Guid.Empty, customer.CustomerId);
        Assert.Null(customer.QalNumber);
        Assert.Null(customer.AccountNumber);
        Assert.Null(customer.Name);
        Assert.Null(customer.ContactName);
        Assert.Null(customer.Organisation);
        Assert.Null(customer.Country);
        Assert.Equal(Guid.Empty, participant.ParticipantId);
        Assert.Equal(Guid.Empty, participant.CustomerId);
        Assert.Null(participant.LabCode);
        Assert.Null(participant.OldLabCode);
        Assert.Null(participant.LabName);
        Assert.Null(participant.ContactName);
    }

    [Fact]
    public async Task SearchByDistributionNumberAsync_NullCriteria_NormalisedToEmptyString()
    {
        var (service, repository, _) = CreateService();

        await service.SearchByDistributionNumberAsync(new DistributionNumberSearchCriteriaRequest());

        Assert.Equal(string.Empty, Assert.Single(repository.DistributionNumberCalls).DistributionNumber);
    }

    [Fact]
    public async Task SearchByDistributionNumberAsync_BlankDistributionNumber_StillSearchesAndReturnsEverything()
    {
        var (service, repository, _) = CreateService();
        repository.DistributionNumberRows = [new DistributionNumberSearchRow { DistributionNumber = "D1" }, new DistributionNumberSearchRow { DistributionNumber = "D2" }];

        var results = await service.SearchByDistributionNumberAsync(new DistributionNumberSearchCriteriaRequest(string.Empty));

        Assert.Equal(2, results.Count);
        Assert.Equal(string.Empty, Assert.Single(repository.DistributionNumberCalls).DistributionNumber);
    }

    [Fact]
    public async Task SearchByDistributionNumberAsync_ValueIsPassedThroughUnchanged()
    {
        var (service, repository, _) = CreateService();

        await service.SearchByDistributionNumberAsync(new DistributionNumberSearchCriteriaRequest(" D1a "));

        Assert.Equal(" D1a ", Assert.Single(repository.DistributionNumberCalls).DistributionNumber);
    }

    [Fact]
    public async Task SearchByDistributionNumberAsync_ReturnsRepositoryRowsInRepositoryOrder()
    {
        var (service, repository, _) = CreateService();
        repository.DistributionNumberRows =
        [
            new DistributionNumberSearchRow { DistributionNumber = "D2" },
            new DistributionNumberSearchRow { DistributionNumber = "D1" },
        ];

        var results = await service.SearchByDistributionNumberAsync(new DistributionNumberSearchCriteriaRequest());

        Assert.Same(repository.DistributionNumberRows, results);
        Assert.Equal("D2,D1", string.Join(',', results.Select(r => r.DistributionNumber)));
    }

    [Fact]
    public async Task SearchByDistributionNumberAsync_NoMatches_ReturnsEmptyList()
    {
        var (service, _, _) = CreateService();

        var results = await service.SearchByDistributionNumberAsync(new DistributionNumberSearchCriteriaRequest("nothing"));

        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchByDistributionNumberAsync_ForwardsTheCancellationToken()
    {
        var (service, repository, _) = CreateService();
        using var source = new CancellationTokenSource();

        await service.SearchByDistributionNumberAsync(new DistributionNumberSearchCriteriaRequest(), source.Token);

        Assert.Equal(source.Token, repository.LastCancellationToken);
    }

    [Fact]
    public async Task SearchByDistributionNumberAsync_RepositoryFailure_Propagates()
    {
        var (service, repository, logger) = CreateService();
        repository.Failure = new InvalidOperationException("database unavailable");

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SearchByDistributionNumberAsync(new DistributionNumberSearchCriteriaRequest("D1")));

        Assert.Equal("database unavailable", failure.Message);
        Assert.Empty(logger.Entries);
    }

    [Fact]
    public async Task SearchByDistributionNumberAsync_LogsOnlyTheResultCount_NeverTheSearchText()
    {
        var (service, repository, logger) = CreateService();
        repository.DistributionNumberRows = [new DistributionNumberSearchRow { DistributionNumber = "D-SECRET" }];

        await service.SearchByDistributionNumberAsync(new DistributionNumberSearchCriteriaRequest("D-SECRET"));

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Contains("1", entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("D-SECRET", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchBySampleNumberAsync_NullCriteria_NormalisedToEmptyString()
    {
        var (service, repository, _) = CreateService();

        await service.SearchBySampleNumberAsync(new SampleNumberSearchCriteriaRequest());

        Assert.Equal(string.Empty, Assert.Single(repository.SampleNumberCalls).SampleNumber);
    }

    [Fact]
    public async Task SearchBySampleNumberAsync_ValueIsPassedThroughUnchanged()
    {
        var (service, repository, _) = CreateService();

        await service.SearchBySampleNumberAsync(new SampleNumberSearchCriteriaRequest("S100"));

        Assert.Equal("S100", Assert.Single(repository.SampleNumberCalls).SampleNumber);
    }

    [Fact]
    public async Task SearchBySampleNumberAsync_ReturnsRepositoryRowsInRepositoryOrder()
    {
        var (service, repository, _) = CreateService();
        repository.SampleNumberRows =
        [
            new SampleNumberSearchRow { SampleNumber = "S2" },
            new SampleNumberSearchRow { SampleNumber = "S1" },
        ];

        var results = await service.SearchBySampleNumberAsync(new SampleNumberSearchCriteriaRequest());

        Assert.Same(repository.SampleNumberRows, results);
        Assert.Equal("S2,S1", string.Join(',', results.Select(r => r.SampleNumber)));
    }

    [Fact]
    public async Task SearchBySampleNumberAsync_NoMatches_ReturnsEmptyList()
    {
        var (service, _, _) = CreateService();

        var results = await service.SearchBySampleNumberAsync(new SampleNumberSearchCriteriaRequest("nothing"));

        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchBySampleNumberAsync_ForwardsTheCancellationToken()
    {
        var (service, repository, _) = CreateService();
        using var source = new CancellationTokenSource();

        await service.SearchBySampleNumberAsync(new SampleNumberSearchCriteriaRequest(), source.Token);

        Assert.Equal(source.Token, repository.LastCancellationToken);
    }

    [Fact]
    public async Task SearchBySampleNumberAsync_RepositoryFailure_Propagates()
    {
        var (service, repository, logger) = CreateService();
        repository.Failure = new InvalidOperationException("database unavailable");

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SearchBySampleNumberAsync(new SampleNumberSearchCriteriaRequest("S1")));

        Assert.Equal("database unavailable", failure.Message);
        Assert.Empty(logger.Entries);
    }

    [Fact]
    public async Task SearchBySampleNumberAsync_LogsOnlyTheResultCount_NeverTheSearchText()
    {
        var (service, repository, logger) = CreateService();
        repository.SampleNumberRows = [new SampleNumberSearchRow { SampleNumber = "S-SECRET" }];

        await service.SearchBySampleNumberAsync(new SampleNumberSearchCriteriaRequest("S-SECRET"));

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Contains("1", entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("S-SECRET", entry.Message, StringComparison.Ordinal);
    }
}
