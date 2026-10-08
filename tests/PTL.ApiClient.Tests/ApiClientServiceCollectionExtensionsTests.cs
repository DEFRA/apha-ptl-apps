using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PTL.ApiClient.Tests;

public class ApiClientServiceCollectionExtensionsTests
{
    [Fact]
    public void AddPtlApiClient_WithoutBaseUrl_Throws()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        Assert.Throws<InvalidOperationException>(() => services.AddPtlApiClient(configuration));
    }

    [Fact]
    public void AddPtlApiClient_WithBaseUrl_RegistersApiClient()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Api:BaseUrl"] = "http://localhost:5252" })
            .Build();

        services.AddPtlApiClient(configuration);
        var provider = services.BuildServiceProvider();

        Assert.IsType<ApiClient>(provider.GetRequiredService<IApiClient>());
        Assert.IsType<CustomerApiClient>(provider.GetRequiredService<ICustomerApiClient>());
        Assert.IsType<ParticipantApiClient>(provider.GetRequiredService<IParticipantApiClient>());
        Assert.IsType<ParticipantSchemeApiClient>(provider.GetRequiredService<IParticipantSchemeApiClient>());
        Assert.IsType<ContractApiClient>(provider.GetRequiredService<IContractApiClient>());
        Assert.IsType<GroupAddressApiClient>(provider.GetRequiredService<IGroupAddressApiClient>());
        Assert.IsType<SchemeApiClient>(provider.GetRequiredService<ISchemeApiClient>());
        Assert.IsType<LookupApiClient>(provider.GetRequiredService<ILookupApiClient>());
        Assert.IsType<ImportPermitApiClient>(provider.GetRequiredService<IImportPermitApiClient>());
        Assert.IsType<ContractExportApiClient>(provider.GetRequiredService<IContractExportApiClient>());
        Assert.IsType<ContractRenewalApiClient>(provider.GetRequiredService<IContractRenewalApiClient>());
        Assert.IsType<AdministrationChargeApiClient>(provider.GetRequiredService<IAdministrationChargeApiClient>());
        Assert.IsType<WeightedPricingPlanApiClient>(provider.GetRequiredService<IWeightedPricingPlanApiClient>());
        Assert.IsType<ExportTemplateApiClient>(provider.GetRequiredService<IExportTemplateApiClient>());
        Assert.IsType<BulkExportApiClient>(provider.GetRequiredService<IBulkExportApiClient>());
        Assert.IsType<InvoiceApiClient>(provider.GetRequiredService<IInvoiceApiClient>());
        Assert.IsType<SystemMessageApiClient>(provider.GetRequiredService<ISystemMessageApiClient>());
    }

    [Theory]
    [InlineData("ptl-api:8080")]
    [InlineData("not a url")]
    [InlineData("ftp://ptl-api:8080")]
    public void AddPtlApiClient_WithNonHttpBaseUrl_Throws(string baseUrl)
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Api:BaseUrl"] = baseUrl })
            .Build();

        Assert.Throws<InvalidOperationException>(() => services.AddPtlApiClient(configuration));
    }
}
