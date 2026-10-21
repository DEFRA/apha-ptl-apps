using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using PTL.Contracts.Contract;
using PTL.Contracts.Lookup;
using PTL.Contracts.Participant;
using PTL.Contracts.Scheme;
using PTL.InternalWeb.Confirmation;
using PTL.InternalWeb.Features.Account;
using PTL.InternalWeb.Features.ParticipantScheme;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Features.ParticipantScheme;

public class ParticipantSchemeControllerTests
{
    private static ParticipantSchemeController CreateController(
        FakeParticipantSchemeApiClient? participantSchemeApiClient = null,
        FakeContractApiClient? contractApiClient = null,
        FakeParticipantApiClient? participantApiClient = null,
        FakeSchemeApiClient? schemeApiClient = null,
        FakeLookupApiClient? lookupApiClient = null) =>
        new(
            participantSchemeApiClient ?? new FakeParticipantSchemeApiClient(),
            contractApiClient ?? new FakeContractApiClient(),
            participantApiClient ?? new FakeParticipantApiClient(),
            schemeApiClient ?? new FakeSchemeApiClient(),
            lookupApiClient ?? new FakeLookupApiClient(),
            NullLogger<ParticipantSchemeController>.Instance);

    internal static ContractResponse SampleContract(Guid contractId, Guid customerId, bool isReadOnly = false) => new(
        contractId, customerId, "Sample Laboratories Ltd", "QAL/00001", DateTime.UtcNow.Year, "UT12345",
        string.Empty, "Alice Example", string.Empty, string.Empty, 0, 0, 0, 0, 0, 0, 0, 0,
        DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow, string.Empty, DateTime.UtcNow, true, isReadOnly, "A",
        DateTime.UtcNow, string.Empty, false, false, false, null, null);

    internal static ParticipantSchemeResponse SampleParticipantScheme(Guid participantSchemeId, Guid contractId, bool isRemoved = false) => new(
        ParticipantSchemeId: participantSchemeId,
        ContractId: contractId,
        ParticipantId: Guid.NewGuid(),
        SchemeId: Guid.NewGuid(),
        DistributionMonthJan: true, DistributionMonthFeb: false, DistributionMonthMar: false, DistributionMonthApr: false,
        DistributionMonthMay: false, DistributionMonthJun: false, DistributionMonthJul: false, DistributionMonthAug: false,
        DistributionMonthSep: false, DistributionMonthOct: false, DistributionMonthNov: false, DistributionMonthDec: false,
        CanEditJan: true, CanEditFeb: true, CanEditMar: true, CanEditApr: true, CanEditMay: true, CanEditJun: true,
        CanEditJul: true, CanEditAug: true, CanEditSep: true, CanEditOct: true, CanEditNov: true, CanEditDec: true,
        NumberOfSetsRequired: 1,
        ExternalReference: null,
        Contact: null,
        IsRemoved: isRemoved,
        ImportExportLicenceRequired: false,
        CustomsCertificateRequired: false,
        NonFeePaying: false,
        PackingInstructions: null,
        IsWeightedPricing: false,
        DataConsentDeclarationGiven: false,
        IsOverrideJan: false, IsOverrideFeb: false, IsOverrideMar: false, IsOverrideApr: false, IsOverrideMay: false,
        IsOverrideJun: false, IsOverrideJul: false, IsOverrideAug: false, IsOverrideSep: false, IsOverrideOct: false,
        IsOverrideNov: false, IsOverrideDec: false,
        Price: 42.5m,
        ParticipantDisplayName: "Lab One: LAB1",
        SchemeDisplayName: "S1: Salmonella");

    [Fact]
    public async Task Details_UnknownParticipantScheme_ReturnsNotFound()
    {
        var controller = CreateController();

        var result = await controller.Details(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Details_UnknownContract_ReturnsNotFound()
    {
        var participantSchemeId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        var participantSchemeApiClient = new FakeParticipantSchemeApiClient { ParticipantSchemeResponse = SampleParticipantScheme(participantSchemeId, contractId) };
        var controller = CreateController(participantSchemeApiClient, new FakeContractApiClient { ContractResponse = null });

        var result = await controller.Details(participantSchemeId, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Details_Found_ReturnsViewWithReadOnlyFields()
    {
        var participantSchemeId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var participantSchemeApiClient = new FakeParticipantSchemeApiClient { ParticipantSchemeResponse = SampleParticipantScheme(participantSchemeId, contractId) };
        var contractApiClient = new FakeContractApiClient { ContractResponse = SampleContract(contractId, customerId) };
        var controller = CreateController(participantSchemeApiClient, contractApiClient);

        var result = await controller.Details(participantSchemeId, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ParticipantSchemeDetailsViewModel>(view.Model);
        Assert.True(model.Fields.IsReadOnly);
        Assert.Equal(contractId, model.Fields.ContractId);
    }

    [Fact]
    public async Task Details_RemovedParticipantScheme_MarksModelAsReadOnly()
    {
        var participantSchemeId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        var participantSchemeApiClient = new FakeParticipantSchemeApiClient { ParticipantSchemeResponse = SampleParticipantScheme(participantSchemeId, contractId, isRemoved: true) };
        var contractApiClient = new FakeContractApiClient { ContractResponse = SampleContract(contractId, Guid.NewGuid()) };
        var controller = CreateController(participantSchemeApiClient, contractApiClient);

        var result = await controller.Details(participantSchemeId, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ParticipantSchemeDetailsViewModel>(view.Model);
        Assert.True(model.IsReadOnly);
    }

    [Fact]
    public async Task Details_SchemeMatchesParticipantSelection_OffersFullSchemePricingOnly()
    {
        var participantSchemeId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        var yearId = DateTime.UtcNow.Year;
        var schemeId = Guid.NewGuid();
        var participantSchemeApiClient = new FakeParticipantSchemeApiClient { ParticipantSchemeResponse = SampleParticipantScheme(participantSchemeId, contractId) with { SchemeId = schemeId } };
        var contractApiClient = new FakeContractApiClient { ContractResponse = SampleContract(contractId, Guid.NewGuid()) with { YearId = yearId } };
        var schemeApiClient = new FakeSchemeApiClient { SchemeResponse = SampleScheme(schemeId, yearId, jan: true, feb: false) };
        var lookupApiClient = new FakeLookupApiClient { WeightedPricingYears = [new YearResponse(yearId, $"{yearId}/{yearId + 1}")] };
        var controller = CreateController(participantSchemeApiClient, contractApiClient, schemeApiClient: schemeApiClient, lookupApiClient: lookupApiClient);

        var result = await controller.Details(participantSchemeId, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ParticipantSchemeDetailsViewModel>(view.Model);
        Assert.Equal("Full", model.Fields.PricingPlan);
        Assert.Single(model.Fields.PricingPlanOptions);
    }

    [Fact]
    public async Task Details_PartialSelection_OffersWeightedAndProRataOptions()
    {
        var participantSchemeId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        var yearId = DateTime.UtcNow.Year;
        var schemeId = Guid.NewGuid();
        var participantSchemeApiClient = new FakeParticipantSchemeApiClient
        {
            ParticipantSchemeResponse = SampleParticipantScheme(participantSchemeId, contractId) with { SchemeId = schemeId, IsWeightedPricing = true }
        };
        var contractApiClient = new FakeContractApiClient { ContractResponse = SampleContract(contractId, Guid.NewGuid()) with { YearId = yearId } };
        var schemeApiClient = new FakeSchemeApiClient { SchemeResponse = SampleScheme(schemeId, yearId, jan: true, feb: true) };
        var lookupApiClient = new FakeLookupApiClient { WeightedPricingYears = [new YearResponse(yearId, $"{yearId}/{yearId + 1}")] };
        var controller = CreateController(participantSchemeApiClient, contractApiClient, schemeApiClient: schemeApiClient, lookupApiClient: lookupApiClient);

        var result = await controller.Details(participantSchemeId, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ParticipantSchemeDetailsViewModel>(view.Model);
        Assert.Equal(2, model.Fields.PricingPlanOptions.Count());
        Assert.Equal("Weighted", model.Fields.PricingPlan);
    }

    [Fact]
    public async Task Details_NoWeightedPricingForYear_ForcesProRataAndHidesOptions()
    {
        var participantSchemeId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        var participantSchemeApiClient = new FakeParticipantSchemeApiClient { ParticipantSchemeResponse = SampleParticipantScheme(participantSchemeId, contractId) };
        var contractApiClient = new FakeContractApiClient { ContractResponse = SampleContract(contractId, Guid.NewGuid()) };
        var controller = CreateController(participantSchemeApiClient, contractApiClient);

        var result = await controller.Details(participantSchemeId, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ParticipantSchemeDetailsViewModel>(view.Model);
        Assert.Equal("ProRata", model.Fields.PricingPlan);
        Assert.Empty(model.Fields.PricingPlanOptions);
        Assert.False(model.Fields.IsWeightedSchemeAvailable);
    }

    internal static SchemeResponse SampleScheme(Guid schemeId, int yearId, bool jan, bool feb) => new(
        schemeId, Guid.NewGuid(), yearId, "S1", "Salmonella", Guid.NewGuid(), Guid.NewGuid(), null,
        DistributionMonthApr: false, DistributionMonthMay: false, DistributionMonthJun: false, DistributionMonthJul: false,
        DistributionMonthAug: false, DistributionMonthSep: false, DistributionAsAvailable: false, DistributionMonthOct: false,
        DistributionMonthNov: false, DistributionMonthDec: false, DistributionMonthJan: jan, DistributionMonthFeb: feb,
        DistributionMonthMar: false, WeekNumber: 0, DayOfWeekId: Guid.NewGuid(), NumberOfSamples: 1, SampleNoSequence: null,
        SampleOrigin: string.Empty, Deadline: 0, Subcontractor: string.Empty, CombinedPackaging: false, Postage: null,
        CustomsVolume: null, SamplePackingInstructions: string.Empty, RequiresAssessment: false, CommentsRequired: false,
        Pilot: false, LimitedSampleAvailability: false, Accredited: false, NoVLALabs: false, ComerciallyAvailable: false,
        CustomsDescription: null, DataConsentDeclarationActive: false, DataConsentDeclarationText: null, Instructions: string.Empty,
        DateOfReceipt: false, StorageConditions: false, ConditionOnReceipt: false, TestConsultant1: null, TestConsultant2: null,
        TestConsultant3: null, TestConsultantTabulationId: null, UseExternalReference: false, StoreRatings: false,
        Assessor1: null, Assessor2: null, Assessor3: null, Assessor4: null, StandardTabulationText: null,
        LastModified: DateTime.UtcNow, IsReadOnly: false);

    [Fact]
    public async Task Create_Get_UnknownContract_ReturnsNotFound()
    {
        var controller = CreateController(contractApiClient: new FakeContractApiClient { ContractResponse = null });

        var result = await controller.Create(Guid.NewGuid(), Guid.NewGuid());

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Create_Get_ExistingContract_ReturnsViewWithPopulatedOptions()
    {
        var contractId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var contractApiClient = new FakeContractApiClient { ContractResponse = SampleContract(contractId, customerId) };
        var controller = CreateController(contractApiClient: contractApiClient);

        var result = await controller.Create(contractId, customerId);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ParticipantSchemeFormViewModel>(view.Model);
        Assert.Equal(contractId, model.ContractId);
        Assert.Equal(customerId, model.CustomerId);
    }

    [Fact]
    public async Task Create_Post_MissingParticipantAndScheme_ReturnsViewWithModelErrors()
    {
        var controller = CreateController();
        var model = new ParticipantSchemeFormViewModel { ContractId = Guid.NewGuid(), CustomerId = Guid.NewGuid() };

        var result = await controller.Create(model, cancellationToken: CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(model, view.Model);
        Assert.False(controller.ModelState.IsValid);
        Assert.True(controller.ModelState.ContainsKey(nameof(model.ParticipantId)));
        Assert.True(controller.ModelState.ContainsKey(nameof(model.SchemeId)));
    }

    [Fact]
    public async Task Create_Post_ApiFailure_AddsFieldErrorsAndReturnsView()
    {
        var participantSchemeApiClient = new FakeParticipantSchemeApiClient
        {
            SaveResult = new ParticipantSchemeSaveResult(false, null, new Dictionary<string, string[]> { ["SchemeId"] = ["This participant is already on this scheme"] })
        };
        var controller = CreateController(participantSchemeApiClient);
        var model = new ParticipantSchemeFormViewModel { ContractId = Guid.NewGuid(), CustomerId = Guid.NewGuid(), ParticipantId = Guid.NewGuid(), SchemeId = Guid.NewGuid() };

        var result = await controller.Create(model, cancellationToken: CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(model, view.Model);
        Assert.False(controller.ModelState.IsValid);
    }

    [Fact]
    public async Task Create_Post_ApiFailure_WithKnownScheme_PopulatesSchemeDisplayNameAndDataConsent()
    {
        var schemeId = Guid.NewGuid();
        var yearId = DateTime.UtcNow.Year;
        var participantSchemeApiClient = new FakeParticipantSchemeApiClient
        {
            SaveResult = new ParticipantSchemeSaveResult(false, null, new Dictionary<string, string[]> { ["SchemeId"] = ["This participant is already on this scheme"] })
        };
        var schemeApiClient = new FakeSchemeApiClient { SchemeResponse = SampleScheme(schemeId, yearId, jan: true, feb: false) };
        var controller = CreateController(participantSchemeApiClient, schemeApiClient: schemeApiClient);
        var model = new ParticipantSchemeFormViewModel { ContractId = Guid.NewGuid(), CustomerId = Guid.NewGuid(), ParticipantId = Guid.NewGuid(), SchemeId = schemeId };

        var result = await controller.Create(model, cancellationToken: CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var viewModel = Assert.IsType<ParticipantSchemeFormViewModel>(view.Model);
        Assert.Equal("S1: Salmonella", viewModel.SchemeDisplayName);
    }

    [Fact]
    public async Task Create_Post_Success_RedirectsToContractItems()
    {
        var contractId = Guid.NewGuid();
        var participantSchemeId = Guid.NewGuid();
        var participantSchemeApiClient = new FakeParticipantSchemeApiClient
        {
            SaveResult = new ParticipantSchemeSaveResult(true, SampleParticipantScheme(participantSchemeId, contractId), new Dictionary<string, string[]>())
        };
        var controller = CreateController(participantSchemeApiClient);
        var model = new ParticipantSchemeFormViewModel { ContractId = contractId, CustomerId = Guid.NewGuid(), ParticipantId = Guid.NewGuid(), SchemeId = Guid.NewGuid() };

        var result = await controller.Create(model, cancellationToken: CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("ContractItems", redirect.ActionName);
        Assert.Equal("Contract", redirect.ControllerName);
        Assert.Equal(contractId, redirect.RouteValues!["id"]);
    }

    [Fact]
    public async Task Edit_Get_UnknownParticipantScheme_ReturnsNotFound()
    {
        var controller = CreateController();

        var result = await controller.Edit(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Edit_Get_UnknownContract_ReturnsNotFound()
    {
        var participantSchemeId = Guid.NewGuid();
        var participantSchemeApiClient = new FakeParticipantSchemeApiClient { ParticipantSchemeResponse = SampleParticipantScheme(participantSchemeId, Guid.NewGuid()) };
        var controller = CreateController(participantSchemeApiClient, new FakeContractApiClient { ContractResponse = null });

        var result = await controller.Edit(participantSchemeId, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Edit_Get_Found_ReturnsViewWithModel()
    {
        var participantSchemeId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        var participantSchemeApiClient = new FakeParticipantSchemeApiClient { ParticipantSchemeResponse = SampleParticipantScheme(participantSchemeId, contractId) };
        var contractApiClient = new FakeContractApiClient { ContractResponse = SampleContract(contractId, Guid.NewGuid()) };
        var controller = CreateController(participantSchemeApiClient, contractApiClient);

        var result = await controller.Edit(participantSchemeId, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ParticipantSchemeFormViewModel>(view.Model);
        Assert.Equal(participantSchemeId, model.ParticipantSchemeId);
    }

    [Fact]
    public async Task Edit_Post_InvalidModelState_ReturnsViewWithModel()
    {
        var controller = CreateController();
        controller.ModelState.AddModelError("NumberOfSetsRequired", "Enter the number of sets required");
        var model = new ParticipantSchemeFormViewModel { ContractId = Guid.NewGuid() };

        var result = await controller.Edit(Guid.NewGuid(), model, cancellationToken: CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(model, view.Model);
    }

    [Fact]
    public async Task Edit_Post_ApiFailure_AddsFieldErrorsAndReturnsView()
    {
        var participantSchemeApiClient = new FakeParticipantSchemeApiClient
        {
            SaveResult = new ParticipantSchemeSaveResult(false, null, new Dictionary<string, string[]> { [string.Empty] = ["Participant scheme was not found."] })
        };
        var controller = CreateController(participantSchemeApiClient);
        var model = new ParticipantSchemeFormViewModel { ContractId = Guid.NewGuid() };

        var result = await controller.Edit(Guid.NewGuid(), model, cancellationToken: CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(model, view.Model);
        Assert.False(controller.ModelState.IsValid);
    }

    [Fact]
    public async Task Edit_Post_Success_RedirectsToDetails()
    {
        var participantSchemeId = Guid.NewGuid();
        var participantSchemeApiClient = new FakeParticipantSchemeApiClient
        {
            SaveResult = new ParticipantSchemeSaveResult(true, SampleParticipantScheme(participantSchemeId, Guid.NewGuid()), new Dictionary<string, string[]>())
        };
        var controller = CreateController(participantSchemeApiClient);
        var model = new ParticipantSchemeFormViewModel { ContractId = Guid.NewGuid() };

        var result = await controller.Edit(participantSchemeId, model, cancellationToken: CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Details", redirect.ActionName);
        Assert.Equal(participantSchemeId, redirect.RouteValues!["id"]);
    }

    [Fact]
    public async Task Create_Post_SaveThrows_AddsGenericErrorAndReturnsView()
    {
        var participantSchemeApiClient = new FakeParticipantSchemeApiClient { ExceptionToThrow = new InvalidOperationException("boom") };
        var controller = CreateController(participantSchemeApiClient);
        var model = new ParticipantSchemeFormViewModel { ContractId = Guid.NewGuid(), CustomerId = Guid.NewGuid(), ParticipantId = Guid.NewGuid(), SchemeId = Guid.NewGuid() };

        var result = await controller.Create(model, cancellationToken: CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(model, view.Model);
        Assert.Contains("The contract item could not be saved. Please try again.", AllErrorMessages(controller));
    }

    [Fact]
    public async Task Edit_Post_SaveThrows_AddsGenericErrorAndReturnsView()
    {
        var participantSchemeApiClient = new FakeParticipantSchemeApiClient { ExceptionToThrow = new InvalidOperationException("boom") };
        var controller = CreateController(participantSchemeApiClient);
        var model = new ParticipantSchemeFormViewModel { ContractId = Guid.NewGuid() };

        var result = await controller.Edit(Guid.NewGuid(), model, cancellationToken: CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(model, view.Model);
        Assert.Contains("The contract item could not be saved. Please try again.", AllErrorMessages(controller));
    }

    [Fact]
    public async Task Create_Get_BuildsParticipantOptionsFromCustomerParticipants()
    {
        var contractId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var participantApiClient = new FakeParticipantApiClient
        {
            SearchResponse = new ParticipantSearchResponse(
                [new ParticipantSummaryResponse(participantId, customerId, "LAB1", "Lab One", "Bob Example", true)], 1, 1, 20)
        };
        var contractApiClient = new FakeContractApiClient { ContractResponse = SampleContract(contractId, customerId) };
        var controller = CreateController(contractApiClient: contractApiClient, participantApiClient: participantApiClient);

        var result = await controller.Create(contractId, customerId);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ParticipantSchemeFormViewModel>(view.Model);
        var option = Assert.Single(model.ParticipantOptions);
        Assert.Equal("LAB1: Lab One", option.Text);
        Assert.Equal(participantId.ToString(), option.Value);
    }

    [Fact]
    public async Task Create_Get_BuildsGroupAddressOptionsWithResolvedCountryName()
    {
        var contractId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var groupAddressId = Guid.NewGuid();
        var countryId = Guid.NewGuid();
        var contractApiClient = new FakeContractApiClient { ContractResponse = SampleContract(contractId, customerId) };
        var lookupApiClient = new FakeLookupApiClient
        {
            GroupAddresses = [new GroupAddressResponse(groupAddressId, "GA1", "1 Group Street", countryId)],
            Countries = [new CountryResponse(countryId, "United Kingdom")]
        };
        var controller = CreateController(contractApiClient: contractApiClient, lookupApiClient: lookupApiClient);

        var result = await controller.Create(contractId, customerId);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ParticipantSchemeFormViewModel>(view.Model);
        var option = Assert.Single(model.GroupAddressOptions);
        Assert.Equal("GA1", option.Identifier);
        Assert.Equal("1 Group Street", option.Address1);
        Assert.Equal("United Kingdom", option.CountryName);

        // Nothing selected on a fresh Create, so the read-only display fields stay empty.
        Assert.Null(model.GroupAddressIdentifier);
        Assert.Null(model.GroupAddressAddress1);
        Assert.Null(model.GroupAddressCountry);
    }

    [Fact]
    public async Task Edit_Get_SelectedGroupAddress_PopulatesDisplayFields()
    {
        var participantSchemeId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        var groupAddressId = Guid.NewGuid();
        var countryId = Guid.NewGuid();
        var participantSchemeApiClient = new FakeParticipantSchemeApiClient
        {
            ParticipantSchemeResponse = SampleParticipantScheme(participantSchemeId, contractId) with { GroupAddressId = groupAddressId }
        };
        var contractApiClient = new FakeContractApiClient { ContractResponse = SampleContract(contractId, Guid.NewGuid()) };
        var lookupApiClient = new FakeLookupApiClient
        {
            GroupAddresses = [new GroupAddressResponse(groupAddressId, "GA1", "1 Group Street", countryId)],
            Countries = [new CountryResponse(countryId, "United Kingdom")]
        };
        var controller = CreateController(participantSchemeApiClient, contractApiClient, lookupApiClient: lookupApiClient);

        var result = await controller.Edit(participantSchemeId, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ParticipantSchemeFormViewModel>(view.Model);
        Assert.Equal("GA1", model.GroupAddressIdentifier);
        Assert.Equal("1 Group Street", model.GroupAddressAddress1);
        Assert.Equal("United Kingdom", model.GroupAddressCountry);
    }

    [Fact]
    public async Task Create_Get_EverySchemeMonthAvailable_OffersFullSchemePricingOnly()
    {
        var contractId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var schemeId = Guid.NewGuid();
        var yearId = DateTime.UtcNow.Year;
        var contractApiClient = new FakeContractApiClient { ContractResponse = SampleContract(contractId, customerId) with { YearId = yearId } };
        var schemeApiClient = new FakeSchemeApiClient { SchemeResponse = SampleScheme(schemeId, yearId, jan: true, feb: true) };
        var lookupApiClient = new FakeLookupApiClient { WeightedPricingYears = [new YearResponse(yearId, $"{yearId}/{yearId + 1}")] };
        var controller = CreateController(contractApiClient: contractApiClient, schemeApiClient: schemeApiClient, lookupApiClient: lookupApiClient);

        var result = await controller.Create(contractId, customerId, participantId: Guid.NewGuid(), schemeId: schemeId);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ParticipantSchemeFormViewModel>(view.Model);
        Assert.True(model.IsWeightedSchemeAvailable);
        Assert.Equal("Full", model.PricingPlan);
        Assert.Single(model.PricingPlanOptions);
    }

    [Fact]
    public async Task Details_ExistingItemNotWeightedPriced_DefaultsToProRataPricingPlan()
    {
        var participantSchemeId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        var schemeId = Guid.NewGuid();
        var yearId = DateTime.UtcNow.Year;
        var participantSchemeApiClient = new FakeParticipantSchemeApiClient
        {
            ParticipantSchemeResponse = SampleParticipantScheme(participantSchemeId, contractId) with { SchemeId = schemeId, IsWeightedPricing = false }
        };
        var contractApiClient = new FakeContractApiClient { ContractResponse = SampleContract(contractId, Guid.NewGuid()) with { YearId = yearId } };
        var schemeApiClient = new FakeSchemeApiClient { SchemeResponse = SampleScheme(schemeId, yearId, jan: true, feb: true) };
        var lookupApiClient = new FakeLookupApiClient { WeightedPricingYears = [new YearResponse(yearId, $"{yearId}/{yearId + 1}")] };
        var controller = CreateController(participantSchemeApiClient, contractApiClient, schemeApiClient: schemeApiClient, lookupApiClient: lookupApiClient);

        var result = await controller.Details(participantSchemeId, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ParticipantSchemeDetailsViewModel>(view.Model);
        Assert.Equal("ProRata", model.Fields.PricingPlan);
    }

    [Fact]
    public async Task Create_Get_WithoutSchemeId_PopulatesSchemeSearchOptions()
    {
        var contractId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var yearId = DateTime.UtcNow.Year;
        var contractApiClient = new FakeContractApiClient { ContractResponse = SampleContract(contractId, customerId) with { YearId = yearId } };
        var schemeApiClient = new FakeSchemeApiClient
        {
            SearchResponse = new SchemeSearchResponse(
                [new SchemeSummaryResponse(Guid.NewGuid(), yearId, Guid.NewGuid(), "S1", "Salmonella", null, null, null, null, null, null)], 1, 1, 25)
        };
        var controller = CreateController(contractApiClient: contractApiClient, schemeApiClient: schemeApiClient);

        var result = await controller.Create(contractId, customerId, schemeSearchTerm: "sal");

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ParticipantSchemeFormViewModel>(view.Model);
        Assert.Single(model.SchemeOptions);
        Assert.Equal(1, model.SchemeOptionsTotalCount);

        var pagination = model.SchemePagination;
        Assert.Equal(1, pagination.CurrentPage);
        Assert.Equal(1, pagination.TotalRecords);
        Assert.Equal("Create", pagination.Action);
        Assert.Equal(contractId, pagination.RouteValues["contractId"]);
        Assert.Equal("sal", pagination.RouteValues["schemeSearchTerm"]);
    }

    private static SchemeMonthEditabilityResponse MonthsDefined(bool jan = true, bool feb = true) =>
        new(jan, feb, true, true, true, true, true, true, true, true, true, true);

    [Fact]
    public async Task Create_Get_WithScheme_TicksEveryOpenSchemeMonthAndLocksThePostedOnes()
    {
        var contractId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var schemeId = Guid.NewGuid();
        var yearId = DateTime.UtcNow.Year;
        var participantSchemeApiClient = new FakeParticipantSchemeApiClient
        {
            Context = new ParticipantSchemeContextResponse([true, false, true, true, true, true, true, true, true, true, true, true], [4, 5])
        };
        var contractApiClient = new FakeContractApiClient { ContractResponse = SampleContract(contractId, customerId) with { YearId = yearId } };
        var schemeApiClient = new FakeSchemeApiClient { SchemeResponse = SampleScheme(schemeId, yearId, jan: true, feb: true) };
        var controller = CreateController(participantSchemeApiClient, contractApiClient, schemeApiClient: schemeApiClient);

        var result = await controller.Create(contractId, customerId, participantId: Guid.NewGuid(), schemeId: schemeId);

        var model = Assert.IsType<ParticipantSchemeFormViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.True(model.DistributionMonthJan);
        Assert.False(model.DistributionMonthFeb);
        Assert.False(model.CanEditFeb);
        Assert.False(model.DistributionMonthMar);
        Assert.Equal([4, 5], model.InitialisedMonthIds);
    }

    [Fact]
    public async Task Create_Post_RedisplayKeepsThePostedMonthsInsteadOfReapplyingDefaults()
    {
        var participantSchemeApiClient = new FakeParticipantSchemeApiClient
        {
            SaveResult = new ParticipantSchemeSaveResult(false, null, new Dictionary<string, string[]> { ["SchemeId"] = ["x"] })
        };
        var schemeId = Guid.NewGuid();
        var yearId = DateTime.UtcNow.Year;
        var schemeApiClient = new FakeSchemeApiClient { SchemeResponse = SampleScheme(schemeId, yearId, jan: true, feb: true) };
        var controller = CreateController(participantSchemeApiClient, schemeApiClient: schemeApiClient);
        var model = new ParticipantSchemeFormViewModel { ContractId = Guid.NewGuid(), CustomerId = Guid.NewGuid(), ParticipantId = Guid.NewGuid(), SchemeId = schemeId, DistributionMonthFeb = true };

        await controller.Create(model, cancellationToken: CancellationToken.None);

        Assert.False(model.DistributionMonthJan);
        Assert.True(model.DistributionMonthFeb);
    }

    [Fact]
    public async Task Create_Get_SchemeWithMonthsAlreadyDefined_OffersWeightedAndProRataDefaultingToWeighted()
    {
        // numOnScheme (2) differs from numAvailableOnScheme (1) and a new item starts with no months.
        var contractId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var schemeId = Guid.NewGuid();
        var yearId = DateTime.UtcNow.Year;
        var contractApiClient = new FakeContractApiClient { ContractResponse = SampleContract(contractId, customerId) with { YearId = yearId } };
        var schemeApiClient = new FakeSchemeApiClient { SchemeResponse = SampleScheme(schemeId, yearId, jan: true, feb: true) with { CanEdit = MonthsDefined(feb: false) } };
        var lookupApiClient = new FakeLookupApiClient { WeightedPricingYears = [new YearResponse(yearId, $"{yearId}/{yearId + 1}")] };
        var controller = CreateController(contractApiClient: contractApiClient, schemeApiClient: schemeApiClient, lookupApiClient: lookupApiClient);

        var result = await controller.Create(contractId, customerId, participantId: Guid.NewGuid(), schemeId: schemeId);

        var model = Assert.IsType<ParticipantSchemeFormViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(2, model.NumberOnScheme);
        Assert.Equal(1, model.NumberAvailableOnScheme);
        Assert.Equal(["Weighted", "ProRata"], model.PricingPlanOptions.Select(o => o.Value));
        Assert.Equal("Weighted", model.PricingPlan);
    }

    [Fact]
    public async Task Create_Post_Redisplay_KeepsTheChosenProRataPlan()
    {
        var participantSchemeApiClient = new FakeParticipantSchemeApiClient
        {
            SaveResult = new ParticipantSchemeSaveResult(false, null, new Dictionary<string, string[]> { ["SchemeId"] = ["x"] })
        };
        var schemeId = Guid.NewGuid();
        var yearId = DateTime.UtcNow.Year;
        var contractApiClient = new FakeContractApiClient { ContractResponse = SampleContract(Guid.NewGuid(), Guid.NewGuid()) with { YearId = yearId } };
        var schemeApiClient = new FakeSchemeApiClient { SchemeResponse = SampleScheme(schemeId, yearId, jan: true, feb: true) };
        var lookupApiClient = new FakeLookupApiClient { WeightedPricingYears = [new YearResponse(yearId, $"{yearId}/{yearId + 1}")] };
        var controller = CreateController(participantSchemeApiClient, contractApiClient, schemeApiClient: schemeApiClient, lookupApiClient: lookupApiClient);
        var model = new ParticipantSchemeFormViewModel
        {
            ContractId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            YearId = yearId,
            ParticipantId = Guid.NewGuid(),
            SchemeId = schemeId,
            DistributionMonthJan = true,
            PricingPlan = "ProRata"
        };

        await controller.Create(model, cancellationToken: CancellationToken.None);

        Assert.Equal("ProRata", model.PricingPlan);
        Assert.Equal(2, model.PricingPlanOptions.Count());
    }

    [Fact]
    public async Task Create_Post_ConsentRequiredButNotGiven_AddsTheLegacyErrorAndDoesNotSave()
    {
        var participantSchemeApiClient = new FakeParticipantSchemeApiClient();
        var schemeId = Guid.NewGuid();
        var schemeApiClient = new FakeSchemeApiClient { SchemeResponse = SampleScheme(schemeId, DateTime.UtcNow.Year, jan: true, feb: false) with { DataConsentDeclarationActive = true } };
        var controller = CreateController(participantSchemeApiClient, schemeApiClient: schemeApiClient);
        var model = new ParticipantSchemeFormViewModel { ContractId = Guid.NewGuid(), CustomerId = Guid.NewGuid(), ParticipantId = Guid.NewGuid(), SchemeId = schemeId, DistributionMonthJan = true };

        var result = await controller.Create(model, cancellationToken: CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.False(participantSchemeApiClient.CreateCalled);
        Assert.Contains("Accept 'Data Consent Declaration' before saving", AllErrorMessages(controller));
    }

    [Fact]
    public async Task Create_Post_ConsentRequiredAndGiven_Saves()
    {
        var contractId = Guid.NewGuid();
        var participantSchemeApiClient = new FakeParticipantSchemeApiClient
        {
            SaveResult = new ParticipantSchemeSaveResult(true, SampleParticipantScheme(Guid.NewGuid(), contractId), new Dictionary<string, string[]>())
        };
        var schemeId = Guid.NewGuid();
        var schemeApiClient = new FakeSchemeApiClient { SchemeResponse = SampleScheme(schemeId, DateTime.UtcNow.Year, jan: true, feb: false) with { DataConsentDeclarationActive = true } };
        var controller = CreateController(participantSchemeApiClient, schemeApiClient: schemeApiClient);
        var model = new ParticipantSchemeFormViewModel { ContractId = contractId, CustomerId = Guid.NewGuid(), ParticipantId = Guid.NewGuid(), SchemeId = schemeId, DataConsentDeclarationGiven = true };

        var result = await controller.Create(model, cancellationToken: CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.True(participantSchemeApiClient.CreateCalled);
    }

    [Fact]
    public async Task Edit_Post_ConsentRequiredButNotGiven_AddsTheLegacyErrorAndDoesNotSave()
    {
        var participantSchemeApiClient = new FakeParticipantSchemeApiClient();
        var schemeId = Guid.NewGuid();
        var schemeApiClient = new FakeSchemeApiClient { SchemeResponse = SampleScheme(schemeId, DateTime.UtcNow.Year, jan: true, feb: false) with { DataConsentDeclarationActive = true } };
        var controller = CreateController(participantSchemeApiClient, schemeApiClient: schemeApiClient);
        var model = new ParticipantSchemeFormViewModel { ContractId = Guid.NewGuid(), SchemeId = schemeId };

        await controller.Edit(Guid.NewGuid(), model, cancellationToken: CancellationToken.None);

        Assert.False(participantSchemeApiClient.UpdateCalled);
        Assert.Contains("Accept 'Data Consent Declaration' before saving", AllErrorMessages(controller));
    }

    [Fact]
    public async Task Edit_Post_ApiFailure_RestoresLockedMonthsAndDisplayFlagsFromTheStoredItem()
    {
        var participantSchemeId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        var stored = SampleParticipantScheme(participantSchemeId, contractId) with { CanEditJan = false, DistributionMonthFeb = true };
        var participantSchemeApiClient = new FakeParticipantSchemeApiClient
        {
            ParticipantSchemeResponse = stored,
            SaveResult = new ParticipantSchemeSaveResult(false, null, new Dictionary<string, string[]> { [string.Empty] = ["Something is wrong"] })
        };
        var controller = CreateController(participantSchemeApiClient);
        // Locked months post nothing, so Jan arrives unticked; Feb is editable and was unticked by the user.
        var model = new ParticipantSchemeFormViewModel { ContractId = contractId, CustomerId = Guid.NewGuid(), SchemeId = stored.SchemeId };

        await controller.Edit(participantSchemeId, model, cancellationToken: CancellationToken.None);

        Assert.False(model.CanEditJan);
        Assert.True(model.DistributionMonthJan);
        Assert.False(model.DistributionMonthFeb);
        Assert.Equal("Lab One: LAB1", model.ParticipantDisplayName);
    }

    [Fact]
    public async Task Edit_Post_ApiFailureInOverrideMode_KeepsTheMonthChosenForOverride()
    {
        var participantSchemeId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        var stored = SampleParticipantScheme(participantSchemeId, contractId) with { CanEditFeb = false };
        var participantSchemeApiClient = new FakeParticipantSchemeApiClient
        {
            ParticipantSchemeResponse = stored,
            SaveResult = new ParticipantSchemeSaveResult(false, null, new Dictionary<string, string[]> { [string.Empty] = ["Something is wrong"] })
        };
        var controller = WithRoles(CreateController(participantSchemeApiClient), "Override Schemes");
        var model = new ParticipantSchemeFormViewModel { ContractId = contractId, CustomerId = Guid.NewGuid(), SchemeId = stored.SchemeId, OverrideModeActive = true, DistributionMonthFeb = true };

        await controller.Edit(participantSchemeId, model, cancellationToken: CancellationToken.None);

        Assert.True(model.DistributionMonthFeb);
    }

    [Fact]
    public async Task Edit_Get_ForwardsTheInitialisedMonthsForTheWarning()
    {
        var participantSchemeId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        var participantSchemeApiClient = new FakeParticipantSchemeApiClient
        {
            ParticipantSchemeResponse = SampleParticipantScheme(participantSchemeId, contractId),
            Context = new ParticipantSchemeContextResponse(Enumerable.Repeat(true, 12).ToArray(), [7])
        };
        var contractApiClient = new FakeContractApiClient { ContractResponse = SampleContract(contractId, Guid.NewGuid()) };
        var controller = CreateController(participantSchemeApiClient, contractApiClient);

        var result = await controller.Edit(participantSchemeId, CancellationToken.None);

        var model = Assert.IsType<ParticipantSchemeFormViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal([7], model.InitialisedMonthIds);
    }

    [Fact]
    public async Task Details_SchemeMarkedAsAvailable_FlagsTheModel()
    {
        var participantSchemeId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        var schemeId = Guid.NewGuid();
        var participantSchemeApiClient = new FakeParticipantSchemeApiClient { ParticipantSchemeResponse = SampleParticipantScheme(participantSchemeId, contractId) with { SchemeId = schemeId } };
        var contractApiClient = new FakeContractApiClient { ContractResponse = SampleContract(contractId, Guid.NewGuid()) };
        var schemeApiClient = new FakeSchemeApiClient { SchemeResponse = SampleScheme(schemeId, DateTime.UtcNow.Year, jan: false, feb: false) with { DistributionAsAvailable = true } };
        var controller = CreateController(participantSchemeApiClient, contractApiClient, schemeApiClient: schemeApiClient);

        var result = await controller.Details(participantSchemeId, CancellationToken.None);

        var model = Assert.IsType<ParticipantSchemeDetailsViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.True(model.Fields.SchemeDistributionAsAvailable);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(10, true)]
    [InlineData(11, false)]
    public void SetsOfSamplesRequired_IsRestrictedToOneToTen(int sets, bool valid)
    {
        var model = new ParticipantSchemeFormViewModel { NumberOfSetsRequired = sets };
        var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();

        var isValid = System.ComponentModel.DataAnnotations.Validator.TryValidateProperty(
            model.NumberOfSetsRequired,
            new System.ComponentModel.DataAnnotations.ValidationContext(model) { MemberName = nameof(model.NumberOfSetsRequired) },
            results);

        Assert.Equal(valid, isValid);
    }

    private static IEnumerable<string> AllErrorMessages(ParticipantSchemeController controller) =>
        controller.ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);

    private static ParticipantSchemeController WithRoles(ParticipantSchemeController controller, string? roles)
    {
        var claims = roles is null ? [] : new[] { new Claim(InternalUserClaimTypes.ResolvedRoles, roles) };
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")) } };
        return controller;
    }

    private static FakeParticipantSchemeApiClient ApiClientWithInitialisedMonth(int monthId, Guid participantSchemeId, Guid contractId) => new()
    {
        ParticipantSchemeResponse = SampleParticipantScheme(participantSchemeId, contractId),
        Context = new ParticipantSchemeContextResponse(Enumerable.Repeat(true, 12).ToArray(), [monthId])
    };

    [Theory]
    [InlineData("Override Schemes", true)]
    [InlineData("Admin, override schemes", true)]
    [InlineData("Admin", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public async Task Create_Get_OnlyUsersInTheOverrideSchemesRoleCanOverride(string? roles, bool expected)
    {
        var contractId = Guid.NewGuid();
        var contractApiClient = new FakeContractApiClient { ContractResponse = SampleContract(contractId, Guid.NewGuid()) };
        var controller = WithRoles(CreateController(contractApiClient: contractApiClient), roles);

        var result = await controller.Create(contractId, Guid.NewGuid());

        Assert.Equal(expected, Assert.IsType<ParticipantSchemeFormViewModel>(Assert.IsType<ViewResult>(result).Model).CanOverride);
    }

    [Fact]
    public async Task Edit_Get_UserWithoutTheRole_CannotOverride()
    {
        var participantSchemeId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        var participantSchemeApiClient = new FakeParticipantSchemeApiClient { ParticipantSchemeResponse = SampleParticipantScheme(participantSchemeId, contractId) };
        var contractApiClient = new FakeContractApiClient { ContractResponse = SampleContract(contractId, Guid.NewGuid()) };
        var controller = WithRoles(CreateController(participantSchemeApiClient, contractApiClient), "Admin");

        var result = await controller.Edit(participantSchemeId, CancellationToken.None);

        Assert.False(Assert.IsType<ParticipantSchemeFormViewModel>(Assert.IsType<ViewResult>(result).Model).CanOverride);
    }

    [Fact]
    public async Task Edit_Post_OverrideModePostedByAUserWithoutTheRole_IsIgnored()
    {
        var participantSchemeId = Guid.NewGuid();
        var participantSchemeApiClient = new FakeParticipantSchemeApiClient();
        var controller = WithRoles(CreateController(participantSchemeApiClient), "Admin");
        var model = new ParticipantSchemeFormViewModel { ContractId = Guid.NewGuid(), SchemeId = Guid.NewGuid(), OverrideModeActive = true, DistributionMonthFeb = true };

        await controller.Edit(participantSchemeId, model, cancellationToken: CancellationToken.None);

        Assert.False(model.OverrideModeActive);
        Assert.False(participantSchemeApiClient.LastUpdateRequest!.IsOverrideFeb);
    }

    [Fact]
    public async Task Edit_Post_OverrideModeByAUserInTheRole_FlagsTheTickedMonthsAsOverride()
    {
        var participantSchemeId = Guid.NewGuid();
        var participantSchemeApiClient = new FakeParticipantSchemeApiClient();
        var controller = WithRoles(CreateController(participantSchemeApiClient), "Override Schemes");
        var model = new ParticipantSchemeFormViewModel { ContractId = Guid.NewGuid(), SchemeId = Guid.NewGuid(), OverrideModeActive = true, DistributionMonthFeb = true };

        await controller.Edit(participantSchemeId, model, cancellationToken: CancellationToken.None);

        Assert.True(participantSchemeApiClient.LastUpdateRequest!.IsOverrideFeb);
        Assert.False(participantSchemeApiClient.LastUpdateRequest.IsOverrideJan);
    }

    [Fact]
    public async Task Create_Post_ChangingAnInitialisedMonth_AsksForConfirmationBeforeSaving()
    {
        var participantSchemeApiClient = ApiClientWithInitialisedMonth(4, Guid.NewGuid(), Guid.NewGuid());
        var controller = WithRoles(CreateController(participantSchemeApiClient), null);
        var model = new ParticipantSchemeFormViewModel { ContractId = Guid.NewGuid(), CustomerId = Guid.NewGuid(), ParticipantId = Guid.NewGuid(), SchemeId = Guid.NewGuid(), DistributionMonthApr = true };

        var result = await controller.Create(model, cancellationToken: CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal(ConfirmationFlow.ViewName, view.ViewName);
        var page = Assert.IsType<ConfirmationPageViewModel>(view.Model);
        Assert.Contains("already been initialised", page.Message);
        Assert.Contains("April", page.Message);
        Assert.False(participantSchemeApiClient.CreateCalled);
    }

    [Fact]
    public async Task Create_Post_ConfirmedInitialisedMonthChange_Saves()
    {
        var participantSchemeApiClient = ApiClientWithInitialisedMonth(4, Guid.NewGuid(), Guid.NewGuid());
        participantSchemeApiClient.SaveResult = new ParticipantSchemeSaveResult(true, SampleParticipantScheme(Guid.NewGuid(), Guid.NewGuid()), new Dictionary<string, string[]>());
        var controller = WithRoles(CreateController(participantSchemeApiClient), null);
        var model = new ParticipantSchemeFormViewModel { ContractId = Guid.NewGuid(), CustomerId = Guid.NewGuid(), ParticipantId = Guid.NewGuid(), SchemeId = Guid.NewGuid(), DistributionMonthApr = true };

        var result = await controller.Create(model, ConfirmationFlow.Confirmed, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.True(participantSchemeApiClient.CreateCalled);
    }

    [Fact]
    public async Task Create_Post_CancelledConfirmation_RedisplaysTheFormWithoutSaving()
    {
        var participantSchemeApiClient = ApiClientWithInitialisedMonth(4, Guid.NewGuid(), Guid.NewGuid());
        var controller = WithRoles(CreateController(participantSchemeApiClient), null);
        controller.ModelState.AddModelError("Anything", "stale");
        var model = new ParticipantSchemeFormViewModel { ContractId = Guid.NewGuid(), CustomerId = Guid.NewGuid(), ParticipantId = Guid.NewGuid(), SchemeId = Guid.NewGuid(), DistributionMonthApr = true };

        var result = await controller.Create(model, ConfirmationFlow.Cancelled, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(model, view.Model);
        Assert.True(controller.ModelState.IsValid);
        Assert.False(participantSchemeApiClient.CreateCalled);
    }

    [Fact]
    public async Task Create_Post_InitialisedMonthLeftUnticked_SavesWithoutAsking()
    {
        var participantSchemeApiClient = ApiClientWithInitialisedMonth(4, Guid.NewGuid(), Guid.NewGuid());
        participantSchemeApiClient.SaveResult = new ParticipantSchemeSaveResult(true, SampleParticipantScheme(Guid.NewGuid(), Guid.NewGuid()), new Dictionary<string, string[]>());
        var controller = WithRoles(CreateController(participantSchemeApiClient), null);
        var model = new ParticipantSchemeFormViewModel { ContractId = Guid.NewGuid(), CustomerId = Guid.NewGuid(), ParticipantId = Guid.NewGuid(), SchemeId = Guid.NewGuid(), DistributionMonthJan = true };

        var result = await controller.Create(model, cancellationToken: CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
    }

    [Fact]
    public async Task Edit_Post_ChangingAnInitialisedMonth_AsksForConfirmationBeforeSaving()
    {
        var participantSchemeId = Guid.NewGuid();
        var participantSchemeApiClient = ApiClientWithInitialisedMonth(1, participantSchemeId, Guid.NewGuid());
        var controller = WithRoles(CreateController(participantSchemeApiClient), null);
        // Stored Jan is ticked; the user unticks it.
        var model = new ParticipantSchemeFormViewModel { ContractId = Guid.NewGuid(), CustomerId = Guid.NewGuid(), SchemeId = Guid.NewGuid(), DistributionMonthJan = false, DistributionMonthFeb = true };

        var result = await controller.Edit(participantSchemeId, model, cancellationToken: CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal(ConfirmationFlow.ViewName, view.ViewName);
        Assert.Contains("January", Assert.IsType<ConfirmationPageViewModel>(view.Model).Message);
        Assert.False(participantSchemeApiClient.UpdateCalled);
    }

    [Fact]
    public async Task Edit_Post_InitialisedMonthUnchanged_SavesWithoutAsking()
    {
        var participantSchemeId = Guid.NewGuid();
        var participantSchemeApiClient = ApiClientWithInitialisedMonth(1, participantSchemeId, Guid.NewGuid());
        var controller = WithRoles(CreateController(participantSchemeApiClient), null);
        var model = new ParticipantSchemeFormViewModel { ContractId = Guid.NewGuid(), CustomerId = Guid.NewGuid(), SchemeId = Guid.NewGuid(), DistributionMonthJan = true };

        var result = await controller.Edit(participantSchemeId, model, cancellationToken: CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.True(participantSchemeApiClient.UpdateCalled);
    }

    [Fact]
    public async Task Edit_Post_CancelledConfirmation_RedisplaysTheFormWithoutSaving()
    {
        var participantSchemeId = Guid.NewGuid();
        var participantSchemeApiClient = ApiClientWithInitialisedMonth(1, participantSchemeId, Guid.NewGuid());
        var controller = WithRoles(CreateController(participantSchemeApiClient), null);
        var model = new ParticipantSchemeFormViewModel { ContractId = Guid.NewGuid(), CustomerId = Guid.NewGuid(), SchemeId = Guid.NewGuid(), DistributionMonthJan = false };

        var result = await controller.Edit(participantSchemeId, model, ConfirmationFlow.Cancelled, CancellationToken.None);

        Assert.Same(model, Assert.IsType<ViewResult>(result).Model);
        Assert.False(participantSchemeApiClient.UpdateCalled);
    }

    [Fact]
    public async Task Edit_Post_ConfirmedInitialisedMonthChange_Saves()
    {
        var participantSchemeId = Guid.NewGuid();
        var participantSchemeApiClient = ApiClientWithInitialisedMonth(1, participantSchemeId, Guid.NewGuid());
        var controller = WithRoles(CreateController(participantSchemeApiClient), null);
        var model = new ParticipantSchemeFormViewModel { ContractId = Guid.NewGuid(), CustomerId = Guid.NewGuid(), SchemeId = Guid.NewGuid(), DistributionMonthJan = false, DistributionMonthFeb = true };

        var result = await controller.Edit(participantSchemeId, model, ConfirmationFlow.Confirmed, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.True(participantSchemeApiClient.UpdateCalled);
    }
}
