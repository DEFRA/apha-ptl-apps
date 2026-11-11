using PTL.Contracts.ExternalUser;
using PTL.ExternalWeb.Navigation;

namespace PTL.ExternalWeb.Tests.Navigation;

public class LabUserNavigationTests
{
    private static NavigationItem Section(string text) =>
        NavigationProvider.Build().Single(item => item.Text == text);

    [Fact]
    public void LabUsersSection_HasViewAndAddForThePrimaryContactOnly()
    {
        var section = Section("Lab users");

        Assert.Equal(["View lab users", "Add lab user"], section.Children.Select(child => child.Text));
        Assert.All(section.Children, child => Assert.Equal("LabUser", child.ControllerName));
        Assert.True(section.ExcludeLabUsers);
    }

    [Theory]
    [InlineData("Lab users")]
    [InlineData("My account")]
    public void SectionsForThePrimaryContactOnly_AreHiddenFromLabUsers(string text)
    {
        var section = Section(text);
        string[] roles = [ExternalRoleNames.Participant];

        Assert.True(section.IsVisibleTo(roles, isLabUser: false, canOrderOnline: false));
        Assert.False(section.IsVisibleTo(roles, isLabUser: true, canOrderOnline: false));
        Assert.False(section.IsVisibleTo([ExternalRoleNames.Viewer], isLabUser: false, canOrderOnline: false));
    }

    [Fact]
    public void SectionsWithoutTheExclusion_AreStillVisibleToLabUsers()
    {
        var schemes = Section("Schemes");

        Assert.False(schemes.ExcludeLabUsers);
        Assert.True(schemes.IsVisibleTo([ExternalRoleNames.Participant, ExternalRoleNames.Viewer], isLabUser: true, canOrderOnline: false));
    }

    [Theory]
    [InlineData("Index", "Lab users", "Lab users")]
    [InlineData("Add", "Lab users", "Add lab user")]
    [InlineData("Edit", "Lab users", "Edit lab user")]
    public void BreadcrumbTrails_ExistForTheLabUserPages(string action, string first, string last)
    {
        var trail = NavigationProvider.BreadcrumbTrail("LabUser", action)!;

        Assert.Equal(first, trail[0].Text);
        Assert.Equal(last, trail[^1].Text);
        if (action != "Index")
        {
            Assert.Equal(("LabUser", "Index"), (trail[0].Controller, trail[0].Action));
        }
    }
}
