using PTL.ExternalWeb.Features.Account;

namespace PTL.ExternalWeb.Tests.Features.Account;

public class AccountViewModelTests
{
    [Fact]
    public void Properties_RoundTrip()
    {
        var model = new AccountViewModel { Username = "alice", Password = "secret", ReturnUrl = "/dashboard" };

        Assert.Equal("alice", model.Username);
        Assert.Equal("secret", model.Password);
        Assert.Equal("/dashboard", model.ReturnUrl);
    }
}
