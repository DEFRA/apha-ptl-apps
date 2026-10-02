using Microsoft.Extensions.Hosting;

namespace PTL.InternalWeb.Tests.TestSupport;

// Minimal IHostEnvironment test double so InvoiceController's environment-gated Reset action can
// be tested for both Production (hidden/blocked) and non-Production (allowed) behaviour.
internal sealed class FakeHostEnvironment(string environmentName) : IHostEnvironment
{
    public string EnvironmentName { get; set; } = environmentName;
    public string ApplicationName { get; set; } = "PTL.InternalWeb.Tests";
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
}
