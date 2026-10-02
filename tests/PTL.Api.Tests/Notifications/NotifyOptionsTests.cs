using PTL.Core.Notifications;

namespace PTL.Api.Tests.Notifications;

// NotifyOptions lives in PTL.Core; PTL.Data.Tests excludes PTL.Core from its own coverage scope,
// so this is covered here instead (PTL.Api.Tests only excludes PTL.Data).
public class NotifyOptionsTests
{
    [Fact]
    public void UsesExpectedDefaults()
    {
        var options = new NotifyOptions();

        Assert.Equal("GovUkNotify", NotifyOptions.SectionName);
        Assert.Equal(string.Empty, options.BaseUrl);
        Assert.Equal(string.Empty, options.ApiKey);
    }
}
