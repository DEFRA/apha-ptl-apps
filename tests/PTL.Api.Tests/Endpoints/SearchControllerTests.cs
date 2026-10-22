using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using PTL.Api.Controllers;
using PTL.Api.Tests.Search;
using PTL.Contracts.Search;
using PTL.Core.Search;

namespace PTL.Api.Tests.Endpoints;

public class SearchControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public SearchControllerTests(WebApplicationFactory<Program> factory)
    {
        // Startup checks read these before the host is built (see HealthEndpointsTests).
        Environment.SetEnvironmentVariable("Database__Host", "localhost");
        Environment.SetEnvironmentVariable("Database__Name", "ProficiencyTesting");
        Environment.SetEnvironmentVariable("Database__IntegratedSecurity", "true");
        Environment.SetEnvironmentVariable("Database__TrustServerCertificate", "true");
        Environment.SetEnvironmentVariable("HealthCheck__ReadinessKey", "local-dev-readiness-key");

        // Both flags off: the hosted schedulers return at once, so no test run ever reaches a real database.
        _factory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TabulationEmails:Enabled"] = "false",
                ["PublicationNotifications:Enabled"] = "false",
            })));
    }

    private static (SearchController Controller, FakeSearchRepository Repository) CreateController()
    {
        var repository = new FakeSearchRepository();
        return (new SearchController(new SearchService(repository, NullLogger<SearchService>.Instance)), repository);
    }

    private WebApplicationFactory<Program> WithRepository(FakeSearchRepository repository) =>
        _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<ISearchRepository>();
            services.AddSingleton<ISearchRepository>(repository);
        }));

    [Fact]
    public async Task SearchCustomers_MapsRowsAndPreservesOrder()
    {
        var (controller, repository) = CreateController();
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        repository.CustomerRows =
        [
            new CustomerSearchRow { CustomerId = firstId, QalNumber = "QAL/2", AccountNumber = "ACC2", Name = "Zulu", ContactName = "Zed", Organisation = "Zulu Org", Country = "France" },
            new CustomerSearchRow { CustomerId = secondId, QalNumber = "QAL/1", AccountNumber = "ACC1", Name = "Alpha", ContactName = "Amy", Organisation = "Alpha Org", Country = "Spain" },
        ];

        var result = await controller.SearchCustomers(new CustomerSearchCriteriaRequest("QAL"), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var rows = Assert.IsType<List<CustomerSearchResultResponse>>(ok.Value);
        Assert.Equal(2, rows.Count);
        Assert.Equal(new CustomerSearchResultResponse(firstId, "QAL/2", "ACC2", "Zulu", "Zed", "Zulu Org", "France"), rows[0]);
        Assert.Equal(new CustomerSearchResultResponse(secondId, "QAL/1", "ACC1", "Alpha", "Amy", "Alpha Org", "Spain"), rows[1]);
        Assert.Equal("QAL", Assert.Single(repository.CustomerCalls).QalNumber);
    }

    [Fact]
    public async Task SearchCustomers_NullStringsInRowsBecomeEmpty()
    {
        var (controller, repository) = CreateController();
        repository.CustomerRows = [new CustomerSearchRow { CustomerId = Guid.NewGuid() }];

        var result = await controller.SearchCustomers(new CustomerSearchCriteriaRequest(), CancellationToken.None);

        var row = Assert.Single(Assert.IsType<List<CustomerSearchResultResponse>>(Assert.IsType<OkObjectResult>(result.Result).Value));
        Assert.Equal(string.Empty, row.QalNumber);
        Assert.Equal(string.Empty, row.AccountNumber);
        Assert.Equal(string.Empty, row.Name);
        Assert.Equal(string.Empty, row.ContactName);
        Assert.Equal(string.Empty, row.Organisation);
        Assert.Equal(string.Empty, row.Country);
    }

    [Fact]
    public async Task SearchCustomers_NoMatches_ReturnsOkWithEmptyList()
    {
        var (controller, _) = CreateController();

        var result = await controller.SearchCustomers(new CustomerSearchCriteriaRequest(Name: "nothing"), CancellationToken.None);

        Assert.Empty(Assert.IsType<List<CustomerSearchResultResponse>>(Assert.IsType<OkObjectResult>(result.Result).Value));
    }

    [Fact]
    public async Task SearchCustomers_RepositoryFailure_Propagates()
    {
        var (controller, repository) = CreateController();
        repository.Failure = new InvalidOperationException("database unavailable");

        await Assert.ThrowsAsync<InvalidOperationException>(() => controller.SearchCustomers(new CustomerSearchCriteriaRequest(), CancellationToken.None));
    }

    [Fact]
    public async Task SearchParticipants_MapsRowsAndPreservesOrder()
    {
        var (controller, repository) = CreateController();
        var participantId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        repository.ParticipantRows =
        [
            new ParticipantSearchRow { ParticipantId = participantId, CustomerId = customerId, LabCode = "LAB1", OldLabCode = "OLD2", LabName = "Lab One", ContactName = "Bob" },
            new ParticipantSearchRow { ParticipantId = participantId, CustomerId = customerId, LabCode = "LAB1", OldLabCode = "OLD1", LabName = "Lab One", ContactName = "Bob" },
        ];

        var result = await controller.SearchParticipants(new ParticipantSearchCriteriaRequest(LabCode: "LAB"), CancellationToken.None);

        var rows = Assert.IsType<List<ParticipantSearchResultResponse>>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(new ParticipantSearchResultResponse(participantId, customerId, "LAB1", "OLD2", "Lab One", "Bob"), rows[0]);
        Assert.Equal("OLD1", rows[1].OldLabCode);
        Assert.Equal("LAB", Assert.Single(repository.ParticipantCalls).LabCode);
    }

    [Fact]
    public async Task SearchParticipants_NullStringsInRowsBecomeEmpty()
    {
        var (controller, repository) = CreateController();
        repository.ParticipantRows = [new ParticipantSearchRow { ParticipantId = Guid.NewGuid() }];

        var result = await controller.SearchParticipants(new ParticipantSearchCriteriaRequest(), CancellationToken.None);

        var row = Assert.Single(Assert.IsType<List<ParticipantSearchResultResponse>>(Assert.IsType<OkObjectResult>(result.Result).Value));
        Assert.Equal(string.Empty, row.LabCode);
        Assert.Equal(string.Empty, row.OldLabCode);
        Assert.Equal(string.Empty, row.LabName);
        Assert.Equal(string.Empty, row.ContactName);
    }

    [Fact]
    public async Task SearchParticipants_NoMatches_ReturnsOkWithEmptyList()
    {
        var (controller, _) = CreateController();

        var result = await controller.SearchParticipants(new ParticipantSearchCriteriaRequest(), CancellationToken.None);

        Assert.Empty(Assert.IsType<List<ParticipantSearchResultResponse>>(Assert.IsType<OkObjectResult>(result.Result).Value));
    }

    [Fact]
    public async Task SearchParticipants_RepositoryFailure_Propagates()
    {
        var (controller, repository) = CreateController();
        repository.Failure = new InvalidOperationException("database unavailable");

        await Assert.ThrowsAsync<InvalidOperationException>(() => controller.SearchParticipants(new ParticipantSearchCriteriaRequest(), CancellationToken.None));
    }

    [Fact]
    public void SearchServices_ResolveFromTheRealContainer()
    {
        using var scope = _factory.Services.CreateScope();

        Assert.IsType<SearchService>(scope.ServiceProvider.GetRequiredService<ISearchService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ISearchRepository>());
    }

    [Fact]
    public async Task GetCustomers_BindsEveryQueryValueIncludingEmptyCountry()
    {
        var repository = new FakeSearchRepository
        {
            CustomerRows = [new CustomerSearchRow { CustomerId = Guid.NewGuid(), Name = "Alpha" }],
        };
        var client = WithRepository(repository).CreateClient();
        var countryId = Guid.NewGuid();

        var response = await client.GetAsync($"/api/search/customers?qalNumber=QAL%2F1&accountNumber=ACC&name=Alpha%20%26%20Co&contactName=Amy&organisation=Org&countryId={countryId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rows = await response.Content.ReadFromJsonAsync<List<CustomerSearchResultResponse>>();
        Assert.Equal("Alpha", Assert.Single(rows!).Name);
        var call = Assert.Single(repository.CustomerCalls);
        Assert.Equal(new CustomerSearchCriteria("QAL/1", "ACC", "Alpha & Co", "Amy", "Org", countryId), call);
    }

    [Fact]
    public async Task GetCustomers_NoQueryString_SearchesWithEmptyCriteria()
    {
        var repository = new FakeSearchRepository();
        var client = WithRepository(repository).CreateClient();

        var response = await client.GetAsync("/api/search/customers?countryId=");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new CustomerSearchCriteria(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, Guid.Empty), Assert.Single(repository.CustomerCalls));
        Assert.Empty((await response.Content.ReadFromJsonAsync<List<CustomerSearchResultResponse>>())!);
    }

    [Fact]
    public async Task GetCustomers_MalformedCountryId_ReturnsBadRequest()
    {
        var repository = new FakeSearchRepository();
        var client = WithRepository(repository).CreateClient();

        var response = await client.GetAsync("/api/search/customers?countryId=not-a-guid");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(repository.CustomerCalls);
    }

    [Fact]
    public async Task GetParticipants_BindsEveryQueryValue()
    {
        var repository = new FakeSearchRepository
        {
            ParticipantRows = [new ParticipantSearchRow { ParticipantId = Guid.NewGuid(), LabCode = "LAB1" }],
        };
        var client = WithRepository(repository).CreateClient();

        var response = await client.GetAsync("/api/search/participants?labCode=LAB1&labName=Lab%20One&contactName=Bob");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rows = await response.Content.ReadFromJsonAsync<List<ParticipantSearchResultResponse>>();
        Assert.Equal("LAB1", Assert.Single(rows!).LabCode);
        Assert.Equal(new ParticipantSearchCriteria("LAB1", "Lab One", "Bob"), Assert.Single(repository.ParticipantCalls));
    }

    [Fact]
    public async Task GetParticipants_NoQueryString_SearchesWithEmptyCriteria()
    {
        var repository = new FakeSearchRepository();
        var client = WithRepository(repository).CreateClient();

        var response = await client.GetAsync("/api/search/participants");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new ParticipantSearchCriteria(string.Empty, string.Empty, string.Empty), Assert.Single(repository.ParticipantCalls));
    }

    [Fact]
    public async Task SearchDistributions_MapsRowsAndPreservesOrder()
    {
        var (controller, repository) = CreateController();
        var firstMonthlyId = Guid.NewGuid();
        var secondMonthlyId = Guid.NewGuid();
        var firstSchemeId = Guid.NewGuid();
        var secondSchemeId = Guid.NewGuid();
        repository.DistributionNumberRows =
        [
            new DistributionNumberSearchRow { MonthlyDistributionSchemeId = firstMonthlyId, SchemeId = firstSchemeId, DistributionNumber = "D1a/00002", SchemeName = "D1a/S2 BT", SchemeIdentifier = "D1a", MonthId = 6, Year = "2026" },
            new DistributionNumberSearchRow { MonthlyDistributionSchemeId = secondMonthlyId, SchemeId = secondSchemeId, DistributionNumber = "D1a/00001", SchemeName = "D1a/S1 BT", SchemeIdentifier = "D1a", MonthId = 5, Year = "2026" },
        ];

        var result = await controller.SearchDistributions(new DistributionNumberSearchCriteriaRequest("D1a"), CancellationToken.None);

        var rows = Assert.IsType<List<DistributionNumberSearchResultResponse>>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(new DistributionNumberSearchResultResponse(firstMonthlyId, firstSchemeId, "D1a/00002", "D1a/S2 BT", "D1a", "2026", "June"), rows[0]);
        Assert.Equal(new DistributionNumberSearchResultResponse(secondMonthlyId, secondSchemeId, "D1a/00001", "D1a/S1 BT", "D1a", "2026", "May"), rows[1]);
        Assert.Equal("D1a", Assert.Single(repository.DistributionNumberCalls).DistributionNumber);
    }

    [Fact]
    public async Task SearchDistributions_NullStringsInRowsBecomeEmpty()
    {
        var (controller, repository) = CreateController();
        repository.DistributionNumberRows = [new DistributionNumberSearchRow { MonthlyDistributionSchemeId = Guid.NewGuid(), SchemeId = Guid.NewGuid() }];

        var result = await controller.SearchDistributions(new DistributionNumberSearchCriteriaRequest(), CancellationToken.None);

        var row = Assert.Single(Assert.IsType<List<DistributionNumberSearchResultResponse>>(Assert.IsType<OkObjectResult>(result.Result).Value));
        Assert.Equal(string.Empty, row.DistributionNumber);
        Assert.Equal(string.Empty, row.SchemeName);
        Assert.Equal(string.Empty, row.SchemeIdentifier);
        Assert.Equal(string.Empty, row.Year);
        Assert.Equal(string.Empty, row.Month);
    }

    [Fact]
    public async Task SearchDistributions_BlankDistributionNumber_StillSearchesAsLegacyDoes()
    {
        var (controller, repository) = CreateController();
        repository.DistributionNumberRows = [new DistributionNumberSearchRow { MonthlyDistributionSchemeId = Guid.NewGuid(), SchemeId = Guid.NewGuid() }];

        var result = await controller.SearchDistributions(new DistributionNumberSearchCriteriaRequest(), CancellationToken.None);

        Assert.Equal(new DistributionNumberSearchCriteria(string.Empty), Assert.Single(repository.DistributionNumberCalls));
        Assert.Single(Assert.IsType<List<DistributionNumberSearchResultResponse>>(Assert.IsType<OkObjectResult>(result.Result).Value));
    }

    [Fact]
    public async Task SearchDistributions_NoMatches_ReturnsOkWithEmptyList()
    {
        var (controller, _) = CreateController();

        var result = await controller.SearchDistributions(new DistributionNumberSearchCriteriaRequest("nothing"), CancellationToken.None);

        Assert.Empty(Assert.IsType<List<DistributionNumberSearchResultResponse>>(Assert.IsType<OkObjectResult>(result.Result).Value));
    }

    [Fact]
    public async Task SearchDistributions_RepositoryFailure_Propagates()
    {
        var (controller, repository) = CreateController();
        repository.Failure = new InvalidOperationException("database unavailable");

        await Assert.ThrowsAsync<InvalidOperationException>(() => controller.SearchDistributions(new DistributionNumberSearchCriteriaRequest(), CancellationToken.None));
    }

    [Theory]
    [InlineData(1, "January")]
    [InlineData(2, "February")]
    [InlineData(3, "March")]
    [InlineData(4, "April")]
    [InlineData(5, "May")]
    [InlineData(6, "June")]
    [InlineData(7, "July")]
    [InlineData(8, "August")]
    [InlineData(9, "September")]
    [InlineData(10, "October")]
    [InlineData(11, "November")]
    [InlineData(12, "December")]
    [InlineData(0, "")]
    [InlineData(13, "")]
    [InlineData(-1, "")]
    public async Task SearchDistributions_MonthIdConvertsToTheEnGbMonthName(int monthId, string expectedMonth)
    {
        var (controller, repository) = CreateController();
        repository.DistributionNumberRows = [new DistributionNumberSearchRow { MonthlyDistributionSchemeId = Guid.NewGuid(), SchemeId = Guid.NewGuid(), MonthId = monthId }];

        var result = await controller.SearchDistributions(new DistributionNumberSearchCriteriaRequest(), CancellationToken.None);

        var row = Assert.Single(Assert.IsType<List<DistributionNumberSearchResultResponse>>(Assert.IsType<OkObjectResult>(result.Result).Value));
        Assert.Equal(expectedMonth, row.Month);
    }

    [Fact]
    public async Task SearchSamples_MapsRowsAndPreservesOrder()
    {
        var (controller, repository) = CreateController();
        var firstSampleId = Guid.NewGuid();
        var secondSampleId = Guid.NewGuid();
        var schemeId = Guid.NewGuid();
        repository.SampleNumberRows =
        [
            new SampleNumberSearchRow { SampleId = firstSampleId, SchemeId = schemeId, SampleNumber = "S00002", SchemeName = "D1a/S1 BT", SchemeIdentifier = "D1a", DistributionNumber = "D1a/00001", MonthId = 5, Year = "2026" },
            new SampleNumberSearchRow { SampleId = secondSampleId, SchemeId = schemeId, SampleNumber = "S00001", SchemeName = "D1a/S1 BT", SchemeIdentifier = "D1a", DistributionNumber = "D1a/00001", MonthId = 5, Year = "2026" },
        ];

        var result = await controller.SearchSamples(new SampleNumberSearchCriteriaRequest("S0"), CancellationToken.None);

        var rows = Assert.IsType<List<SampleNumberSearchResultResponse>>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(new SampleNumberSearchResultResponse(firstSampleId, schemeId, "S00002", "D1a/S1 BT", "D1a", "D1a/00001", "2026", "May"), rows[0]);
        Assert.Equal("S00001", rows[1].SampleNumber);
        Assert.Equal("S0", Assert.Single(repository.SampleNumberCalls).SampleNumber);
    }

    [Fact]
    public async Task SearchSamples_NullStringsInRowsBecomeEmpty()
    {
        var (controller, repository) = CreateController();
        repository.SampleNumberRows = [new SampleNumberSearchRow { SampleId = Guid.NewGuid(), SchemeId = Guid.NewGuid() }];

        var result = await controller.SearchSamples(new SampleNumberSearchCriteriaRequest("S1"), CancellationToken.None);

        var row = Assert.Single(Assert.IsType<List<SampleNumberSearchResultResponse>>(Assert.IsType<OkObjectResult>(result.Result).Value));
        Assert.Equal(string.Empty, row.SampleNumber);
        Assert.Equal(string.Empty, row.SchemeName);
        Assert.Equal(string.Empty, row.SchemeIdentifier);
        Assert.Equal(string.Empty, row.DistributionNumber);
        Assert.Equal(string.Empty, row.Year);
        Assert.Equal(string.Empty, row.Month);
    }

    [Fact]
    public async Task SearchSamples_BlankSampleNumber_StillSearches()
    {
        var (controller, repository) = CreateController();

        var result = await controller.SearchSamples(new SampleNumberSearchCriteriaRequest(), CancellationToken.None);

        Assert.Equal(new SampleNumberSearchCriteria(string.Empty), Assert.Single(repository.SampleNumberCalls));
        Assert.Empty(Assert.IsType<List<SampleNumberSearchResultResponse>>(Assert.IsType<OkObjectResult>(result.Result).Value));
    }

    [Fact]
    public async Task SearchSamples_NoMatches_ReturnsOkWithEmptyList()
    {
        var (controller, _) = CreateController();

        var result = await controller.SearchSamples(new SampleNumberSearchCriteriaRequest("nothing"), CancellationToken.None);

        Assert.Empty(Assert.IsType<List<SampleNumberSearchResultResponse>>(Assert.IsType<OkObjectResult>(result.Result).Value));
    }

    [Fact]
    public async Task SearchSamples_RepositoryFailure_Propagates()
    {
        var (controller, repository) = CreateController();
        repository.Failure = new InvalidOperationException("database unavailable");

        await Assert.ThrowsAsync<InvalidOperationException>(() => controller.SearchSamples(new SampleNumberSearchCriteriaRequest(), CancellationToken.None));
    }

    [Fact]
    public async Task GetDistributions_BindsTheQueryValue()
    {
        var repository = new FakeSearchRepository
        {
            DistributionNumberRows = [new DistributionNumberSearchRow { MonthlyDistributionSchemeId = Guid.NewGuid(), SchemeId = Guid.NewGuid(), DistributionNumber = "D1a/00001" }],
        };
        var client = WithRepository(repository).CreateClient();

        var response = await client.GetAsync("/api/search/distributions?distributionNumber=D1a");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rows = await response.Content.ReadFromJsonAsync<List<DistributionNumberSearchResultResponse>>();
        Assert.Equal("D1a/00001", Assert.Single(rows!).DistributionNumber);
        Assert.Equal(new DistributionNumberSearchCriteria("D1a"), Assert.Single(repository.DistributionNumberCalls));
    }

    [Fact]
    public async Task GetDistributions_NoQueryString_SearchesWithEmptyCriteria()
    {
        var repository = new FakeSearchRepository();
        var client = WithRepository(repository).CreateClient();

        var response = await client.GetAsync("/api/search/distributions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new DistributionNumberSearchCriteria(string.Empty), Assert.Single(repository.DistributionNumberCalls));
    }

    [Fact]
    public async Task GetSamples_BindsTheQueryValue()
    {
        var repository = new FakeSearchRepository
        {
            SampleNumberRows = [new SampleNumberSearchRow { SampleId = Guid.NewGuid(), SchemeId = Guid.NewGuid(), SampleNumber = "S00001" }],
        };
        var client = WithRepository(repository).CreateClient();

        var response = await client.GetAsync("/api/search/samples?sampleNumber=S00001");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rows = await response.Content.ReadFromJsonAsync<List<SampleNumberSearchResultResponse>>();
        Assert.Equal("S00001", Assert.Single(rows!).SampleNumber);
        Assert.Equal(new SampleNumberSearchCriteria("S00001"), Assert.Single(repository.SampleNumberCalls));
    }

    [Fact]
    public async Task GetSamples_NoQueryString_SearchesWithEmptyCriteria()
    {
        var repository = new FakeSearchRepository();
        var client = WithRepository(repository).CreateClient();

        var response = await client.GetAsync("/api/search/samples");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new SampleNumberSearchCriteria(string.Empty), Assert.Single(repository.SampleNumberCalls));
    }
}
