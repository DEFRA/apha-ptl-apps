using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PTL.Common.SessionTimeout;

namespace PTL.Common.Tests.SessionTimeout;

public class SessionTimeoutServiceCollectionExtensionsTests
{
    private static ServiceProvider Build(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new ServiceCollection().AddPtlSessionTimeout(configuration).BuildServiceProvider();
    }

    [Fact]
    public void AddPtlSessionTimeout_BindsTheConfiguredDurations()
    {
        using var provider = Build(new()
        {
            ["SessionTimeout:InactivityWarningAfter"] = "00:20:00",
            ["SessionTimeout:ExpireAfterWarning"] = "00:03:00"
        });

        var options = provider.GetRequiredService<IOptions<SessionTimeoutOptions>>().Value;

        Assert.Equal(TimeSpan.FromMinutes(20), options.InactivityWarningAfter);
        Assert.Equal(TimeSpan.FromMinutes(3), options.ExpireAfterWarning);
    }

    [Fact]
    public void AddPtlSessionTimeout_WithoutASection_UsesTheDefaults()
    {
        using var provider = Build([]);

        var options = provider.GetRequiredService<IOptions<SessionTimeoutOptions>>().Value;

        Assert.Equal(TimeSpan.FromMinutes(15), options.InactivityWarningAfter);
        Assert.Equal(TimeSpan.FromMinutes(2), options.ExpireAfterWarning);
    }

    [Fact]
    public void AddPtlSessionTimeout_WithANonPositiveDuration_FailsValidation()
    {
        using var provider = Build(new() { ["SessionTimeout:ExpireAfterWarning"] = "00:00:00" });

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<SessionTimeoutOptions>>().Value);
    }
}
