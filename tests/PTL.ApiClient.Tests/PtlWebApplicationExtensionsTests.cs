using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace PTL.ApiClient.Tests;

/// <summary>
/// Tests for the PtlWebApplicationExtensions shared startup bootstrap.
/// Focuses on the ApiClientServiceCollectionExtensions which is called by AddPtlWebFrontEnd.
/// </summary>
public class PtlWebApplicationExtensionsTests
{
    [Fact]
    public void AddPtlApiClient_WithValidBaseUrl_RegistersHttpClients()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string?>("Api:BaseUrl", "http://localhost:8080")
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();

        ApiClientServiceCollectionExtensions.AddPtlApiClient(services, config);

        // Verify key client registrations
        Assert.NotNull(services.FirstOrDefault(s => s.ServiceType == typeof(IApiClient)));
        Assert.NotNull(services.FirstOrDefault(s => s.ServiceType == typeof(ICustomerApiClient)));
        Assert.NotNull(services.FirstOrDefault(s => s.ServiceType == typeof(IParticipantApiClient)));
        Assert.NotNull(services.FirstOrDefault(s => s.ServiceType == typeof(IContractApiClient)));
        Assert.NotNull(services.FirstOrDefault(s => s.ServiceType == typeof(ISchemeApiClient)));
        Assert.NotNull(services.FirstOrDefault(s => s.ServiceType == typeof(ILookupApiClient)));
    }

    [Fact]
    public void AddPtlApiClient_WithMissingBaseUrl_ThrowsInvalidOperationException()
    {
        var config = new ConfigurationBuilder().Build(); // Empty, no Api:BaseUrl

        var services = new ServiceCollection();
        services.AddLogging();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            ApiClientServiceCollectionExtensions.AddPtlApiClient(services, config));

        Assert.Contains("Api:BaseUrl", ex.Message);
    }

    [Fact]
    public void AddPtlApiClient_WithMalformedUrl_ThrowsInvalidOperationException()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string?>("Api:BaseUrl", "not-a-valid-url")
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            ApiClientServiceCollectionExtensions.AddPtlApiClient(services, config));

        Assert.Contains("http://", ex.Message);
    }

    [Fact]
    public void AddPtlApiClient_WithFtpScheme_ThrowsInvalidOperationException()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string?>("Api:BaseUrl", "ftp://invalid-scheme.com")
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            ApiClientServiceCollectionExtensions.AddPtlApiClient(services, config));

        Assert.Contains("http://", ex.Message);
    }

    private static WebApplicationBuilder CreateFrontEndBuilder(string environmentName = "Production")
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environmentName });
        builder.Configuration["Api:BaseUrl"] = "http://localhost:5252";
        return builder;
    }

    [Fact]
    public void AddPtlWebFrontEnd_RegistersExpectedServices()
    {
        var builder = CreateFrontEndBuilder();

        var result = builder.AddPtlWebFrontEnd();

        Assert.Same(builder, result);
        using var provider = builder.Services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<ICustomerApiClient>());
    }

    [Fact]
    public void AddPtlWebFrontEnd_ConfiguresFriendlyModelBindingMessages()
    {
        var builder = CreateFrontEndBuilder();
        builder.AddPtlWebFrontEnd();
        using var provider = builder.Services.BuildServiceProvider();

        var mvcOptions = provider.GetRequiredService<IOptions<MvcOptions>>().Value;

        Assert.True(mvcOptions.ValidateComplexTypesIfChildValidationFails);
        var messages = mvcOptions.ModelBindingMessageProvider;
        Assert.Equal("Enter a valid value for Number of courier items", messages.AttemptedValueIsInvalidAccessor("abc", "Number of courier items"));
        Assert.Equal("Enter a valid value for Number of courier items", messages.ValueMustNotBeNullAccessor("Number of courier items"));
        Assert.Equal("Enter a valid value for Number of courier items", messages.ValueMustBeANumberAccessor("Number of courier items"));
    }

    [Fact]
    public void AddPtlWebFrontEnd_AddsFeatureFolderViewLocationsAndCookiePaths()
    {
        var builder = CreateFrontEndBuilder();
        builder.AddPtlWebFrontEnd();
        using var provider = builder.Services.BuildServiceProvider();

        var razorOptions = provider.GetRequiredService<IOptions<RazorViewEngineOptions>>().Value;

        Assert.Equal("/Features/{1}/Views/{0}.cshtml", razorOptions.ViewLocationFormats[0]);
        Assert.Equal("/Features/Shared/{0}.cshtml", razorOptions.ViewLocationFormats[1]);

        var cookieOptions = provider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);

        Assert.Equal("/Account/Login", cookieOptions.LoginPath);
        Assert.Equal("/Account/Logout", cookieOptions.LogoutPath);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public void UsePtlWebFrontEnd_BuildsPipelineForEveryEnvironment(string environmentName)
    {
        var builder = CreateFrontEndBuilder(environmentName);
        builder.AddPtlWebFrontEnd();
        var app = builder.Build();

        var result = app.UsePtlWebFrontEnd();

        Assert.Same(app, result);
    }
}
