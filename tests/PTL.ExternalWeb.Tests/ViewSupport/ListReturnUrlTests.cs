using PTL.ExternalWeb.ViewSupport;

namespace PTL.ExternalWeb.Tests.ViewSupport;

public class ListReturnUrlTests
{
    [Theory]
    [InlineData("/Distribution/Previous")]
    [InlineData("/Distribution/Previous?year=2026&month=1")]
    [InlineData("/distribution/previous?year=2026")]
    [InlineData("/app/Distribution/Previous?page=2#results")]
    public void Validate_RelativeUrlForTheExpectedList_IsAccepted(string url) =>
        Assert.Equal(url, ListReturnUrl.Validate(url, ListReturnUrl.DistributionPrevious));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Distribution/Previous")]
    [InlineData("https://evil.example/Distribution/Previous")]
    [InlineData("//evil.example/Distribution/Previous")]
    [InlineData("/\\evil.example/Distribution/Previous")]
    [InlineData("/Distribution\\Previous")]
    [InlineData("javascript:alert(1)")]
    [InlineData("/Tabulation/Index")]
    [InlineData("/Distribution/Previous/../Admin")]
    [InlineData("/Distribution/Previous\r\nSet-Cookie: a=b")]
    public void Validate_AnythingElse_IsRejected(string? url) =>
        Assert.Null(ListReturnUrl.Validate(url, ListReturnUrl.DistributionPrevious));

    [Fact]
    public void Validate_OverlongUrl_IsRejected() =>
        Assert.Null(ListReturnUrl.Validate("/Distribution/Previous?x=" + new string('a', 2100), ListReturnUrl.DistributionPrevious));

    [Fact]
    public void Validate_TabulationListUrl_IsAcceptedForTheTabulationList() =>
        Assert.Equal("/Tabulation/Index?year=2026", ListReturnUrl.Validate("/Tabulation/Index?year=2026", ListReturnUrl.TabulationIndex));

    [Theory]
    [InlineData("Distribution", "Previous", "/Distribution/Previous")]
    [InlineData("Tabulation", "Index", "/Tabulation/Index")]
    [InlineData("Tabulation", "Report", null)]
    [InlineData("Distribution", "Current", null)]
    [InlineData(null, null, null)]
    public void ListPathFor_ReturnsThePathOfFilterableListsOnly(string? controller, string? action, string? expected) =>
        Assert.Equal(expected, ListReturnUrl.ListPathFor(controller, action));
}
