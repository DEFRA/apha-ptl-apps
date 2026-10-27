using Microsoft.AspNetCore.Mvc;
using PTL.ApiClient.Distribution;
using PTL.Contracts.Search;
using PTL.InternalWeb.Features.Distribution;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Features.Distribution;

// Distribution Search - legacy Distributions/Search.aspx. Distribution Number tab is optional and
// unpaged; Sample Number tab is mandatory and paged at DistributionSearchViewModel.SampleNumberPageSize.
public class DistributionControllerSearchTests
{
    private static DistributionController CreateController(IDistributionApiClient apiClient) => new(apiClient, new FakeSearchApiClient(), new FakeLookupApiClient());

    private static DistributionNumberSearchResultResponse DistributionRow(string number) =>
        new(Guid.NewGuid(), Guid.NewGuid(), number, "D1a/S1 BT", "D1a", "2026", "May");

    private static SampleNumberSearchResultResponse SampleRow(string number) =>
        new(Guid.NewGuid(), Guid.NewGuid(), number, "D1a/S1 BT", "D1a", "D1a/00001", "2026", "May");

    private static DistributionSearchViewModel ModelOf(IActionResult result) =>
        Assert.IsType<DistributionSearchViewModel>(Assert.IsType<ViewResult>(result).Model);

    private static Task<IActionResult> SearchAsync(
        DistributionController controller, DistributionSearchTab? tab = null, int page = 1,
        DistributionNumberSearchForm? distributionNumber = null, SampleNumberSearchForm? sampleNumber = null) =>
        controller.Search(tab, page, distributionNumber, sampleNumber, CancellationToken.None);

    [Fact]
    public async Task Get_NoTabSpecified_DefaultsToDistributionNumberTab_WithNoResultsAtAll()
    {
        var apiClient = new FakeDistributionApiClient();
        var controller = CreateController(apiClient);

        var model = ModelOf(await SearchAsync(controller));

        Assert.Equal(DistributionSearchTab.DistributionNumber, model.ActiveTab);
        Assert.Null(model.DistributionNumberResults);
        Assert.Null(model.SampleNumberResults);
        Assert.Empty(apiClient.DistributionNumberSearchCalls);
        Assert.Empty(apiClient.SampleNumberSearchCalls);
    }

    [Fact]
    public async Task Get_DistributionNumberTab_BlankDistributionNumber_StillSearches()
    {
        var apiClient = new FakeDistributionApiClient { DistributionNumberSearchResults = [DistributionRow("D1a/00001")] };
        var controller = CreateController(apiClient);

        var model = ModelOf(await SearchAsync(controller, DistributionSearchTab.DistributionNumber, distributionNumber: new DistributionNumberSearchForm()));

        Assert.Equal(new DistributionNumberSearchCriteriaRequest(null), Assert.Single(apiClient.DistributionNumberSearchCalls));
        Assert.Single(model.DistributionNumberResults!);
        Assert.Null(model.SampleNumberResults);
    }

    [Fact]
    public async Task Get_DistributionNumberTab_WithACriterion_SearchesWithIt()
    {
        var apiClient = new FakeDistributionApiClient { DistributionNumberSearchResults = [DistributionRow("D1a/00001")] };
        var controller = CreateController(apiClient);

        await SearchAsync(controller, DistributionSearchTab.DistributionNumber, distributionNumber: new DistributionNumberSearchForm { DistributionNumber = "D1a" });

        Assert.Equal("D1a", Assert.Single(apiClient.DistributionNumberSearchCalls).DistributionNumber);
    }

    [Fact]
    public async Task Get_DistributionNumberTab_HasNoPaging_ReturnsEveryResult()
    {
        var rows = Enumerable.Range(1, 30).Select(i => DistributionRow($"D1a/{i:00000}")).ToList();
        var apiClient = new FakeDistributionApiClient { DistributionNumberSearchResults = rows };
        var controller = CreateController(apiClient);

        var model = ModelOf(await SearchAsync(controller, DistributionSearchTab.DistributionNumber, distributionNumber: new DistributionNumberSearchForm()));

        Assert.Equal(30, model.DistributionNumberResults!.Count);
    }

    [Fact]
    public async Task Get_DistributionNumberTab_SampleNumberResultsStayNull()
    {
        var apiClient = new FakeDistributionApiClient { DistributionNumberSearchResults = [DistributionRow("D1a/00001")] };
        var controller = CreateController(apiClient);

        var model = ModelOf(await SearchAsync(controller, DistributionSearchTab.DistributionNumber, distributionNumber: new DistributionNumberSearchForm()));

        Assert.Null(model.SampleNumberResults);
        Assert.Empty(apiClient.SampleNumberSearchCalls);
    }

    [Fact]
    public async Task Get_SampleNumberTab_BlankSampleNumber_InvalidModelState_DoesNotCallTheService()
    {
        var apiClient = new FakeDistributionApiClient { SampleNumberSearchResults = [SampleRow("S00001")] };
        var controller = CreateController(apiClient);
        controller.ModelState.AddModelError("SampleNumber.SampleNumber", "A Sample Number must be supplied.");

        var model = ModelOf(await SearchAsync(controller, DistributionSearchTab.SampleNumber, sampleNumber: new SampleNumberSearchForm()));

        Assert.Empty(apiClient.SampleNumberSearchCalls);
        Assert.Null(model.SampleNumberResults);
        Assert.Equal(0, model.SampleNumberTotalCount);
        var error = Assert.Single(controller.ModelState["SampleNumber.SampleNumber"]!.Errors);
        Assert.Equal("A Sample Number must be supplied.", error.ErrorMessage);
    }

    [Fact]
    public async Task Get_SampleNumberTab_BlankSampleNumber_InvalidModelState_DistributionResultsStayNullToo()
    {
        var apiClient = new FakeDistributionApiClient();
        var controller = CreateController(apiClient);
        controller.ModelState.AddModelError("SampleNumber.SampleNumber", "A Sample Number must be supplied.");

        var model = ModelOf(await SearchAsync(controller, DistributionSearchTab.SampleNumber, sampleNumber: new SampleNumberSearchForm()));

        Assert.Null(model.DistributionNumberResults);
        Assert.Empty(apiClient.DistributionNumberSearchCalls);
    }

    [Fact]
    public async Task Get_SampleNumberTab_Valid_SearchesWithTheSubmittedValue()
    {
        var apiClient = new FakeDistributionApiClient { SampleNumberSearchResults = [SampleRow("S00001")] };
        var controller = CreateController(apiClient);

        await SearchAsync(controller, DistributionSearchTab.SampleNumber, sampleNumber: new SampleNumberSearchForm { SampleNumber = "S00001" });

        Assert.Equal("S00001", Assert.Single(apiClient.SampleNumberSearchCalls).SampleNumber);
    }

    [Fact]
    public async Task Get_SampleNumberTab_Valid_DistributionNumberResultsStayNull()
    {
        var apiClient = new FakeDistributionApiClient { SampleNumberSearchResults = [SampleRow("S00001")] };
        var controller = CreateController(apiClient);

        var model = ModelOf(await SearchAsync(controller, DistributionSearchTab.SampleNumber, sampleNumber: new SampleNumberSearchForm { SampleNumber = "S00001" }));

        Assert.Null(model.DistributionNumberResults);
        Assert.Empty(apiClient.DistributionNumberSearchCalls);
    }

    [Fact]
    public async Task Get_SampleNumberTab_FewerThanAPage_ReturnsThemAllOnPageOne()
    {
        var rows = Enumerable.Range(1, 10).Select(i => SampleRow($"S{i:00000}")).ToList();
        var apiClient = new FakeDistributionApiClient { SampleNumberSearchResults = rows };
        var controller = CreateController(apiClient);

        var model = ModelOf(await SearchAsync(controller, DistributionSearchTab.SampleNumber, page: 1, sampleNumber: new SampleNumberSearchForm { SampleNumber = "S" }));

        Assert.Equal(10, model.SampleNumberResults!.Count);
        Assert.Equal(10, model.SampleNumberTotalCount);
        Assert.Equal(1, model.SampleNumberPage);
    }

    [Fact]
    public async Task Get_SampleNumberTab_SecondPage_ReturnsTheNextFifteenAndTheFullTotalCount()
    {
        var rows = Enumerable.Range(1, 20).Select(i => SampleRow($"S{i:00000}")).ToList();
        var apiClient = new FakeDistributionApiClient { SampleNumberSearchResults = rows };
        var controller = CreateController(apiClient);

        var model = ModelOf(await SearchAsync(controller, DistributionSearchTab.SampleNumber, page: 2, sampleNumber: new SampleNumberSearchForm { SampleNumber = "S" }));

        Assert.Equal(5, model.SampleNumberResults!.Count);
        Assert.Equal("S00016", model.SampleNumberResults[0].SampleNumber);
        Assert.Equal(20, model.SampleNumberTotalCount);
        Assert.Equal(2, model.SampleNumberPage);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Get_SampleNumberTab_OutOfRangePage_ClampsToPageOne(int requestedPage)
    {
        var rows = Enumerable.Range(1, 20).Select(i => SampleRow($"S{i:00000}")).ToList();
        var apiClient = new FakeDistributionApiClient { SampleNumberSearchResults = rows };
        var controller = CreateController(apiClient);

        var model = ModelOf(await SearchAsync(controller, DistributionSearchTab.SampleNumber, page: requestedPage, sampleNumber: new SampleNumberSearchForm { SampleNumber = "S" }));

        Assert.Equal(1, model.SampleNumberPage);
        Assert.Equal("S00001", model.SampleNumberResults![0].SampleNumber);
        Assert.Equal(15, model.SampleNumberResults.Count);
    }

    [Fact]
    public async Task Get_SampleNumberTab_NoMatches_ReturnsAnEmptyListNotNull()
    {
        var apiClient = new FakeDistributionApiClient();
        var controller = CreateController(apiClient);

        var model = ModelOf(await SearchAsync(controller, DistributionSearchTab.SampleNumber, sampleNumber: new SampleNumberSearchForm { SampleNumber = "nothing" }));

        Assert.NotNull(model.SampleNumberResults);
        Assert.Empty(model.SampleNumberResults);
        Assert.Equal(0, model.SampleNumberTotalCount);
    }

    [Fact]
    public async Task Get_EchoesBackThePostedFormsOnTheViewModel()
    {
        var apiClient = new FakeDistributionApiClient();
        var controller = CreateController(apiClient);
        var distributionForm = new DistributionNumberSearchForm { DistributionNumber = "D1a" };
        var sampleForm = new SampleNumberSearchForm { SampleNumber = "S1" };

        var model = ModelOf(await SearchAsync(controller, DistributionSearchTab.DistributionNumber, distributionNumber: distributionForm, sampleNumber: sampleForm));

        Assert.Same(distributionForm, model.DistributionNumber);
        Assert.Same(sampleForm, model.SampleNumber);
    }
}
