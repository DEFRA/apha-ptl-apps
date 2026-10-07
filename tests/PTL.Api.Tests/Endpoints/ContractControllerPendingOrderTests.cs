using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PTL.Api.Controllers;
using PTL.Api.Tests.Contract;
using PTL.Api.Tests.Customer;
using PTL.Api.Tests.Lookup;
using PTL.Api.Tests.Scheme;
using PTL.Contracts.Contract;
using PTL.Core.Contract;
using PTL.Core.Contract.ImportPermit;
using PTL.Core.Contract.PendingOrder;
using PTL.Core.Lookup;

namespace PTL.Api.Tests.Endpoints;

public class ContractControllerPendingOrderTests
{
    private const int CurrentYearId = 2026;
    private static readonly Guid CourierPostageId = Guid.NewGuid();
    private static readonly Guid UkCountryId = Guid.NewGuid();

    private sealed record Harness(ContractController Controller, FakePendingOrderRepository PendingOrders, Guid PendingContractId);

    private static async Task<Harness> CreateAsync()
    {
        var customerRepository = new FakeCustomerRepository();
        var customer = await customerRepository.CreateAsync(new PTL.Core.Customer.Customer
        {
            CustomerId = Guid.NewGuid(),
            Name = "Sample Laboratories Ltd",
            CountryId = UkCountryId,
            IsActive = true
        });

        var schemeRepository = new FakeSchemeRepository();
        var realScheme = new PTL.Core.Scheme.Scheme
        {
            SchemeId = Guid.NewGuid(),
            YearId = CurrentYearId,
            Identifier = "S1",
            Name = "Sample Scheme",
            Postage = CourierPostageId,
            DayOfWeekId = Guid.NewGuid(),
            WeekNumber = 1
        };
        await schemeRepository.CreateAsync(realScheme);

        var pendingContractId = Guid.NewGuid();
        var pendingOrders = new FakePendingOrderRepository();
        pendingOrders.Seed(
            new PendingOrderEntity
            {
                PendingContractId = pendingContractId,
                CustomerId = customer.CustomerId,
                YearId = CurrentYearId,
                IsSubmitted = true,
                PurchaseOrderNumber = "PO-1",
                CustomerName = customer.Name,
                CurrencySymbol = "£"
            },
            new PendingOrderSummaryEntity
            {
                PendingContractId = pendingContractId,
                CustomerId = customer.CustomerId,
                YearId = CurrentYearId,
                IsSubmitted = true,
                QalNumber = customer.QalNumber,
                CustomerName = customer.Name
            },
            new PendingOrderSchemeEntity
            {
                PendingParticipantSchemeId = Guid.NewGuid(),
                PendingContractId = pendingContractId,
                ParticipantId = Guid.NewGuid(),
                ParticipantName = "001: Alpha Lab",
                SchemeId = realScheme.SchemeId,
                SchemeIdentifier = "S1",
                SchemeName = "Sample Scheme",
                DistributionMonthApr = true,
                Price = 100m
            });

        var lookupRepository = new FakeLookupRepository
        {
            SystemSettings = new SystemSettingsEntity { CurrentYearId = CurrentYearId, NextYearId = CurrentYearId + 1 },
            Countries = [new CountryEntity { CountryId = UkCountryId, Country = "United Kingdom", CountryType = "UK" }],
            AllYears = [new YearEntity { YearId = CurrentYearId, Year = "2026/27" }],
            PostagePricingPlans = [new PostagePricingPlanEntity { PostageId = CourierPostageId, Name = "Courier", UKPrice = 10m, EUPrice = 20m, NonEUPrice = 30m, YearId = CurrentYearId }]
        };

        var pendingOrderService = new PendingOrderService(
            pendingOrders,
            new FakeContractRepository(),
            new FakeParticipantSchemeRepository(),
            schemeRepository,
            customerRepository,
            lookupRepository);

        var controller = new ContractController(
            new ContractService(new FakeContractRepository(), new FakeParticipantSchemeRepository(), NullLogger<ContractService>.Instance),
            new ImportPermitService(new FakeImportPermitRepository()),
            new StubSampleAddressService(),
            new StubContractRenewalService(),
            new StubRenewContractsService(),
            pendingOrderService,
            new StubExportTemplateService(),
            new StubBulkExportService(),
            NullLogger<ContractController>.Instance);

        var services = new ServiceCollection().AddMvc().Services.BuildServiceProvider();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { RequestServices = services }
        };

        return new Harness(controller, pendingOrders, pendingContractId);
    }

    [Fact]
    public async Task GetPendingOrders_ReturnsCurrentAndNextYearGrids()
    {
        var harness = await CreateAsync();

        var result = await harness.Controller.GetPendingOrders(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<PendingOrderListResponse>(ok.Value);
        Assert.Single(response.CurrentYearOrders);
        Assert.Empty(response.NextYearOrders);
        Assert.Equal("2026/27", response.CurrentYearOrders[0].Year);
    }

    [Fact]
    public async Task GetPendingOrder_UnknownOrder_ReturnsNotFound()
    {
        var harness = await CreateAsync();

        var result = await harness.Controller.GetPendingOrder(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetPendingOrder_ReturnsPricedOrder()
    {
        var harness = await CreateAsync();

        var result = await harness.Controller.GetPendingOrder(harness.PendingContractId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<PendingOrderDetailsResponse>(ok.Value);
        Assert.Single(response.Schemes);
        Assert.Equal(100m, response.TotalSchemePrice);
        Assert.Equal(10m, response.TotalPostagePrice);
        Assert.Equal(110m, response.Total);
        Assert.Equal("£", response.CurrencySymbol);
    }

    [Fact]
    public async Task UpdatePendingOrderScheme_UnknownScheme_ReturnsNotFound()
    {
        var harness = await CreateAsync();

        var result = await harness.Controller.UpdatePendingOrderScheme(harness.PendingContractId, Guid.NewGuid(), UpdateRequest(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task UpdatePendingOrderScheme_KnownScheme_ReturnsNoContent()
    {
        var harness = await CreateAsync();
        var schemes = await harness.PendingOrders.GetSchemesAsync(harness.PendingContractId);

        var result = await harness.Controller.UpdatePendingOrderScheme(
            harness.PendingContractId, schemes[0].PendingParticipantSchemeId, UpdateRequest(isRemoved: true), CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.True(schemes[0].IsRemoved);
    }

    [Fact]
    public async Task ApprovePendingOrder_UnknownOrder_ReturnsNotFound()
    {
        var harness = await CreateAsync();

        var result = await harness.Controller.ApprovePendingOrder(Guid.NewGuid(), new PendingOrderApproveRequest("PO-2"), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task ApprovePendingOrder_KnownOrder_ReturnsNoContent()
    {
        var harness = await CreateAsync();

        var result = await harness.Controller.ApprovePendingOrder(harness.PendingContractId, new PendingOrderApproveRequest("PO-2"), CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal("PO-2", harness.PendingOrders.LastPurchaseOrderNumber);
    }

    [Fact]
    public async Task ApprovePendingOrder_NoBody_UsesEmptyPurchaseOrderNumber()
    {
        var harness = await CreateAsync();

        var result = await harness.Controller.ApprovePendingOrder(harness.PendingContractId, request: null, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal(string.Empty, harness.PendingOrders.LastPurchaseOrderNumber);
    }

    [Fact]
    public async Task ApprovePendingOrder_AuthenticatedUser_RecordsLowercasedUserNameAsApprovedBy()
    {
        var harness = await CreateAsync();
        var identity = new System.Security.Claims.ClaimsIdentity([new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, "ALICE")], "Test");
        harness.Controller.ControllerContext.HttpContext.User = new System.Security.Claims.ClaimsPrincipal(identity);

        var result = await harness.Controller.ApprovePendingOrder(harness.PendingContractId, new PendingOrderApproveRequest("PO-2"), CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task DeclinePendingOrder_UnknownOrder_ReturnsNotFound()
    {
        var harness = await CreateAsync();

        var result = await harness.Controller.DeclinePendingOrder(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task DeclinePendingOrder_KnownOrder_ReturnsNoContent()
    {
        var harness = await CreateAsync();

        var result = await harness.Controller.DeclinePendingOrder(harness.PendingContractId, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.Single(harness.PendingOrders.DeletedSchemeIds);
    }

    private static PendingOrderSchemeUpdateRequest UpdateRequest(bool isRemoved = false) =>
        new(false, false, false, true, false, false, false, false, false, false, false, false, false, isRemoved);
}
