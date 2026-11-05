using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PTL.Api.Controllers;
using PTL.Api.Tests.Contract;
using PTL.Api.Tests.Customer;
using PTL.Api.Tests.Lookup;
using PTL.Api.Tests.Participant;
using PTL.Api.Tests.Scheme;
using PTL.Contracts.CreateOrder;
using PTL.Core.Contract.CreateOrder;
using PTL.Core.Lookup;
using CoreCustomer = PTL.Core.Customer.Customer;
using CoreParticipant = PTL.Core.Participant.Participant;

namespace PTL.Api.Tests.Endpoints;

public class CreateOrderControllerTests
{
    private sealed record Setup(CreateOrderController Controller, Guid ParticipantId, FakeCreateOrderStore Store, AvailableSchemeEntity Scheme);

    private static async Task<Setup> CreateAsync(bool canOrderOnline = true)
    {
        var store = new FakeCreateOrderStore();
        var participants = new FakeParticipantRepository();
        var customers = new FakeCustomerRepository();
        var lookup = new FakeLookupRepository();

        var currencyId = Guid.NewGuid();
        var customer = await customers.CreateAsync(new CoreCustomer { CanOrderOnline = canOrderOnline, CurrencyId = currencyId });
        var participant = await participants.CreateAsync(new CoreParticipant { CustomerId = customer.CustomerId, LabCode = "LAB001", IsActive = true });

        lookup.SystemSettings = new SystemSettingsEntity { CurrentYearId = 10, NextYearId = 11 };
        lookup.AllYears = [new YearEntity { YearId = 10, Year = "2026/27" }];

        var scheme = new AvailableSchemeEntity { SchemeId = Guid.NewGuid(), Name = "Alpha", Identifier = "PT1", Schedule = "Monthly", ComerciallyAvailable = true, DistributionMonthApr = true };
        store.AvailableSchemes.Add(scheme);
        lookup.SchemeCurrencies = [new SchemeCurrencyEntity { SchemeId = scheme.SchemeId, CurrencyId = currencyId, Price = 100m, CurrencySymbol = "£" }];

        var service = new CreateOrderService(store, store, participants, customers, lookup, new FakeSchemeRepository(), Options.Create(new CreateOrderOptions()));
        return new Setup(new CreateOrderController(service), participant.ParticipantId, store, scheme);
    }

    [Fact]
    public async Task GetEligibility_UnknownParticipant_ReturnsNotFound()
    {
        var setup = await CreateAsync();

        var result = await setup.Controller.GetEligibility(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetEligibility_EligibleParticipant_ReturnsFlags()
    {
        var setup = await CreateAsync();

        var result = await setup.Controller.GetEligibility(setup.ParticipantId, CancellationToken.None);

        var response = Assert.IsType<CreateOrderEligibilityResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(new CreateOrderEligibilityResponse(true, true, true), response);
    }

    [Fact]
    public async Task Get_Eligible_ReturnsMappedOrder()
    {
        var setup = await CreateAsync();

        var result = await setup.Controller.Get(setup.ParticipantId, OrderYear.Current, review: false, CancellationToken.None);

        var response = Assert.IsType<CreateOrderResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal("2026/27", response.Year);
        Assert.Equal("LAB001", response.LabCode);
        var group = Assert.Single(response.AvailableSchemes);
        Assert.Equal("Monthly", group.Schedule);
        var scheme = Assert.Single(group.Schemes);
        Assert.Equal("Alpha", scheme.Name);
        Assert.Equal(100m, scheme.AnnualPrice);
        Assert.Equal(12, scheme.Months.Count);
        Assert.False(response.CanProceed);
    }

    [Fact]
    public async Task Get_NotEligible_ReturnsForbidden()
    {
        var setup = await CreateAsync(canOrderOnline: false);

        var result = await setup.Controller.Get(setup.ParticipantId, OrderYear.Current, review: false, CancellationToken.None);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeResult>(result.Result).StatusCode);
    }

    [Fact]
    public async Task Get_UnknownParticipant_ReturnsNotFound()
    {
        var setup = await CreateAsync();

        var result = await setup.Controller.Get(Guid.NewGuid(), OrderYear.Current, review: false, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Change_SelectScheme_ReturnsOrderWithSchemeSelected()
    {
        var setup = await CreateAsync();

        var result = await setup.Controller.Change(
            setup.ParticipantId, OrderYear.Current, new CreateOrderLineChangeRequest(setup.Scheme.SchemeId, "select", true), CancellationToken.None);

        var response = Assert.IsType<CreateOrderResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        var scheme = response.AvailableSchemes.Single().Schemes.Single();
        Assert.True(scheme.Selected);
        Assert.True(scheme.HasPendingMonths);
        Assert.Equal(FakeCreateOrderStore.PricePerMonth, response.SchemeTotal);
        Assert.True(response.CanProceed);
    }

    [Fact]
    public async Task Change_UnknownField_ReturnsBadRequest()
    {
        var setup = await CreateAsync();

        var result = await setup.Controller.Change(
            setup.ParticipantId, OrderYear.Current, new CreateOrderLineChangeRequest(setup.Scheme.SchemeId, "bogus", true), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Change_NotEligible_ReturnsForbidden()
    {
        var setup = await CreateAsync(canOrderOnline: false);

        var result = await setup.Controller.Change(
            setup.ParticipantId, OrderYear.Current, new CreateOrderLineChangeRequest(setup.Scheme.SchemeId, "select", true), CancellationToken.None);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeResult>(result.Result).StatusCode);
    }

    [Fact]
    public async Task Clear_Eligible_ReturnsNoContent()
    {
        var setup = await CreateAsync();

        Assert.IsType<NoContentResult>(await setup.Controller.Clear(setup.ParticipantId, OrderYear.Current, CancellationToken.None));
    }

    [Fact]
    public async Task Clear_NotEligible_ReturnsForbidden()
    {
        var setup = await CreateAsync(canOrderOnline: false);

        var result = await setup.Controller.Clear(setup.ParticipantId, OrderYear.Current, CancellationToken.None);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeResult>(result).StatusCode);
    }

    [Fact]
    public async Task Clear_UnknownParticipant_ReturnsNotFound()
    {
        var setup = await CreateAsync();

        Assert.IsType<NotFoundResult>(await setup.Controller.Clear(Guid.NewGuid(), OrderYear.Current, CancellationToken.None));
    }

    private static async Task<Setup> CreateWithSelectedSchemeAsync()
    {
        var setup = await CreateAsync();
        await setup.Controller.Change(
            setup.ParticipantId, OrderYear.Current, new CreateOrderLineChangeRequest(setup.Scheme.SchemeId, "select", true), CancellationToken.None);
        return setup;
    }

    [Fact]
    public async Task Get_ForReview_ReturnsThePurchaseOrderAndAConsentList()
    {
        var setup = await CreateWithSelectedSchemeAsync();
        await setup.Controller.SavePurchaseOrder(setup.ParticipantId, OrderYear.Current, new CreateOrderSaveRequest("PO-1"), CancellationToken.None);

        var result = await setup.Controller.Get(setup.ParticipantId, OrderYear.Current, review: true, CancellationToken.None);

        var response = Assert.IsType<CreateOrderResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal("PO-1", response.PurchaseOrderNumber);
        Assert.NotNull(response.Consents);
        Assert.Empty(response.DataConsents);
    }

    [Fact]
    public async Task SavePurchaseOrder_Eligible_ReturnsNoContent()
    {
        var setup = await CreateWithSelectedSchemeAsync();

        var result = await setup.Controller.SavePurchaseOrder(setup.ParticipantId, OrderYear.Current, new CreateOrderSaveRequest("PO-1"), CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal("PO-1", Assert.Single(setup.Store.Orders).PurchaseOrderNumber);
    }

    [Fact]
    public async Task SavePurchaseOrder_TooLong_ReturnsTheFieldErrors()
    {
        var setup = await CreateWithSelectedSchemeAsync();

        var result = await setup.Controller.SavePurchaseOrder(setup.ParticipantId, OrderYear.Current, new CreateOrderSaveRequest(new string('A', 256)), CancellationToken.None);

        var validation = Assert.IsType<CreateOrderValidationResponse>(Assert.IsType<BadRequestObjectResult>(result).Value);
        Assert.Equal("PurchaseOrderNumber", Assert.Single(validation.Errors).Field);
    }

    [Fact]
    public async Task SavePurchaseOrder_NotEligibleOrUnknown_IsRefused()
    {
        var ineligible = await CreateAsync(canOrderOnline: false);
        var known = await CreateAsync();

        var forbidden = await ineligible.Controller.SavePurchaseOrder(ineligible.ParticipantId, OrderYear.Current, new CreateOrderSaveRequest("PO"), CancellationToken.None);
        var missing = await known.Controller.SavePurchaseOrder(Guid.NewGuid(), OrderYear.Current, new CreateOrderSaveRequest("PO"), CancellationToken.None);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeResult>(forbidden).StatusCode);
        Assert.IsType<NotFoundResult>(missing);
    }

    [Fact]
    public async Task Submit_ValidOrder_ReturnsNoContentAndSubmitsIt()
    {
        var setup = await CreateWithSelectedSchemeAsync();

        var result = await setup.Controller.Submit(
            setup.ParticipantId, OrderYear.Current, new CreateOrderSubmitRequest("PO-2", TermsAccepted: true, ConsentedSchemeIds: null), CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        var order = Assert.Single(setup.Store.Orders);
        Assert.True(order.IsSubmitted);
        Assert.Equal("PO-2", order.PurchaseOrderNumber);
    }

    [Fact]
    public async Task Submit_TermsNotAccepted_ReturnsTheFieldErrors()
    {
        var setup = await CreateWithSelectedSchemeAsync();

        var result = await setup.Controller.Submit(
            setup.ParticipantId, OrderYear.Current, new CreateOrderSubmitRequest("PO-2", TermsAccepted: false, ConsentedSchemeIds: []), CancellationToken.None);

        var validation = Assert.IsType<CreateOrderValidationResponse>(Assert.IsType<BadRequestObjectResult>(result).Value);
        Assert.Equal("TermsAccepted", Assert.Single(validation.Errors).Field);
        Assert.False(Assert.Single(setup.Store.Orders).IsSubmitted);
    }

    [Fact]
    public async Task Submit_NotEligibleOrUnknown_IsRefused()
    {
        var ineligible = await CreateAsync(canOrderOnline: false);
        var known = await CreateAsync();
        var request = new CreateOrderSubmitRequest("PO", TermsAccepted: true, ConsentedSchemeIds: []);

        var forbidden = await ineligible.Controller.Submit(ineligible.ParticipantId, OrderYear.Current, request, CancellationToken.None);
        var missing = await known.Controller.Submit(Guid.NewGuid(), OrderYear.Current, request, CancellationToken.None);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeResult>(forbidden).StatusCode);
        Assert.IsType<NotFoundResult>(missing);
    }
}
