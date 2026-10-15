using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PTL.Core.Distribution;

namespace PTL.Api.Tests.Distribution;

// Program.cs registrations for View Comments: the service and the "Attachments" appsettings section.
public class TestConsultantCommentsRegistrationTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public void TestConsultantCommentsService_ResolvesFromTheRealContainer()
    {
        using var scope = factory.Services.CreateScope();

        Assert.IsType<TestConsultantCommentsService>(scope.ServiceProvider.GetRequiredService<ITestConsultantCommentsService>());
    }

    [Fact]
    public void AttachmentOptions_AreBoundFromAppSettingsWithoutDuplicatingTheDefaults()
    {
        using var scope = factory.Services.CreateScope();

        var options = scope.ServiceProvider.GetRequiredService<IOptions<AttachmentOptions>>().Value;

        Assert.Equal([".pdf", ".jpeg", ".jpg", ".xls", ".xlsx", ".pptx"], options.EffectiveAllowedFileTypes);
        Assert.Equal(10485760, options.MaxFileSize);
    }

    [Fact]
    public void AttachmentOptions_WithNothingConfigured_UseTheLegacyDefaults()
    {
        var options = new AttachmentOptions();

        Assert.Equal([".pdf", ".jpeg", ".jpg", ".xls", ".xlsx", ".pptx"], options.EffectiveAllowedFileTypes);
        Assert.Equal(10485760, options.MaxFileSize);
    }
}
