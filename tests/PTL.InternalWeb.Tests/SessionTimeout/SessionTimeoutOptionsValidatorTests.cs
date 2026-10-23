using PTL.InternalWeb.SessionTimeout;

namespace PTL.InternalWeb.Tests.SessionTimeout;

public class SessionTimeoutOptionsValidatorTests
{
    private static SessionTimeoutOptions ValidOptions() => new()
    {
        InactivityWarningAfter = TimeSpan.FromMinutes(15),
        ExpireAfterWarning = TimeSpan.FromMinutes(2)
    };

    [Fact]
    public void Validate_AllRequiredValuesPresent_Succeeds()
    {
        var result = new SessionTimeoutOptionsValidator().Validate(null, ValidOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_InactivityWarningAfterZero_Fails()
    {
        var options = ValidOptions();
        options.InactivityWarningAfter = TimeSpan.Zero;

        var result = new SessionTimeoutOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains(nameof(SessionTimeoutOptions.InactivityWarningAfter)));
    }

    [Fact]
    public void Validate_InactivityWarningAfterNegative_Fails()
    {
        var options = ValidOptions();
        options.InactivityWarningAfter = TimeSpan.FromMinutes(-1);

        var result = new SessionTimeoutOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains(nameof(SessionTimeoutOptions.InactivityWarningAfter)));
    }

    [Fact]
    public void Validate_ExpireAfterWarningZero_Fails()
    {
        var options = ValidOptions();
        options.ExpireAfterWarning = TimeSpan.Zero;

        var result = new SessionTimeoutOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains(nameof(SessionTimeoutOptions.ExpireAfterWarning)));
    }

    [Fact]
    public void Validate_ExpireAfterWarningNegative_Fails()
    {
        var options = ValidOptions();
        options.ExpireAfterWarning = TimeSpan.FromMinutes(-1);

        var result = new SessionTimeoutOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains(nameof(SessionTimeoutOptions.ExpireAfterWarning)));
    }

    [Fact]
    public void Validate_BothValuesInvalid_ReportsBothFailures()
    {
        var options = new SessionTimeoutOptions
        {
            InactivityWarningAfter = TimeSpan.Zero,
            ExpireAfterWarning = TimeSpan.Zero
        };

        var result = new SessionTimeoutOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains(nameof(SessionTimeoutOptions.InactivityWarningAfter)));
        Assert.Contains(result.Failures!, f => f.Contains(nameof(SessionTimeoutOptions.ExpireAfterWarning)));
        Assert.Equal(2, result.Failures!.Count());
    }
}
