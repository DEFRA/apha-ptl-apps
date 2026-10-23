using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.Distribution;
using PTL.InternalWeb.Features.Distribution;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Features.Distribution;

// Monthly Distributions - Tabulations screen - legacy Scheduling/Tabulations.aspx.
public class DistributionControllerTabulationsTests
{
    private static DistributionController CreateController(FakeDistributionApiClient apiClient) => new(apiClient, new FakeSearchApiClient());

    private static TabulationActionResponse Action(string text, bool enabled, string hint, string command) => new(text, enabled, hint, command);

    private static TabulationsSchemeResponse Scheme(string name, bool requiresAssessment = false) =>
        new(
            Guid.NewGuid(), name, "D1a/S1", new DateTime(2026, 5, 20), new DateTime(2026, 6, 30), requiresAssessment,
            ["A", "", ""],
            [new TabulationRowResponse(Guid.NewGuid(), "Summary", true, 2)],
            new TabulationActionsResponse(
                Action("Amend Results", true, "Click to Amend the Results for this Distribution.", "None"),
                Action("Send To Test Consultant", true, string.Empty, "SendToTestConsultant"),
                Action("Send For Assessment", true, string.Empty, "SendForAssessment"),
                Action("Send For Results Sign-Off", true, string.Empty, "SendToResultsSignOff")));

    [Fact]
    public async Task Tabulations_ApiReturnsNull_ReturnsNotFound()
    {
        var apiClient = new FakeDistributionApiClient { Tabulations = null };

        var result = await CreateController(apiClient).Tabulations(2026, 5, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        Assert.Equal([(2026, 5)], apiClient.TabulationRequests);
    }

    [Fact]
    public async Task Tabulations_MapsResponseOntoViewModel()
    {
        var schemes = new[] { Scheme("BT: Brucella"), Scheme("TB: Tuberculosis", requiresAssessment: true) };
        var apiClient = new FakeDistributionApiClient { Tabulations = new TabulationsResponse(2026, 5, schemes) };

        var result = await CreateController(apiClient).Tabulations(2026, 5, CancellationToken.None);

        var model = Assert.IsType<TabulationsViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal((2026, 5, "May 2026"), (model.YearId, model.MonthId, model.MonthDescription));
        Assert.Equal(schemes, model.Schemes);
        Assert.Equal([(2026, 5)], apiClient.TabulationRequests);
    }

    [Fact]
    public async Task Tabulations_NoSchemes_ReturnsViewWithEmptySchemeList()
    {
        var apiClient = new FakeDistributionApiClient { Tabulations = new TabulationsResponse(2026, 12, []) };

        var result = await CreateController(apiClient).Tabulations(2026, 12, CancellationToken.None);

        var model = Assert.IsType<TabulationsViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal("December 2026", model.MonthDescription);
        Assert.Empty(model.Schemes);
    }

    [Fact]
    public void TabulationsViewModel_DefaultsToAnEmptyScheme_List()
    {
        var model = new TabulationsViewModel();

        Assert.Equal(string.Empty, model.MonthDescription);
        Assert.Empty(model.Schemes);
    }

    // These pages are now migrated, so ViewStage no longer recognises them as placeholder stages.
    [Theory]
    [InlineData("Tabulation")]
    [InlineData("Issued Tabulations")]
    [InlineData("Amend Results")]
    [InlineData("View Comments")]
    [InlineData("Send To Test Consultant")]
    [InlineData("Send For Assessment")]
    [InlineData("Send For Results Sign-Off")]
    public void ViewStage_NoLongerTreatsMigratedTabulationsLinksAsPlaceholders(string stage)
    {
        var view = Assert.IsType<ViewResult>(CreateController(new FakeDistributionApiClient()).ViewStage(stage));

        Assert.Equal("FeatureNotAvailable", view.ViewName);
        Assert.Equal("Distribution", view.Model);
    }
}
