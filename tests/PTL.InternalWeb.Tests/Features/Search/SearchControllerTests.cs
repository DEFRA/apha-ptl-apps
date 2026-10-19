using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.Lookup;
using PTL.Contracts.Search;
using PTL.InternalWeb.Features.Search;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Features.Search;

public class SearchControllerTests
{
    private static readonly Guid CountryId = Guid.Parse("11111111-0000-0000-0000-0000000000c1");

    private static (SearchController Controller, FakeSearchApiClient Search) Create()
    {
        var search = new FakeSearchApiClient
        {
            CustomerResults = [new CustomerSearchResultResponse(Guid.NewGuid(), "QAL/00001", "ACC1", "Sample Ltd", "Alice", "Sample Org", "France")],
            ParticipantResults = [new ParticipantSearchResultResponse(Guid.NewGuid(), Guid.NewGuid(), "LAB1", "OLD1", "Lab One", "Bob")],
        };
        var lookups = new FakeLookupApiClient { Countries = [new CountryResponse(CountryId, "France")] };
        return (new SearchController(search, lookups), search);
    }

    private static SearchViewModel ModelOf(IActionResult result) =>
        Assert.IsType<SearchViewModel>(Assert.IsType<ViewResult>(result).Model);

    [Fact]
    public async Task Index_FirstVisit_ShowsTheCustomersTabWithoutSearching()
    {
        var (controller, search) = Create();

        var model = ModelOf(await controller.Index(null, null, null, CancellationToken.None));

        Assert.Equal(SearchTab.Customers, model.ActiveTab);
        Assert.Null(model.CustomerResults);
        Assert.Null(model.ParticipantResults);
        Assert.Empty(search.CustomerSearches);
        Assert.Empty(search.ParticipantSearches);
    }

    [Fact]
    public async Task Index_CountryOptions_StartWithNotSpecifiedThenEveryCountry()
    {
        var (controller, _) = Create();

        var model = ModelOf(await controller.Index(null, null, null, CancellationToken.None));

        Assert.Collection(
            model.CountryOptions,
            first =>
            {
                Assert.Equal("- Not Specified -", first.Text);
                Assert.Equal(string.Empty, first.Value);
            },
            second =>
            {
                Assert.Equal("France", second.Text);
                Assert.Equal(CountryId.ToString(), second.Value);
            });
    }

    [Fact]
    public async Task Index_CustomersTab_SearchesCustomersOnlyWithTheSubmittedCriteria()
    {
        var (controller, search) = Create();
        var form = new CustomerSearchForm { QalNumber = "QAL", AccountNumber = "A", Name = "Sam", ContactName = "Ali", Organisation = "Org", CountryId = CountryId };

        var model = ModelOf(await controller.Index(SearchTab.Customers, form, null, CancellationToken.None));

        var criteria = Assert.Single(search.CustomerSearches);
        Assert.Equal(new CustomerSearchCriteriaRequest("QAL", "A", "Sam", "Ali", "Org", CountryId), criteria);
        Assert.Empty(search.ParticipantSearches);
        Assert.Single(model.CustomerResults!);
        Assert.Null(model.ParticipantResults);
        Assert.Equal(SearchTab.Customers, model.ActiveTab);
        Assert.Same(form, model.Customer);
    }

    [Fact]
    public async Task Index_CustomersTab_WithEveryCriterionEmpty_StillSearchesAsLegacyDoes()
    {
        var (controller, search) = Create();

        var model = ModelOf(await controller.Index(SearchTab.Customers, null, null, CancellationToken.None));

        Assert.Equal(new CustomerSearchCriteriaRequest(), Assert.Single(search.CustomerSearches));
        Assert.NotNull(model.CustomerResults);
    }

    [Fact]
    public async Task Index_CustomersTab_NoMatches_ReturnsAnEmptyListNotNull()
    {
        var (controller, search) = Create();
        search.CustomerResults = [];

        var model = ModelOf(await controller.Index(SearchTab.Customers, new CustomerSearchForm(), null, CancellationToken.None));

        Assert.NotNull(model.CustomerResults);
        Assert.Empty(model.CustomerResults);
    }

    [Fact]
    public async Task Index_ParticipantsTab_SearchesParticipantsOnlyWithTheSubmittedCriteria()
    {
        var (controller, search) = Create();
        var form = new ParticipantSearchForm { LabCode = "L1", LabName = "Lab", ContactName = "Bob" };

        var model = ModelOf(await controller.Index(SearchTab.Participants, null, form, CancellationToken.None));

        Assert.Equal(new ParticipantSearchCriteriaRequest("L1", "Lab", "Bob"), Assert.Single(search.ParticipantSearches));
        Assert.Empty(search.CustomerSearches);
        Assert.Single(model.ParticipantResults!);
        Assert.Null(model.CustomerResults);
        Assert.Equal(SearchTab.Participants, model.ActiveTab);
    }

    [Fact]
    public async Task Index_ParticipantsTab_WithEveryCriterionEmpty_StillSearches()
    {
        var (controller, search) = Create();

        var model = ModelOf(await controller.Index(SearchTab.Participants, null, null, CancellationToken.None));

        Assert.Equal(new ParticipantSearchCriteriaRequest(), Assert.Single(search.ParticipantSearches));
        Assert.NotNull(model.ParticipantResults);
    }

    [Fact]
    public async Task Index_CustomerCriteriaWithValidationErrors_DoesNotSearchAndKeepsTheTab()
    {
        var (controller, search) = Create();
        controller.ModelState.AddModelError("Customer.QalNumber", "QAL Number must be 10 characters or fewer");

        var model = ModelOf(await controller.Index(SearchTab.Customers, new CustomerSearchForm { QalNumber = "12345678901" }, null, CancellationToken.None));

        Assert.Empty(search.CustomerSearches);
        Assert.Null(model.CustomerResults);
        Assert.Equal(SearchTab.Customers, model.ActiveTab);
    }

    [Fact]
    public async Task Index_ParticipantCriteriaWithValidationErrors_DoesNotSearch()
    {
        var (controller, search) = Create();
        controller.ModelState.AddModelError("Participant.LabCode", "Lab Code must be 8 characters or fewer");

        var model = ModelOf(await controller.Index(SearchTab.Participants, null, new ParticipantSearchForm { LabCode = "123456789" }, CancellationToken.None));

        Assert.Empty(search.ParticipantSearches);
        Assert.Null(model.ParticipantResults);
    }

    [Fact]
    public async Task Index_ErrorsOnTheOtherTab_DoNotBlockTheSubmittedTabsSearch()
    {
        var (controller, search) = Create();
        controller.ModelState.AddModelError("Participant.LabCode", "Lab Code must be 8 characters or fewer");

        var model = ModelOf(await controller.Index(SearchTab.Customers, new CustomerSearchForm(), null, CancellationToken.None));

        Assert.Single(search.CustomerSearches);
        Assert.NotNull(model.CustomerResults);
    }

    [Fact]
    public async Task Index_AnErrorKeyWithNoMessage_DoesNotBlockTheSearch()
    {
        var (controller, search) = Create();
        controller.ModelState.SetModelValue("Customer.QalNumber", "x", "x");

        await controller.Index(SearchTab.Customers, new CustomerSearchForm(), null, CancellationToken.None);

        Assert.Single(search.CustomerSearches);
    }

    [Theory]
    [InlineData("QalNumber", 10)]
    [InlineData("AccountNumber", 20)]
    [InlineData("Name", 50)]
    [InlineData("ContactName", 50)]
    [InlineData("Organisation", 50)]
    public void CustomerSearchForm_EnforcesTheLegacyMaxLengths(string property, int max)
    {
        var form = new CustomerSearchForm();
        typeof(CustomerSearchForm).GetProperty(property)!.SetValue(form, new string('x', max + 1));

        var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        var valid = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(form, new(form), results, validateAllProperties: true);

        Assert.False(valid);
        Assert.Contains(results, r => r.MemberNames.Contains(property));
    }

    [Theory]
    [InlineData("LabCode", 8)]
    [InlineData("LabName", 50)]
    [InlineData("ContactName", 50)]
    public void ParticipantSearchForm_EnforcesTheLegacyMaxLengths(string property, int max)
    {
        var form = new ParticipantSearchForm();
        typeof(ParticipantSearchForm).GetProperty(property)!.SetValue(form, new string('x', max + 1));

        var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        var valid = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(form, new(form), results, validateAllProperties: true);

        Assert.False(valid);
        Assert.Contains(results, r => r.MemberNames.Contains(property));
    }

    [Fact]
    public void SearchForms_AtTheMaxLength_AreValid()
    {
        var customer = new CustomerSearchForm { QalNumber = new string('x', 10), AccountNumber = new string('x', 20), Name = new string('x', 50) };
        var participant = new ParticipantSearchForm { LabCode = new string('x', 8) };

        Assert.True(System.ComponentModel.DataAnnotations.Validator.TryValidateObject(customer, new(customer), null, true));
        Assert.True(System.ComponentModel.DataAnnotations.Validator.TryValidateObject(participant, new(participant), null, true));
    }
}
