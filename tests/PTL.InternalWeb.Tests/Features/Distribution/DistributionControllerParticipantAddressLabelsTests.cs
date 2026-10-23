using Microsoft.AspNetCore.Mvc;
using PTL.ApiClient.Distribution;
using PTL.Contracts.Search;
using PTL.InternalWeb.Features.Distribution;
using PTL.InternalWeb.Features.Search;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Features.Distribution;

// Participant Address Labels - legacy Distributions/ParticipantAddress.aspx. Reuses the same
// dbo.spgSearchParticipant-backed search as Features/Search's Participants tab.
public class DistributionControllerParticipantAddressLabelsTests
{
    private static DistributionController CreateController(FakeSearchApiClient searchApiClient) =>
        new(new FakeDistributionApiClient(), searchApiClient, new FakeLookupApiClient());

    private static ParticipantAddressLabelsViewModel ModelOf(IActionResult result) =>
        Assert.IsType<ParticipantAddressLabelsViewModel>(Assert.IsType<ViewResult>(result).Model);

    private static ParticipantSearchResultResponse Row(string labCode) =>
        new(Guid.NewGuid(), Guid.NewGuid(), labCode, "OLD1", "Lab One", "Bob Example");

    [Fact]
    public async Task Get_FirstLoad_NotSearchedAndNoFormSupplied_ResultsStayNullAndTheServiceIsNotCalled()
    {
        var search = new FakeSearchApiClient();
        var controller = CreateController(search);

        var model = ModelOf(await controller.ParticipantAddressLabels(searched: false, search: null, CancellationToken.None));

        Assert.Null(model.Results);
        Assert.Empty(search.ParticipantSearches);
        Assert.Null(model.Search.LabCode);
        Assert.Null(model.Search.LabName);
        Assert.Null(model.Search.ContactName);
    }

    [Fact]
    public async Task Get_Searched_BlankFields_StillSearches_TreatingBlankAsMatchEveryParticipant()
    {
        var search = new FakeSearchApiClient { ParticipantResults = [Row("LAB1")] };
        var controller = CreateController(search);

        var model = ModelOf(await controller.ParticipantAddressLabels(searched: true, search: new ParticipantSearchForm(), CancellationToken.None));

        var criteria = Assert.Single(search.ParticipantSearches);
        Assert.Null(criteria.LabCode);
        Assert.Null(criteria.LabName);
        Assert.Null(criteria.ContactName);
        Assert.Single(model.Results!);
        Assert.Equal("LAB1", model.Results![0].LabCode);
    }

    [Fact]
    public async Task Get_Searched_NoMatches_ReturnsAnEmptyListNotNull()
    {
        var search = new FakeSearchApiClient();
        var controller = CreateController(search);

        var model = ModelOf(await controller.ParticipantAddressLabels(searched: true, search: new ParticipantSearchForm(), CancellationToken.None));

        Assert.NotNull(model.Results);
        Assert.Empty(model.Results);
    }

    [Fact]
    public async Task Get_Searched_WithCriteria_ForwardsExactlyTheSubmittedValuesToTheService()
    {
        var search = new FakeSearchApiClient { ParticipantResults = [Row("LAB2")] };
        var controller = CreateController(search);
        var form = new ParticipantSearchForm { LabCode = "LAB2", LabName = "Lab Two", ContactName = "Carol Example" };

        await controller.ParticipantAddressLabels(searched: true, search: form, CancellationToken.None);

        var criteria = Assert.Single(search.ParticipantSearches);
        Assert.Equal(form.ToCriteria(), criteria);
    }

    [Fact]
    public async Task Get_Searched_ButModelStateInvalid_DoesNotCallTheServiceAndResultsStayNull()
    {
        var search = new FakeSearchApiClient { ParticipantResults = [Row("LAB1")] };
        var controller = CreateController(search);
        controller.ModelState.AddModelError("Search.LabCode", "The Lab Code must be at most 8 characters.");

        var model = ModelOf(await controller.ParticipantAddressLabels(searched: true, search: new ParticipantSearchForm { LabCode = "TOO-LONG-CODE" }, CancellationToken.None));

        Assert.Empty(search.ParticipantSearches);
        Assert.Null(model.Results);
    }

    [Fact]
    public void PrintParticipantAddressLabel_ReturnsTheFeatureNotAvailableStub()
    {
        var controller = CreateController(new FakeSearchApiClient());

        var result = controller.PrintParticipantAddressLabel(Guid.NewGuid());

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("FeatureNotAvailable", view.ViewName);
        Assert.Equal("Print Participant Address Label", view.Model);
    }
}
