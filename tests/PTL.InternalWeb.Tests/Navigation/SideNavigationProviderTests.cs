using PTL.InternalWeb.Navigation;

namespace PTL.InternalWeb.Tests.Navigation;

public class SideNavigationProviderTests
{
    [Fact]
    public void Build_ReturnsNonEmptyTree()
    {
        var tree = SideNavigationProvider.Build();

        Assert.NotEmpty(tree);
    }

    [Fact]
    public void FindNode_ExistingNode_ReturnsMatch()
    {
        var tree = SideNavigationProvider.Build();

        var node = SideNavigationProvider.FindNode(tree, "Customer", "Index");

        Assert.NotNull(node);
        Assert.Equal("Customer", node!.ControllerName);
    }

    [Fact]
    public void FindNode_UnknownNode_ReturnsNull()
    {
        var tree = SideNavigationProvider.Build();

        var node = SideNavigationProvider.FindNode(tree, "DoesNotExist", "Index");

        Assert.Null(node);
    }

    [Fact]
    public void FindNode_FindsHiddenNestedNode()
    {
        var tree = SideNavigationProvider.Build();

        // "Renew Contracts" is nested under the hidden "Contracts" node - FindNode must still
        // locate it regardless of IsHidden/depth.
        var node = SideNavigationProvider.FindNode(tree, "Contract", "RenewContracts");

        Assert.NotNull(node);
        Assert.Equal("Renew Contracts", node!.Text);
    }

    [Fact]
    public void FindPath_ExistingNode_ReturnsFullAncestorChain()
    {
        var tree = SideNavigationProvider.Build();

        var path = SideNavigationProvider.FindPath(tree, "Contract", "RenewContracts");

        Assert.NotNull(path);
        Assert.True(path!.Count > 1);
        Assert.Equal("Renew Contracts", path[^1].Text);
    }

    [Fact]
    public void FindPath_UnknownNode_ReturnsNull()
    {
        var tree = SideNavigationProvider.Build();

        var path = SideNavigationProvider.FindPath(tree, "DoesNotExist", "Index");

        Assert.Null(path);
    }

    [Fact]
    public void FindActiveNode_DelegatesToFindNode()
    {
        var tree = SideNavigationProvider.Build();

        var node = SideNavigationProvider.FindActiveNode(tree, "Contract", "Index");

        Assert.NotNull(node);
    }

    [Fact]
    public void FindActivePath_DelegatesToFindPath()
    {
        var tree = SideNavigationProvider.Build();

        var path = SideNavigationProvider.FindActivePath(tree, "Contract", "Index");

        Assert.NotNull(path);
    }

    [Theory]
    [InlineData("CreateUserSearch", "Create User")]
    [InlineData("CreateUserConfirm", "Create User")]
    [InlineData("ViewerManagementAdd", "Viewer Management")]
    [InlineData("ViewerManagementSave", "Viewer Management")]
    [InlineData("ViewerManagementRemove", "Viewer Management")]
    [InlineData("ViewerManagementGenerateLogin", "Viewer Management")]
    [InlineData("ExternalTestConsultantManagementAdd", "External Test Consultant Management")]
    [InlineData("ExternalTestConsultantManagementToggleStatus", "External Test Consultant Management")]
    [InlineData("CountryManagementRemove", "Country Management")]
    [InlineData("InternalTestConsultantDepartmentToggleStatus", "Internal Test Consultant Department Management")]
    [InlineData("PostagePricingPlanRenew", "Postage Pricing Plan")]
    public void FindNode_SubActionWithoutExactMatch_FallsBackToLongestRegisteredPrefix(string actionName, string expectedText)
    {
        var tree = SideNavigationProvider.Build();

        var node = SideNavigationProvider.FindNode(tree, "SystemAdministration", actionName);

        Assert.NotNull(node);
        Assert.Equal(expectedText, node!.Text);
    }

    [Fact]
    public void FindNode_WeightedPricingPlanRenew_MatchesExplicitHiddenNode()
    {
        var tree = SideNavigationProvider.Build();

        // "Renew" has no shared prefix with "WeightedPricingPlan", so it must be an explicit
        // (hidden) node rather than relying on the prefix fallback.
        var node = SideNavigationProvider.FindNode(tree, "SystemAdministration", "Renew");

        Assert.NotNull(node);
        Assert.Equal("Renew Weighted Pricing Plan", node!.Text);
    }

    [Fact]
    public void FindPath_SubActionWithoutExactMatch_ResolvesToLogicalPageAncestorChain()
    {
        var tree = SideNavigationProvider.Build();

        var path = SideNavigationProvider.FindPath(tree, "SystemAdministration", "ViewerManagementRemove");

        Assert.NotNull(path);
        Assert.Equal("Viewer Management", path![^1].Text);
    }
}
