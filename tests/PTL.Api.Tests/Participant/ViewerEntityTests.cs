using PTL.Core.Viewer;

namespace PTL.Api.Tests.Participant;

// SsoId isn't surfaced by ViewerResponse/the viewer-assignment flow, so no other test round-trips
// it; covered directly here (PTL.Core.Viewer lives in the scope PTL.Api.Tests measures).
public class ViewerEntityTests
{
    [Fact]
    public void SsoId_RoundTrips()
    {
        var ssoId = Guid.NewGuid();
        var viewer = new ViewerEntity { SsoId = ssoId };

        Assert.Equal(ssoId, viewer.SsoId);
    }
}
