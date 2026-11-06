using PTL.ExternalWeb.Tests.TestSupport;
using PTL.ExternalWeb.ViewSupport;

namespace PTL.ExternalWeb.Tests.ViewSupport;

public class HelpLinkTests
{
    [Theory]
    [InlineData("Home", "Index", "contextHelpHome")]
    [InlineData("Distribution", "Current", "contextHelpCurrentdistributions")]
    [InlineData("Distribution", "Entry", "contextHelpResultsentry")]
    [InlineData("Distribution", "Previous", "contextHelpPreviousdistributions")]
    [InlineData("Distribution", "Results", "contextHelpViewresults")]
    [InlineData("Comments", "Index", "contextHelpDistributions")]
    [InlineData("Tabulation", "Index", "contextHelpReports")]
    [InlineData("Tabulation", "Report", "contextHelpViewreports")]
    [InlineData("Tabulation", "AssessmentResults", "contextHelpViewreports")]
    [InlineData("distribution", "entry", "contextHelpResultsentry")]
    public void Href_ForAPageWithASection_LinksToThatSection(string controller, string action, string anchor)
    {
        Assert.Equal($"/Help/Index#{anchor}", HelpLink.Href(new FakeUrlHelper(), controller, action));
    }

    [Theory]
    [InlineData("Scheme", "Index")]
    [InlineData("Order", "CurrentYear")]
    [InlineData("Help", "Index")]
    [InlineData("", "")]
    public void Href_ForAPageWithoutASection_LinksToTheTopOfHelp(string controller, string action)
    {
        Assert.Equal("/Help/Index", HelpLink.Href(new FakeUrlHelper(), controller, action));
    }

    [Fact]
    public void Href_WhenTheHelpRouteCannotBeResolved_FallsBackToTheHelpPath()
    {
        Assert.Equal("/Help#contextHelpHome", HelpLink.Href(new FakeUrlHelper(actionReturnsNull: true), "Home", "Index"));
    }
}
