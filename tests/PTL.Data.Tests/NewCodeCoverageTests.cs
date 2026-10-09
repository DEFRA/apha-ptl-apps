using System.Data;
using System.Net;
using System.Net.Http;
using Microsoft.Extensions.Options;
using PTL.Contracts.Participant;
using PTL.Core.Invoice;
using PTL.Core.Notifications;
using PTL.Core.Participant;
using PTL.Core.Scheme;
using PTL.Core.Viewer;
using PTL.Data.Infrastructure;
using PTL.Data.Notifications;
using PTL.Data.Participant;
using PTL.Data.Storage;
using PTL.Data.Tests.Fakes;
using PTL.Data.Viewer;
using CoreScheme = PTL.Core.Scheme.Scheme;

namespace PTL.Data.Tests;

public class NewCodeCoverageTests
{
    static NewCodeCoverageTests() => DapperColumnMappings.Register();

    [Fact]
    public async Task InMemoryInvoiceStorageService_SaveAndGet_RoundTripsContent()
    {
        var service = new InMemoryInvoiceStorageService();
        var content = new byte[] { 1, 2, 3, 4 };

        await service.SaveAsync("invoice-001.csv", content, "text/csv");

        var result = await service.GetAsync("invoice-001.csv");
        Assert.Equal(content, result);
        Assert.Null(await service.GetAsync("missing.csv"));
    }

    [Fact]
    public void NotifyOptions_UsesExpectedDefaults()
    {
        var options = new NotifyOptions();

        Assert.Equal("Notification", NotifyOptions.SectionName);
        Assert.Equal(string.Empty, options.BaseUrl);
        Assert.Equal(string.Empty, options.ApiKey);
    }

    [Fact]
    public async Task NotifyClient_SendsAuthenticatedEmailRequest()
    {
        var handler = new CapturingHandler();
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.notifications.service.gov.uk/")
        };

        // BuildToken only needs length >= 73 with a hyphen at index 36 of the last 73 chars - no
        // "notify-" style prefix or GUID-shaped segments, so this can't match GOV.UK Notify's real
        // API-key pattern and trip GitHub secret-scanning push protection.
        var apiKey = new string('a', 36) + "-" + new string('b', 36);
        var client = new NotifyClient(httpClient, Options.Create(new NotifyOptions { ApiKey = apiKey }));

        await client.SendEmailAsync(
            "template-123",
            "user@example.com",
            new Dictionary<string, string> { ["name"] = "Alice" },
            "ref-123");

        Assert.NotNull(handler.LastRequest);
        var request = handler.LastRequest!;
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/v2/notifications/email", request.RequestUri!.AbsolutePath);
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        Assert.False(string.IsNullOrEmpty(request.Headers.Authorization.Parameter));

        var payload = handler.LastPayload;
        Assert.False(string.IsNullOrEmpty(payload));
        Assert.Contains("template-123", payload);
        Assert.Contains("user@example.com", payload);
        Assert.Contains("Alice", payload);
        Assert.Contains("ref-123", payload);
    }

    [Fact]
    public async Task NotifyClient_WithInvalidApiKey_ThrowsMeaningfulException()
    {
        var handler = new CapturingHandler();
        using var httpClient = new HttpClient(handler);
        var client = new NotifyClient(httpClient, Options.Create(new NotifyOptions { ApiKey = "short" }));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.SendEmailAsync("template", "user@example.com"));

        Assert.Contains("Notification:ApiKey", ex.Message);
    }

    [Fact]
    public async Task NotifyClient_WithEmptyApiKey_ThrowsMeaningfulException()
    {
        var handler = new CapturingHandler();
        using var httpClient = new HttpClient(handler);
        var client = new NotifyClient(httpClient, Options.Create(new NotifyOptions { ApiKey = "   " }));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.SendEmailAsync("template", "user@example.com"));

        Assert.Contains("Notification:ApiKey", ex.Message);
    }

    [Fact]
    public async Task NotifyClient_NonSuccessResponse_ThrowsWithResponseBody()
    {
        var handler = new CapturingHandler
        {
            ResponseStatusCode = HttpStatusCode.BadRequest,
            ResponseBody = "personalisation address_line_1 is missing"
        };
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.notifications.service.gov.uk/")
        };
        var apiKey = new string('a', 36) + "-" + new string('b', 36);
        var client = new NotifyClient(httpClient, Options.Create(new NotifyOptions { ApiKey = apiKey }));

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.SendEmailAsync("template-123", "user@example.com"));

        Assert.Contains("400", ex.Message);
        Assert.Contains("personalisation address_line_1 is missing", ex.Message);
    }

    [Fact]
    public async Task NotifyClient_SendEmailAsync_SendsPersonalisationIncludingDownloadUrl()
    {
        var handler = new CapturingHandler();
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.notifications.service.gov.uk/")
        };

        var apiKey = new string('a', 36) + "-" + new string('b', 36);
        var client = new NotifyClient(httpClient, Options.Create(new NotifyOptions { ApiKey = apiKey }));

        await client.SendEmailAsync(
            "template-123",
            "user@example.com",
            new Dictionary<string, string>
            {
                ["generationDateTime"] = "2026/10/03 21:23:16",
                ["downloadUrl"] = "https://ptlims.example/Invoice/Download/11111111-1111-1111-1111-111111111111"
            },
            "ref-123");

        Assert.NotNull(handler.LastRequest);
        var payload = handler.LastPayload;
        Assert.False(string.IsNullOrEmpty(payload));
        Assert.Contains("template-123", payload);
        Assert.Contains("generationDateTime", payload);
        Assert.Contains("downloadUrl", payload);
        Assert.Contains("/Invoice/Download/11111111-1111-1111-1111-111111111111", payload);
    }

    [Fact]
    public async Task ViewerRepository_GetAllAsync_MapsEachViewerRow()
    {
        var connection = new FakeDbConnection();
        var table = new DataTable();
        table.Columns.Add("fldViewerId", typeof(Guid));
        table.Columns.Add("fldName", typeof(string));
        table.Columns.Add("fldEmail", typeof(string));
        table.Columns.Add("fldSsoId", typeof(Guid));
        var ssoId = Guid.NewGuid();
        table.Rows.Add(Guid.NewGuid(), "Alpha", "alpha@example.com", ssoId);
        table.Rows.Add(Guid.NewGuid(), "Beta", "beta@example.com", Guid.NewGuid());
        connection.RespondToQuery("EXEC dbo.spgaViewers", table);

        var repository = new ViewerRepository(new FakeDbConnectionFactory(connection));

        var result = await repository.GetAllAsync();

        Assert.Equal(2, result.Count);
        Assert.Equal("Alpha", result[0].Name);
        Assert.Equal(ssoId, result[0].SsoId);
        Assert.Equal("beta@example.com", result[1].Email);
    }

    [Fact]
    public async Task ParticipantViewerRepository_GetAddAndRemove_WorkAsExpected()
    {
        var participantId = Guid.NewGuid();
        var connection = new FakeDbConnection();
        var firstTable = new DataTable();
        firstTable.Columns.Add("ViewerParticipantId", typeof(Guid));
        firstTable.Columns.Add("ViewerId", typeof(Guid));
        firstTable.Columns.Add("ParticipantId", typeof(Guid));
        firstTable.Columns.Add("Name", typeof(string));
        firstTable.Rows.Add(Guid.NewGuid(), Guid.NewGuid(), participantId, "Assigned Viewer");

        var secondTable = new DataTable();
        secondTable.Columns.Add("fldViewerParticipantId", typeof(Guid));
        secondTable.Columns.Add("fldViewerId", typeof(Guid));
        secondTable.Columns.Add("fldParticipantId", typeof(Guid));
        secondTable.Columns.Add("fldName", typeof(string));
        secondTable.Rows.Add(Guid.NewGuid(), Guid.NewGuid(), participantId, "Assigned Viewer");

        var dataSet = new DataSet();
        dataSet.Tables.Add(firstTable);
        dataSet.Tables.Add(secondTable);
        connection.RespondToQuery("EXEC dbo.spgViewerParticipants @ParticipantId", dataSet);
        connection.RespondToNonQuery("EXEC dbo.spiViewerParticipant @ViewerParticipantId = @ViewerParticipantId, @ViewerId = @ViewerId, @ParticipantId = @ParticipantId", 1);
        connection.RespondToNonQuery("EXEC dbo.spdViewerParticipant @ViewerParticipantId", 1);

        var repository = new ParticipantViewerRepository(new FakeDbConnectionFactory(connection));

        var assigned = await repository.GetByParticipantIdAsync(participantId);
        Assert.Single(assigned);
        Assert.Equal("Assigned Viewer", assigned[0].Name);

        await repository.AddAsync(Guid.NewGuid(), Guid.NewGuid(), participantId);
        await repository.RemoveAsync(Guid.NewGuid());

        Assert.Equal(3, connection.ExecutedCommands.Count);
    }

    [Fact]
    public void InvoiceContractEntity_ComputedValues_MatchLegacyRules()
    {
        var contract = new InvoiceContractEntity
        {
            NumberPostage = 2,
            PostagePrice = 3.5m,
            NumberCourier = 4,
            CourierPrice = 2m,
            NumberSpecialDelivery = 1,
            SpecialDeliveryPrice = 10m,
            QalNumber = "QAL/123",
            Suffix = "A",
            InvoiceAddress1 = "1 High Street",
            InvoiceAddress2 = "Town",
            InvoiceAddress3 = "County",
            InvoiceAddress4 = "Region",
            InvoiceAddress5 = "Postcode",
            InvoiceCountry = "United Kingdom"
        };

        Assert.Equal(2 * 3.5m + 4 * 2m + 1 * 10m, contract.CombinedPostagePriceTotal);
        Assert.Equal("Provision of PT Services Contract Ref No.QAL/123A", contract.SpecialInstructions);
        Assert.Equal("1 High Street, Town, County, Region, Postcode, United Kingdom", contract.InvoiceDetails);
    }

    [Fact]
    public void InvoiceContractItemEntity_Description_UsesLegacyFormat()
    {
        var item = new InvoiceContractItemEntity
        {
            SchemeIdentifier = "SCHEME-01",
            SchemeName = "Salmonella"
        };

        Assert.Equal("SCHEME-01 Salmonella", item.Description);
    }

    [Fact]
    public void ParticipantViewerAssignmentResponse_ExposesExpectedValues()
    {
        var available = new[] { new ViewerResponse(Guid.NewGuid(), "Available Person", "available@example.com") };
        var assigned = new[] { new ViewerResponse(Guid.NewGuid(), "Assigned Person", "assigned@example.com") };

        var response = new ParticipantViewerAssignmentResponse(
            Guid.NewGuid(), "LAB-01", "Lab Name", true, available, assigned);

        Assert.Equal("LAB-01", response.LabCode);
        Assert.Equal("Lab Name", response.LabName);
        Assert.True(response.IsActive);
        Assert.Equal(available, response.AvailableViewers);
        Assert.Equal(assigned, response.AssignedViewers);
    }

    [Fact]
    public void SchemeIdentifier_Normalise_UsesLegacyRulesForWhitespaceAndNumericValues()
    {
        Assert.Equal(string.Empty, SchemeIdentifier.Normalise(null));
        Assert.Equal(string.Empty, SchemeIdentifier.Normalise("   "));
        Assert.Equal("PT0001", SchemeIdentifier.Normalise("0001"));
        Assert.Equal("PT2025", SchemeIdentifier.Normalise(" 2025 "));
        Assert.Equal("ABC123", SchemeIdentifier.Normalise("abc123"));
        Assert.Equal("PTL-001", SchemeIdentifier.Normalise("ptl-001"));
    }

    [Fact]
    public void SchemeStartDate_Calculate_UsesDistributionAndFallbackRules()
    {
        var contractStart = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Unspecified);

        var availableScheme = new CoreScheme
        {
            YearId = 2027,
            DistributionAsAvailable = true
        };

        Assert.Equal(new DateTime(2027, 7, 1, 0, 0, 0, DateTimeKind.Unspecified),
            SchemeStartDate.Calculate(availableScheme, contractStart));

        var monthDrivenScheme = new CoreScheme
        {
            YearId = 2026,
            DistributionMonthApr = true,
            DistributionMonthMay = true,
            DistributionMonthSep = true,
            DistributionMonthOct = true
        };

        var janContractStart = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
        Assert.Equal(new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Unspecified),
            SchemeStartDate.Calculate(monthDrivenScheme, janContractStart));

        var nextYearFallbackScheme = new CoreScheme
        {
            YearId = 2026,
            DistributionMonthJan = true,
            DistributionMonthFeb = true
        };

        var lateContractStart = new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Unspecified);
        Assert.Equal(new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Unspecified),
            SchemeStartDate.Calculate(nextYearFallbackScheme, lateContractStart));

        var noDistributionScheme = new CoreScheme { YearId = 2026 };
        Assert.Equal(new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Unspecified),
            SchemeStartDate.Calculate(noDistributionScheme, contractStart));
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string LastPayload { get; private set; } = string.Empty;
        public HttpStatusCode ResponseStatusCode { get; set; } = HttpStatusCode.OK;
        public string ResponseBody { get; set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (request.Content is not null)
            {
                LastPayload = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            return new HttpResponseMessage(ResponseStatusCode) { Content = new StringContent(ResponseBody) };
        }
    }
}
