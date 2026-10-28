using PTL.InternalWeb.Authorization;
using PTL.InternalWeb.Navigation;

namespace PTL.InternalWeb.Tests.Navigation;

public class AssessorNavigationTests
{
    [Fact]
    public void Assessor_IsAnEnabledNodeNamedAsInTheLegacySiteMap()
    {
        var node = SideNavigationProvider.Build().Single(item => item.Text == "Assessor");

        Assert.True(node.IsEnabled);
        Assert.Equal(("Assessor", "Index"), (node.ControllerName, node.ActionName));
        Assert.Equal(PtlPolicies.Assessor, node.RequiredPolicy);
    }

    // Legacy sitemap: Assessor > Assessment Items (hidden) > Assessment Entry (hidden).
    [Fact]
    public void Assessor_NestsTheHiddenAssessmentItemsAndAssessmentEntryPages()
    {
        var node = SideNavigationProvider.Build().Single(item => item.Text == "Assessor");

        var items = Assert.Single(node.Children);
        Assert.Equal(("Assessment Items", "Assessor", "AssessmentItemList", true), (items.Text, items.ControllerName, items.ActionName, items.IsHidden));
        Assert.Equal(["testId", "year"], items.LinkQueryKeys);
        var entry = Assert.Single(items.Children);
        Assert.Equal(("Assessment Entry", "Assessor", "AssessmentEntry", true), (entry.Text, entry.ControllerName, entry.ActionName, entry.IsHidden));
    }

    [Fact]
    public void FindActivePath_ForTheAssessmentItemsPage_RunsThroughTheAssessorMenu()
    {
        var path = SideNavigationProvider.FindActivePath(SideNavigationProvider.Build(), "Assessor", "AssessmentItemList");

        Assert.Equal(["Assessor", "Assessment Items"], path!.Select(n => n.Text));
    }

    [Fact]
    public void FindActivePath_ForTheAssessmentEntryPage_RunsThroughTheAssessmentItems()
    {
        var path = SideNavigationProvider.FindActivePath(SideNavigationProvider.Build(), "Assessor", "AssessmentEntry");

        Assert.Equal(["Assessor", "Assessment Items", "Assessment Entry"], path!.Select(n => n.Text));
    }
}
