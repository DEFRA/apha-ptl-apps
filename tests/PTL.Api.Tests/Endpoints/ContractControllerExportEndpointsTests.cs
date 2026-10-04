using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using PTL.Api.Controllers;
using PTL.Api.Tests.Contract;
using PTL.Contracts.Contract;
using PTL.Core.Contract;
using PTL.Core.Contract.Export.Bulk;
using PTL.Core.Contract.Export.Templates;
using PTL.Core.Contract.ImportPermit;
using PTL.Core.Contract.Renewal;
using PTL.Core.Contract.SampleAddress;

namespace PTL.Api.Tests.Endpoints;

// Covers the bulk-export and export-template endpoints, whose response mappers were never reached
// while the stub services returned empty lists.
public class ContractControllerExportEndpointsTests
{
    private static ContractController CreateController(
        StubBulkExportService? bulkExportService = null,
        StubExportTemplateService? exportTemplateService = null) =>
        new(
            new ContractService(new FakeContractRepository(), new FakeParticipantSchemeRepository(), NullLogger<ContractService>.Instance),
            new ImportPermitService(new FakeImportPermitRepository()),
            new StubSampleAddressService(),
            new StubContractRenewalService(),
            new StubRenewContractsService(),
            new StubPendingOrderService(),
            exportTemplateService ?? new StubExportTemplateService(),
            bulkExportService ?? new StubBulkExportService(),
            NullLogger<ContractController>.Instance);

    private static BulkContractEntity SampleContract(Guid contractId, Guid customerId) => new()
    {
        ContractId = contractId,
        CustomerId = customerId,
        ContractNumber = "UT123",
        Suffix = "A",
        YearId = 2026,
        Symbol = "£",
        AdministrationCharge = 10m,
        DiscountRate = 0.1m,
        NumberPostage = 2,
        NumberCourier = 1,
        NumberSpecialDelivery = 3,
        PostagePrice = 1m,
        CourierPrice = 2m,
        SpecialDeliveryPrice = 3m,
        CommencementDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        QalNumber = "QAL/00001",
        ContactName = "Alice Example",
        Organisation = "Sample Labs",
        Address1 = "1 Sample Street",
        Address2 = "Sample District",
        Address3 = "A3",
        Address4 = "A4",
        Address5 = "A5",
        Country = "United Kingdom",
        Telephone = "01234 567890",
        Fax = "01234 567891",
        Email = "alice@example.com",
        InvoiceName = "Accounts Payable",
        InvoiceOrganisation = "Sample Labs Finance",
        InvoiceAddress1 = "2 Invoice Road",
        InvoiceAddress2 = "IA2",
        InvoiceAddress3 = "IA3",
        InvoiceAddress4 = "IA4",
        InvoiceAddress5 = "IA5",
        InvoiceCountry = "United Kingdom",
        InvoiceTelephone = "01234 000000",
        InvoiceFax = "01234 000001",
        InvoiceEmail = "invoices@example.com",
        AccountNumber = "ACC-12345",
        VatNumber = "GB123",
        VatRating = "Standard",
        PurchaseOrderNumber = "PO-1",
        Items =
        [
            new BulkContractItemEntity
            {
                ContractId = contractId,
                Identifier = "ITEM-1",
                SchemeName = "Scheme One",
                LabCode = "LAB-01",
                LabName = "Alice Lab",
                NoOfDistributions = 4,
                Price = 100m
            }
        ]
    };

    [Fact]
    public async Task GetBulkContracts_MapsEveryMergeFieldOntoTheResponse()
    {
        var contractId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var controller = CreateController(new StubBulkExportService { Contracts = [SampleContract(contractId, customerId)] });

        var result = await controller.GetBulkContracts(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var contracts = Assert.IsType<IReadOnlyList<BulkContractResponse>>(ok.Value, exactMatch: false);
        var contract = Assert.Single(contracts);

        Assert.Equal(contractId, contract.ContractId);
        Assert.Equal(customerId, contract.CustomerId);
        Assert.Equal("UT123", contract.ContractNumber);
        Assert.Equal("QAL/00001", contract.QalNumber);
        Assert.Equal("Alice Example", contract.ContactName);
        Assert.Equal("United Kingdom", contract.Country);
        Assert.Equal("Accounts Payable", contract.InvoiceName);
        Assert.Equal("invoices@example.com", contract.InvoiceEmail);
        Assert.Equal("ACC-12345", contract.AccountNumber);
        Assert.Equal("PO-1", contract.PurchaseOrderNumber);
    }

    // Computed totals come from the entity, so the mapper must forward them rather than recalculate.
    [Fact]
    public async Task GetBulkContracts_ForwardsComputedTotalsAndItems()
    {
        var entity = SampleContract(Guid.NewGuid(), Guid.NewGuid());
        var controller = CreateController(new StubBulkExportService { Contracts = [entity] });

        var result = await controller.GetBulkContracts(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var contract = Assert.Single(Assert.IsType<IReadOnlyList<BulkContractResponse>>(ok.Value, exactMatch: false));

        Assert.Equal(entity.TotalPriceItems, contract.TotalPriceItems);
        Assert.Equal(entity.DiscountPrice, contract.DiscountPrice);
        Assert.Equal(entity.TotalPrice, contract.TotalPrice);

        var item = Assert.Single(contract.Items);
        Assert.Equal("ITEM-1", item.Identifier);
        Assert.Equal("Scheme One", item.SchemeName);
        Assert.Equal("LAB-01", item.LabCode);
        Assert.Equal(4, item.NoOfDistributions);
        Assert.Equal(100m, item.Price);
    }

    [Fact]
    public async Task GetBulkContracts_NoContracts_ReturnsEmptyList()
    {
        var controller = CreateController();

        var result = await controller.GetBulkContracts(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Empty(Assert.IsType<IReadOnlyList<BulkContractResponse>>(ok.Value, exactMatch: false));
    }

    [Fact]
    public async Task GetBulkSampleAddresses_MapsAddresses()
    {
        var contractId = Guid.NewGuid();
        var bulk = new StubBulkExportService
        {
            SampleAddresses =
            [
                new SampleAddressEntity
                {
                    ContractId = contractId,
                    LabCode = "LAB-01",
                    QalNumber = "QAL/00001",
                    ContactName = "Alice Example",
                    Address1 = "1 Sample Street",
                    Country = "United Kingdom"
                }
            ]
        };
        var controller = CreateController(bulk);

        var result = await controller.GetBulkSampleAddresses(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var address = Assert.Single(Assert.IsType<IReadOnlyList<SampleAddressResponse>>(ok.Value, exactMatch: false));
        Assert.Equal("LAB-01", address.LabCode);
        Assert.Equal("United Kingdom", address.Country);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetBulkRenewals_PassesNonUkFlagThroughAndMapsRenewals(bool nonUk)
    {
        var bulk = new StubBulkExportService
        {
            Renewals =
            [
                new ContractRenewalEntity
                {
                    ContractId = Guid.NewGuid(),
                    QalNumber = "QAL/00001",
                    ContactName = "Alice Example",
                    Country = "United Kingdom"
                }
            ]
        };
        var controller = CreateController(bulk);

        var result = await controller.GetBulkRenewals(nonUk, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var renewal = Assert.Single(Assert.IsType<IReadOnlyList<ContractRenewalResponse>>(ok.Value, exactMatch: false));
        Assert.Equal("QAL/00001", renewal.QalNumber);
        Assert.Equal(nonUk, bulk.LastNonUk);
    }

    [Fact]
    public async Task GetExportTemplates_MapsUploadedTemplates()
    {
        var fileId = Guid.NewGuid();
        var uploaded = new DateTime(2026, 2, 3, 0, 0, 0, DateTimeKind.Utc);
        var templates = new StubExportTemplateService
        {
            Templates =
            [
                new UploadedTemplate { FileId = fileId, Filename = "contract.docx", UploadedDate = uploaded, DocumentType = "Contract", Selected = true }
            ]
        };
        var controller = CreateController(exportTemplateService: templates);

        var result = await controller.GetExportTemplates("Contract", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ExportTemplateListResponse>(ok.Value);
        var template = Assert.Single(response.Templates);
        Assert.Equal(fileId, template.FileId);
        Assert.Equal("contract.docx", template.Filename);
        Assert.Equal(uploaded, template.UploadedDate);
        Assert.Equal("Contract", template.DocumentType);
        Assert.True(template.Selected);
    }

    [Fact]
    public async Task DownloadExportTemplate_KnownFile_ReturnsFileContent()
    {
        var templates = new StubExportTemplateService
        {
            Download = new ExportTemplateContent("contract.docx", ExportTemplateService.DocxContentType, [1, 2, 3])
        };
        var controller = CreateController(exportTemplateService: templates);

        var result = await controller.DownloadExportTemplate(Guid.NewGuid(), CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("contract.docx", file.FileDownloadName);
        Assert.Equal([1, 2, 3], file.FileContents);
    }

    [Fact]
    public async Task DownloadExportTemplate_UnknownFile_ReturnsNotFound()
    {
        var controller = CreateController(exportTemplateService: new StubExportTemplateService { Download = null });

        Assert.IsType<NotFoundResult>(await controller.DownloadExportTemplate(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task SelectExportTemplate_ReflectsServiceOutcome()
    {
        Assert.IsType<NoContentResult>(
            await CreateController(exportTemplateService: new StubExportTemplateService { SelectResult = true })
                .SelectExportTemplate(Guid.NewGuid(), CancellationToken.None));

        Assert.IsType<NotFoundResult>(
            await CreateController(exportTemplateService: new StubExportTemplateService { SelectResult = false })
                .SelectExportTemplate(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task DeleteExportTemplate_ReflectsServiceOutcome()
    {
        Assert.IsType<NoContentResult>(
            await CreateController(exportTemplateService: new StubExportTemplateService { DeleteResult = true })
                .DeleteExportTemplate(Guid.NewGuid(), CancellationToken.None));

        Assert.IsType<NotFoundResult>(
            await CreateController(exportTemplateService: new StubExportTemplateService { DeleteResult = false })
                .DeleteExportTemplate(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task GetExportTemplates_UnknownDocumentType_ReturnsNotFound()
    {
        var controller = CreateController();

        var result = await controller.GetExportTemplates("Not A Document Type", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    private static FormFile FileNamed(string fileName, byte[] content) =>
        new(new MemoryStream(content), 0, content.Length, "file", fileName);

    [Fact]
    public async Task UploadExportTemplate_ValidFile_ReturnsMappedUploadResponse()
    {
        var fileId = Guid.NewGuid();
        var templates = new StubExportTemplateService
        {
            UploadResult = new ExportTemplateUploadResult(
                true,
                null,
                new UploadedTemplate { FileId = fileId, Filename = "contract.docx", DocumentType = "Contracts", Selected = false })
        };
        var controller = CreateController(exportTemplateService: templates);

        var result = await controller.UploadExportTemplate("Contracts", FileNamed("contract.docx", [1, 2, 3]), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ExportTemplateUploadResponse>(ok.Value);
        Assert.True(response.Success);
        Assert.Null(response.ErrorMessage);
        Assert.Equal(fileId, response.Template!.FileId);
    }

    [Fact]
    public async Task UploadExportTemplate_RejectedByService_ReturnsFailureWithoutTemplate()
    {
        var templates = new StubExportTemplateService { UploadResult = new ExportTemplateUploadResult(false, "File not found", null) };
        var controller = CreateController(exportTemplateService: templates);

        var result = await controller.UploadExportTemplate("Contracts", FileNamed("contract.pdf", [1]), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ExportTemplateUploadResponse>(ok.Value);
        Assert.False(response.Success);
        Assert.Equal("File not found", response.ErrorMessage);
        Assert.Null(response.Template);
    }

    [Fact]
    public async Task UploadExportTemplate_UnknownDocumentType_ReturnsNotFound()
    {
        var controller = CreateController();

        var result = await controller.UploadExportTemplate("Not A Document Type", FileNamed("contract.docx", [1]), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task UploadExportTemplate_MissingOrEmptyFile_ReturnsFileNotFound()
    {
        var controller = CreateController();

        var missing = await controller.UploadExportTemplate("Contracts", null, CancellationToken.None);
        var empty = await controller.UploadExportTemplate("Contracts", FileNamed("contract.docx", []), CancellationToken.None);

        foreach (var result in new[] { missing, empty })
        {
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var response = Assert.IsType<ExportTemplateUploadResponse>(ok.Value);
            Assert.False(response.Success);
            Assert.Equal("File not found", response.ErrorMessage);
        }
    }
}
