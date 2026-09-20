using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace PTL.InternalWeb.Tests.TestSupport;

internal sealed class FakeHostEnvironment : IHostEnvironment
{
    public required string EnvironmentName { get; set; }
    public string ApplicationName { get; set; } = "PTL.InternalWeb.Tests";
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
