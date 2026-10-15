using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using PTL.Core.Distribution;

namespace PTL.Api.Tests.Distribution;

// Program.cs registers the Amend Results service so the [FromServices] actions resolve.
public class AmendResultsRegistrationTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public void AmendResultsService_ResolvesFromTheRealContainer()
    {
        using var scope = factory.Services.CreateScope();

        Assert.IsType<AmendResultsService>(scope.ServiceProvider.GetRequiredService<IAmendResultsService>());
    }
}
