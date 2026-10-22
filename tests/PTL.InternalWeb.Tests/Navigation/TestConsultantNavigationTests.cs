using PTL.InternalWeb.Navigation;

namespace PTL.InternalWeb.Tests.Navigation;

public class TestConsultantNavigationTests
{
    [Fact]
    public void TestConsultant_IsAnEnabledNodeWithAHiddenTabulationPage()
    {
        var node = SideNavigationProvider.Build().Single(item => item.Text == "Test Consultant");

        Assert.True(node.IsEnabled);
        Assert.Equal(("TestConsultant", "Index"), (node.ControllerName, node.ActionName));
        var tabulation = Assert.Single(node.Children);
        Assert.Equal(("Test Consultant Tabulation", "TestConsultant", "Tabulation", true), (tabulation.Text, tabulation.ControllerName, tabulation.ActionName, tabulation.IsHidden));
    }

    [Fact]
    public void FindActiveNode_ForTheTabulationPage_ReturnsTheTabulationNode()
    {
        var node = SideNavigationProvider.FindActiveNode(SideNavigationProvider.Build(), "TestConsultant", "Tabulation");

        Assert.Equal("Test Consultant Tabulation", node?.Text);
    }
}
