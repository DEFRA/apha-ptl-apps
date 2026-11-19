using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PTL.ExternalWeb.Tests.Integration;

// The rich text wire encoder lives once in PTL.SharedUI and is served to the editor pages as a static web asset.
public class RichTextWireAssetTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task SharedWireScript_IsServedWithoutSigningIn()
    {
        var response = await factory.CreateClient().GetAsync("/_content/PTL.SharedUI/js/ptl-rich-text-wire.js");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var script = await response.Content.ReadAsStringAsync();
        Assert.Contains("ptl-b64:", script, StringComparison.Ordinal);
        Assert.Contains("formdata", script, StringComparison.Ordinal);
    }
}
