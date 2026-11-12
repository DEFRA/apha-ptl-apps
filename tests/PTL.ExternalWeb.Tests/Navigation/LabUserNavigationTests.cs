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

    [Theory]
    [InlineData("Schemes", "View schemes")]
    [InlineData("Results", "Enter results")]
    [InlineData("Results", "View results")]
    public void ParticipantLinks_NeedALabCodeInTheMenuOnly(string section, string link)
    {
        var item = Section(section).Children.Single(child => child.Text == link);
        string[] roles = [ExternalRoleNames.Participant];

        Assert.True(item.RequireLabCode);
        Assert.False(item.IsVisibleTo(roles, isLabUser: false, canOrderOnline: false, hasLabCode: false));
        Assert.True(item.IsVisibleTo(roles, isLabUser: false, canOrderOnline: false, hasLabCode: true));
        // The Home cards call without a lab code and must keep showing the link, as legacy did.
        Assert.True(item.IsVisibleTo(roles, isLabUser: false, canOrderOnline: false));
    }

    [Theory]
    [InlineData("CurrentYear", false, true, false)]
    [InlineData("CurrentYear", true, false, true)]
    [InlineData("NextYear", true, false, false)]
    [InlineData("NextYear", false, true, true)]
    public void CreateOrderLinks_AreClosedOnlyForTheirOwnSubmittedYear(string action, bool currentOpen, bool nextOpen, bool expected)
    {
        var link = Section("Orders").Children.Single(child => child.ActionName == action);

        Assert.Equal(expected, link.IsOrderYearOpen(currentOpen, nextOpen));
    }

    [Fact]
    public void LinksOutsideOrders_AreNeverClosedByASubmittedOrder()
    {
        var reports = Section("Results").Children[0];

        Assert.True(reports.IsOrderYearOpen(currentYearOpen: false, nextYearOpen: false));
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
