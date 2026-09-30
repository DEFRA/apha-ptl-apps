using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using PTL.Contracts.Contract;
using PTL.Contracts.Lookup;
using PTL.Contracts.Participant;
using PTL.Contracts.Scheme;
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

    private static ContractResponse SampleContract(Guid contractId, Guid customerId, bool isReadOnly = false) => new(
        contractId, customerId, "Sample Laboratories Ltd", "QAL/00001", DateTime.UtcNow.Year, "UT12345",
        string.Empty, "Alice Example", string.Empty, string.Empty, 0, 0, 0, 0, 0, 0, 0, 0,
        DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow, string.Empty, DateTime.UtcNow, true, isReadOnly, "A",
        DateTime.UtcNow, string.Empty, false, false, false, null, null);

    private static ParticipantSchemeResponse SampleParticipantScheme(Guid participantSchemeId, Guid contractId, bool isRemoved = false) => new(
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

    private static SchemeResponse SampleScheme(Guid schemeId, int yearId, bool jan, bool feb) => new(
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

        var result = await controller.Create(model, CancellationToken.None);

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

        var result = await controller.Create(model, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(model, view.Model);
        Assert.False(controller.ModelState.IsValid);
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

        var result = await controller.Create(model, CancellationToken.None);

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

        var result = await controller.Edit(Guid.NewGuid(), model, CancellationToken.None);

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

        var result = await controller.Edit(Guid.NewGuid(), model, CancellationToken.None);

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

        var result = await controller.Edit(participantSchemeId, model, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Details", redirect.ActionName);
        Assert.Equal(participantSchemeId, redirect.RouteValues!["id"]);
    }
}
