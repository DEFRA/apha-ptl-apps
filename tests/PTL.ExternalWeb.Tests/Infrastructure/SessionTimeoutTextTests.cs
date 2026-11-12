using PTL.ExternalWeb.Infrastructure;

namespace PTL.ExternalWeb.Tests.Infrastructure;

public class SessionTimeoutTextTests
{
    [Theory]
    [InlineData(120, "2 minutes")]
    [InlineData(60, "1 minute")]
    [InlineData(90, "1 minute 30 seconds")]
    [InlineData(61, "1 minute 1 second")]
    [InlineData(45, "45 seconds")]
    [InlineData(1, "1 second")]
    public void Duration_WritesWholeSecondsAsWords(int totalSeconds, string expected)
    {
        Assert.Equal(expected, SessionTimeoutText.Duration(totalSeconds));
    }
}
