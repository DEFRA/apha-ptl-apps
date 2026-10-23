using PTL.InternalWeb.Navigation;

namespace PTL.InternalWeb.Tests.Navigation;

public class SideNavigationProviderTests
{
    // Order and titles are those of the legacy ProficiencyTestingWeb/Web.sitemap "System Administration" node.
    [Fact]
    public void Build_SystemAdministration_MatchesTheLegacySiteMapOrderAndTitles()
    {
        var systemAdministration = SideNavigationProvider.Build().Single(i => i.Text == "System Administration");

        Assert.Equal(
            [
                "Manage Roles",
                "Create User",
                "Remove User",
                "Internal Test Consultant Department",
                "Manage External Test Consultants",
                "Manage Viewers",
                "Manage Countries",
                "External Site Management",
                "Administration Charges",
                "Weighted Pricing Plan",
                "Postage Pricing Plan",
            ],
            systemAdministration.Children.Select(c => c.Text));
    }

    [Fact]
    public void Build_ManageContracts_ListsSearchBetweenCustomersAndGroupAddresses()
    {
        var manageContracts = SideNavigationProvider.Build().Single(i => i.Text == "Manage Contracts");

        var visible = manageContracts.Children.Select(c => c.Text).ToList();

        var customers = visible.IndexOf("Customers");
        Assert.True(customers >= 0);
        Assert.Equal("Search", visible[customers + 1]);
        Assert.Equal("Group Addresses", visible[customers + 2]);

        var search = SideNavigationProvider.FindNode(SideNavigationProvider.Build(), "Search", "Index");
        Assert.NotNull(search);
        Assert.True(search!.IsEnabled);
        Assert.False(search.IsHidden);
    }

    [Fact]
    public void Build_ReturnsNonEmptyTree()
    {
        var tree = SideNavigationProvider.Build();

        Assert.NotEmpty(tree);
    }

    [Fact]
    public void Build_Distributions_ListsSearchAsAnEnabledLiveNode()
    {
        var search = SideNavigationProvider.FindNode(SideNavigationProvider.Build(), "Distribution", "Search");

        Assert.NotNull(search);
        Assert.Equal("Search", search!.Text);
        Assert.True(search.IsEnabled);
        Assert.False(search.IsHidden);
    }

    [Fact]
    public void Build_Distributions_ListsParticipantAddressLabelsAsAnEnabledLiveNode()
    {
        var node = SideNavigationProvider.FindNode(SideNavigationProvider.Build(), "Distribution", "ParticipantAddressLabels");

        Assert.NotNull(node);
        Assert.Equal("Participant Address Labels", node!.Text);
        Assert.True(node.IsEnabled);
        Assert.False(node.IsHidden);
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

    // Sample Numbers/Intended Results are hidden children of "Monthly Distributions" so they get
    // a proper breadcrumb trail instead of falling back to a plain "Distribution - X" crumb.
    [Theory]
    [InlineData("SampleNumbers", "Sample Numbers")]
    [InlineData("IntendedResults", "Intended Results")]
    [InlineData("Preparation", "Sample Preparation")]
    [InlineData("PrintableResults", "Printable Results")]
    [InlineData("Packaging", "Sample Packaging")]
    [InlineData("ResultsReceipt", "Results Receipt")]
    [InlineData("ActualResults", "Results")]
    [InlineData("SampleReceipt", "Submission Status")]
    [InlineData("Search", "Search")]
    [InlineData("ParticipantAddressLabels", "Participant Address Labels")]
    public void FindPath_DistributionScreens_ReturnsFullAncestorChain(string actionName, string expectedText)
    {
        var tree = SideNavigationProvider.Build();

        var path = SideNavigationProvider.FindPath(tree, "Distribution", actionName);

        Assert.NotNull(path);
        Assert.True(path!.Count > 1);
        Assert.Equal(expectedText, path[^1].Text);
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
    [InlineData("ViewerManagementAdd", "Manage Viewers")]
    [InlineData("ViewerManagementSave", "Manage Viewers")]
    [InlineData("ViewerManagementRemove", "Manage Viewers")]
    [InlineData("ExternalTestConsultantManagementAdd", "Manage External Test Consultants")]
    [InlineData("ExternalTestConsultantManagementToggleStatus", "Manage External Test Consultants")]
    [InlineData("ExternalTestConsultantManagementRemove", "Manage External Test Consultants")]
    [InlineData("CountryManagementRemove", "Manage Countries")]
    [InlineData("InternalTestConsultantDepartmentToggleStatus", "Internal Test Consultant Department")]
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
        Assert.Equal("Manage Viewers", path![^1].Text);
    }
}
