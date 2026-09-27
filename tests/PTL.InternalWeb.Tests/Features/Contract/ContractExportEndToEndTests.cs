using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using PTL.Contracts.Contract;
using PTL.Core.Contract.Document;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Features.Contract;

/// <summary>
/// Exercises the real template loader, repository, merge service and document service behind
/// <see cref="PTL.InternalWeb.Features.Contract.ContractController.Export"/> - only the API clients
/// are faked - so a break anywhere in the chain fails here rather than only in production.
/// </summary>
public class ContractExportEndToEndTests : IDisposable
{
    private readonly string _templateRoot = Path.Combine(Path.GetTempPath(), $"ptl-export-{Guid.NewGuid():N}");

    [Fact]
    public async Task Export_ProducesADownloadableDocxWithMergedValues()
    {
        WriteContractTemplate();

        var contractId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var controller = CreateController(contractId, customerId);

        var result = await controller.Export(contractId, "Contract", CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document", file.ContentType);
        Assert.Equal("Contract-QAL00001A.docx", file.FileDownloadName);

        using var stream = new MemoryStream(file.FileContents);
        using var document = WordprocessingDocument.Open(stream, false);
        var body = document.MainDocumentPart!.Document.Body!;

        Assert.Contains("QAL/00001", body.InnerText, StringComparison.Ordinal);
        Assert.Contains("Sample Laboratories Ltd", body.InnerText, StringComparison.Ordinal);
        Assert.DoesNotContain("MERGEFIELD", body.InnerText, StringComparison.OrdinalIgnoreCase);

        var rows = body.Descendants<TableRow>().ToList();
        Assert.Equal(2, rows.Count);
        Assert.Contains("Salmonella", rows[1].InnerText, StringComparison.Ordinal);
        Assert.Contains("\u00a342.50", rows[1].InnerText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Export_FallsBackWhenTheConvertedTemplateIsMissing()
    {
        Directory.CreateDirectory(_templateRoot);

        var contractId = Guid.NewGuid();
        var controller = CreateController(contractId, Guid.NewGuid());

        var result = await controller.Export(contractId, "Contract", CancellationToken.None);

        Assert.Equal("FeatureNotAvailable", Assert.IsType<ViewResult>(result).ViewName);
    }

    private PTL.InternalWeb.Features.Contract.ContractController CreateController(Guid contractId, Guid customerId)
    {
        var documentService = new ContractDocumentService(
            new FileTemplateRepository(new FileTemplateLoader(_templateRoot)),
            new TemplateMergeService());

        var apiClient = new FakeContractApiClient
        {
            ContractResponse = new ContractResponse(
                contractId, customerId, "Sample Laboratories Ltd", "QAL/00001", DateTime.UtcNow.Year, "UT3/306",
                string.Empty, "Alice Example", string.Empty, string.Empty, 0.1m, 25m, 2, 5m, 3, 4m, 1, 6m,
                null, null, null, string.Empty, null, true, false, "A", DateTime.UtcNow, string.Empty, false, false, false, null, null),
            ItemsResponse = new ContractItemsResponse(
                contractId, "A", DateTime.UtcNow.Year, "QAL/00001", "\u00a3", 0.1m, 25m,
                2, 5m, 10m, 3, 4m, 12m, 1, 6m, 6m, 9.5m, 85m, 95.5m, false,
                [
                    new ContractItemSchemeResponse(Guid.NewGuid(), "S1", "Salmonella",
                    [
                        new ContractItemResponse(Guid.NewGuid(), Guid.NewGuid(), "LAB1", "Lab One", "Lab One Ltd", 4, 42.5m, false, false)
                    ])
                ])
        };

        return new PTL.InternalWeb.Features.Contract.ContractController(
            apiClient,
            new FakeCustomerApiClient { CustomerResponse = SampleCustomer(customerId) },
            new FakeLookupApiClient(),
            new FakeImportPermitApiClient(),
            NullLogger<PTL.InternalWeb.Features.Contract.ContractController>.Instance,
            documentService,
            new FakeContractExportApiClient());
    }

    private static PTL.Contracts.Customer.CustomerResponse SampleCustomer(Guid customerId) => new(
        CustomerId: customerId, QalNumber: "QAL/00001", RegisteredFileNumber: string.Empty, Name: "Sample Laboratories Ltd",
        PreviousName: string.Empty, CustomerTypeId: Guid.Empty, VatNumber: string.Empty, VatRatingId: Guid.Empty,
        AccountNumber: string.Empty, CustomerFinanceId: string.Empty, ContactName: "Alice Example", Organisation: "Sample Laboratories Ltd",
        Address1: string.Empty, Address2: string.Empty, Address3: string.Empty, Address4: string.Empty, Address5: string.Empty,
        CountryId: Guid.Empty, Telephone: string.Empty, Telephone2: string.Empty, Fax: string.Empty, Email: string.Empty,
        CurrencyId: Guid.Empty, Comments: string.Empty, InitialStartDate: DateTime.UtcNow, PostageArrangements: string.Empty,
        PaymentNonUK: false, InvoiceName: string.Empty, InvoiceOrganisation: string.Empty, InvoiceAddress1: string.Empty,
        InvoiceAddress2: string.Empty, InvoiceAddress3: string.Empty, InvoiceAddress4: string.Empty, InvoiceAddress5: string.Empty,
        InvoiceCountryId: Guid.Empty, InvoiceTelephone: string.Empty, InvoiceTelephone2: string.Empty, InvoiceFax: string.Empty,
        InvoiceEmail: string.Empty, IsActive: true, CanOrderOnline: true, InactiveDate: null, CustomerStatusId: null);

    private void WriteContractTemplate()
    {
        Directory.CreateDirectory(_templateRoot);
        var path = Path.Combine(_templateRoot, "ContractExampleTemplate.docx");

        using var document = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        var mainPart = document.AddMainDocumentPart();

        var heading = new Paragraph();
        heading.Append(MergeField("ContractNumber"));
        heading.Append(MergeField("CustomerOrganisation"));

        var itemRow = new TableRow();
        var itemCell = new TableCell();
        var itemParagraph = new Paragraph();
        itemParagraph.Append(MergeField("TableStart:ContractItems"));
        itemParagraph.Append(MergeField("SchemeName"));
        itemParagraph.Append(MergeField("Price"));
        itemParagraph.Append(MergeField("TableEnd:ContractItems"));
        itemCell.AppendChild(itemParagraph);
        itemRow.AppendChild(itemCell);

        var headerRow = new TableRow(new TableCell(new Paragraph(new Run(new Text("Scheme")))));

        mainPart.Document = new Document(new Body(heading, new Table(headerRow, itemRow)));
    }

    private static OpenXmlElement[] MergeField(string name) =>
    [
        new Run(new FieldChar { FieldCharType = FieldCharValues.Begin }),
        new Run(new FieldCode($" MERGEFIELD  {name}  \\* MERGEFORMAT ")),
        new Run(new FieldChar { FieldCharType = FieldCharValues.Separate }),
        new Run(new Text($"\u00ab{name}\u00bb")),
        new Run(new FieldChar { FieldCharType = FieldCharValues.End })
    ];

    public void Dispose()
    {
        if (Directory.Exists(_templateRoot))
        {
            Directory.Delete(_templateRoot, true);
        }

        GC.SuppressFinalize(this);
    }
}
