using PTL.InternalWeb.Authorization;
using PTL.InternalWeb.Navigation;

namespace PTL.InternalWeb.Tests.Navigation;

public class ResultsSignOffNavigationTests
{
    [Fact]
    public void ResultsSignOff_IsAnEnabledNodeNamedAsInTheLegacySiteMap()
    {
        var node = SideNavigationProvider.Build().Single(item => item.Text == "Results Sign-off");

        Assert.True(node.IsEnabled);
        Assert.Equal(("ResultsSignOff", "Index"), (node.ControllerName, node.ActionName));
        Assert.Equal(PtlPolicies.ResultsSignOff, node.RequiredPolicy);
    }

    // Legacy sitemap: SignOff > Tabulation (hidden) > AssessmentResults (hidden).
    [Fact]
    public void ResultsSignOff_NestsTheHiddenTabulationAndAssessmentResultsPages()
    {
        var node = SideNavigationProvider.Build().Single(item => item.Text == "Results Sign-off");

        var tabulation = Assert.Single(node.Children);
        Assert.Equal(("Results Sign-Off Tabulation", "ResultsSignOff", "Tabulation", true), (tabulation.Text, tabulation.ControllerName, tabulation.ActionName, tabulation.IsHidden));
        Assert.Equal(["tabulationId"], tabulation.LinkQueryKeys);
        var assessment = Assert.Single(tabulation.Children);
        Assert.Equal(("Assessment Results", "ResultsSignOff", "AssessmentResults", true), (assessment.Text, assessment.ControllerName, assessment.ActionName, assessment.IsHidden));
    }

    [Fact]
    public void FindActivePath_ForTheAssessmentResultsPage_RunsThroughTheTabulation()
    {
        var path = SideNavigationProvider.FindActivePath(SideNavigationProvider.Build(), "ResultsSignOff", "AssessmentResults");

        Assert.Equal(["Results Sign-off", "Results Sign-Off Tabulation", "Assessment Results"], path!.Select(n => n.Text));
    }
}
