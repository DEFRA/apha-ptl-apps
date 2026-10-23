using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.Lookup;
using PTL.InternalWeb.Features.Distribution;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Features.Distribution;

// Group Address Labels - legacy Distributions/GroupAddress.aspx. No search/filter - always
// fetches every configured Group Address and renders the grid (or the empty-state message).
public class DistributionControllerGroupAddressLabelsTests
{
    private static DistributionController CreateController(FakeLookupApiClient lookupApiClient) =>
        new(new FakeDistributionApiClient(), new FakeSearchApiClient(), lookupApiClient);

    private static GroupAddressLabelsViewModel ModelOf(IActionResult result) =>
        Assert.IsType<GroupAddressLabelsViewModel>(Assert.IsType<ViewResult>(result).Model);

    [Fact]
    public async Task Get_WithGroupAddresses_ReturnsAViewModelWhoseGroupAddressesMatchExactlyWhatTheServiceReturned()
    {
        var countryId = Guid.NewGuid();
        var groupAddresses = new List<GroupAddressResponse>
        {
            new(Guid.NewGuid(), "GA1", "1 Group Street", countryId),
            new(Guid.NewGuid(), "GA2", "2 Group Street", countryId),
        };
        var lookupApiClient = new FakeLookupApiClient { GroupAddresses = groupAddresses };
        var controller = CreateController(lookupApiClient);

        var model = ModelOf(await controller.GroupAddressLabels(CancellationToken.None));

        Assert.Equal(groupAddresses, model.GroupAddresses);
    }

    [Fact]
    public async Task Get_NoGroupAddressesConfigured_GroupAddressesIsAnEmptyListNotNull()
    {
        var lookupApiClient = new FakeLookupApiClient();
        var controller = CreateController(lookupApiClient);

        var model = ModelOf(await controller.GroupAddressLabels(CancellationToken.None));

        Assert.NotNull(model.GroupAddresses);
        Assert.Empty(model.GroupAddresses);
    }

    [Fact]
    public void PrintGroupAddressLabel_ReturnsTheFeatureNotAvailableStub()
    {
        var controller = CreateController(new FakeLookupApiClient());

        var result = controller.PrintGroupAddressLabel(Guid.NewGuid());

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("FeatureNotAvailable", view.ViewName);
        Assert.Equal("Print Group Address Label", view.Model);
    }
}
