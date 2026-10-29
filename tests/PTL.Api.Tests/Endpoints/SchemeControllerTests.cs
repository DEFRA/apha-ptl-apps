using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PTL.Api.Controllers;
using PTL.Api.Tests.Distribution;
using PTL.Api.Tests.Scheme;
using PTL.Contracts.Scheme;
using PTL.Core.Scheme;

namespace PTL.Api.Tests.Endpoints;

public class SchemeControllerTests
{
    private static readonly Guid TestConsultantTabulationId = Guid.NewGuid();
    private static readonly Guid PublishedTabulationId = Guid.NewGuid();
    private static SchemeController CreateController(FakeSchemeRepository repository)
    {
        var controller = new SchemeController(new SchemeService(repository, new PTL.Api.Tests.Lookup.FakeLookupRepository(), new PTL.Api.Tests.Participant.FakeViewerRepository(), new PTL.Api.Tests.Participant.FakeParticipantRepository(), NullLogger<SchemeService>.Instance), NullLogger<SchemeController>.Instance);

        // ValidationProblem() resolves ProblemDetailsFactory from HttpContext.RequestServices,
        // which a bare controller instance does not have without this wiring.
        var services = new ServiceCollection().AddMvc().Services.BuildServiceProvider();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { RequestServices = services }
        };

        return controller;
    }

    private static SchemeRequest ValidCreateRequest(int? yearId = null, string identifier = "PT1234") => new(
        YearId: yearId ?? DateTime.UtcNow.Year + 1,
        Identifier: identifier,
        Name: "Test Scheme",
        ScheduleId: Guid.NewGuid(),
        ScheduleCodeId: Guid.NewGuid(),
        StartDate: null,
        DistributionMonthApr: true,
        DistributionMonthMay: false,
        DistributionMonthJun: false,
        DistributionMonthJul: false,
        DistributionMonthAug: false,
        DistributionMonthSep: false,
        DistributionAsAvailable: false,
        DistributionMonthOct: false,
        DistributionMonthNov: false,
        DistributionMonthDec: false,
        DistributionMonthJan: false,
        DistributionMonthFeb: false,
        DistributionMonthMar: false,
        WeekNumber: 0,
        DayOfWeekId: Guid.NewGuid(),
        NumberOfSamples: 5,
        SampleOrigin: "UK",
        Deadline: 10,
        Subcontractor: string.Empty,
        CombinedPackaging: false,
        Postage: null,
        CustomsVolume: "1kg",
        SamplePackingInstructions: string.Empty,
        RequiresAssessment: false,
        CommentsRequired: false,
        Pilot: false,
        LimitedSampleAvailability: false,
        Accredited: false,
        NoVLALabs: false,
        ComerciallyAvailable: false,
        CustomsDescription: "Biological samples",
        DataConsentDeclarationActive: false,
        DataConsentDeclarationText: null,
        Instructions: "Follow the packing instructions.",
        DateOfReceipt: false,
        StorageConditions: false,
        ConditionOnReceipt: false,
        TestConsultant1: Guid.NewGuid(),
        TestConsultant2: null,
        TestConsultant3: null,
        TestConsultantTabulationId: TestConsultantTabulationId,
        UseExternalReference: false,
        StoreRatings: false,
        Assessor1: null,
        Assessor2: null,
        Assessor3: null,
        Assessor4: null,
        StandardTabulationText: null,
        // A non-assessment scheme needs one tabulation for the Test Consultant and one that can
        // be published.
        Tabulations:
        [
            new SchemeTabulationRequest(TestConsultantTabulationId, "Test Consultant", false, false, false, true, true, [], []),
            new SchemeTabulationRequest(PublishedTabulationId, "Published", false, false, false, true, true, [], [])
        ]);

    private static SchemeRequest ToUpdateRequest(SchemeRequest request) => request;

    [Fact]
    public async Task CreateScheme_ValidRequest_ReturnsCreatedAtAction()
    {
        var controller = CreateController(new FakeSchemeRepository());

        var result = await controller.CreateScheme(ValidCreateRequest(), CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(SchemeController.GetScheme), created.ActionName);
        Assert.IsType<SchemeResponse>(created.Value);
    }

    [Fact]
    public async Task CreateScheme_InvalidIdentifier_ReturnsValidationProblem()
    {
        var controller = CreateController(new FakeSchemeRepository());
        var request = ValidCreateRequest(identifier: "BADID");

        var result = await controller.CreateScheme(request, CancellationToken.None);

        var badRequest = Assert.IsType<ObjectResult>(result.Result, exactMatch: false);
        Assert.Equal(400, badRequest.StatusCode);
    }

    [Fact]
    public async Task GetScheme_UnknownScheme_ReturnsNotFound()
    {
        var controller = CreateController(new FakeSchemeRepository());

        var result = await controller.GetScheme(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task CreateScheme_WithTestsAndCategories_RoundTripsTheWholeTree()
    {
        var controller = CreateController(new FakeSchemeRepository());
        var testId = Guid.NewGuid();
        var resultItem = new SchemeTestItemRequest(Guid.NewGuid(), Guid.NewGuid());
        var methodItem = new SchemeTestItemRequest(Guid.NewGuid(), Guid.NewGuid());
        var criterion = new SchemeTestItemRequest(Guid.NewGuid(), Guid.NewGuid());
        var category = new SchemeCategoryItemRequest(Guid.NewGuid(), Guid.NewGuid(), [criterion]);
        var request = ValidCreateRequest() with
        {
            RequiresAssessment = true,
            Assessor1 = Guid.NewGuid(),
            Assessor2 = Guid.NewGuid(),
            Tests = [new SchemeTestRequest(testId, Guid.NewGuid(), [resultItem], [methodItem], [category])]
        };

        var created = await controller.CreateScheme(request, CancellationToken.None);

        var schemeId = ((SchemeResponse)((CreatedAtActionResult)created.Result!).Value!).SchemeId;
        var result = await controller.GetScheme(schemeId, CancellationToken.None);
        var response = Assert.IsType<SchemeResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);

        var test = Assert.Single(response.Tests!);
        Assert.Equal(testId, test.TestId);
        Assert.Equal(resultItem.ItemId, Assert.Single(test.ResultItems).ItemId);
        Assert.Equal(methodItem.ItemId, Assert.Single(test.MethodItems).ItemId);
        var returnedCategory = Assert.Single(test.Categories);
        Assert.Equal(category.CategoryItemId, returnedCategory.CategoryItemId);
        Assert.Equal(criterion.ItemId, Assert.Single(returnedCategory.Criteria).ItemId);
    }

    [Fact]
    public async Task RenewScheme_UnknownScheme_ReturnsNotFound()
    {
        var controller = CreateController(new FakeSchemeRepository());

        var result = await controller.RenewScheme(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task RenewScheme_ExistingScheme_ReturnsOkWithDraftForNextYear()
    {
        var controller = CreateController(new FakeSchemeRepository());
        var request = ValidCreateRequest();
        var created = await controller.CreateScheme(request, CancellationToken.None);
        var original = (SchemeResponse)((CreatedAtActionResult)created.Result!).Value!;

        var result = await controller.RenewScheme(original.SchemeId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var renewed = Assert.IsType<SchemeResponse>(ok.Value);
        Assert.Equal(Guid.Empty, renewed.SchemeId);
        Assert.Equal(original.SharedId, renewed.SharedId);
        Assert.Equal(original.YearId + 1, renewed.YearId);
        Assert.Equal(original.Identifier, renewed.Identifier);
    }

    [Fact]
    public async Task GetScheme_ExistingScheme_ReturnsOk()
    {
        var controller = CreateController(new FakeSchemeRepository());
        var created = await controller.CreateScheme(ValidCreateRequest(), CancellationToken.None);
        var schemeId = ((SchemeResponse)((CreatedAtActionResult)created.Result!).Value!).SchemeId;

        var result = await controller.GetScheme(schemeId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(schemeId, ((SchemeResponse)ok.Value!).SchemeId);
    }

    [Fact]
    public async Task GetScheme_ExistingScheme_ReturnsFullyMappedResponse()
    {
        var controller = CreateController(new FakeSchemeRepository());
        var request = ValidCreateRequest();
        var created = await controller.CreateScheme(request, CancellationToken.None);
        var schemeId = ((SchemeResponse)((CreatedAtActionResult)created.Result!).Value!).SchemeId;

        var result = await controller.GetScheme(schemeId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<SchemeResponse>(ok.Value);
        Assert.Equal(schemeId, response.SchemeId);
        Assert.NotEqual(Guid.Empty, response.SharedId);
        Assert.Equal(request.YearId, response.YearId);
        Assert.Equal(request.Identifier, response.Identifier);
        Assert.Equal(request.Name, response.Name);
        Assert.Equal(request.ScheduleId, response.ScheduleId);
        Assert.Equal(request.ScheduleCodeId, response.ScheduleCodeId);
        // StartDate is derived from the distribution months (legacy RecalucalateStartDate), not
        // the posted value - the fixture distributes in April, so the scheme starts 1 April.
        Assert.Equal(new DateTime(request.YearId, 4, 1), response.StartDate);
        Assert.Equal(request.DistributionMonthApr, response.DistributionMonthApr);
        Assert.Equal(request.DistributionMonthMay, response.DistributionMonthMay);
        Assert.Equal(request.DistributionMonthJun, response.DistributionMonthJun);
        Assert.Equal(request.DistributionMonthJul, response.DistributionMonthJul);
        Assert.Equal(request.DistributionMonthAug, response.DistributionMonthAug);
        Assert.Equal(request.DistributionMonthSep, response.DistributionMonthSep);
        Assert.Equal(request.DistributionAsAvailable, response.DistributionAsAvailable);
        Assert.Equal(request.DistributionMonthOct, response.DistributionMonthOct);
        Assert.Equal(request.DistributionMonthNov, response.DistributionMonthNov);
        Assert.Equal(request.DistributionMonthDec, response.DistributionMonthDec);
        Assert.Equal(request.DistributionMonthJan, response.DistributionMonthJan);
        Assert.Equal(request.DistributionMonthFeb, response.DistributionMonthFeb);
        Assert.Equal(request.DistributionMonthMar, response.DistributionMonthMar);
        Assert.Equal(request.WeekNumber, response.WeekNumber);
        Assert.Equal(request.DayOfWeekId, response.DayOfWeekId);
        Assert.Equal(request.NumberOfSamples, response.NumberOfSamples);
        Assert.Equal(1, response.SampleNoSequence);
        Assert.Equal(request.SampleOrigin, response.SampleOrigin);
        Assert.Equal(request.Deadline, response.Deadline);
        Assert.Equal(request.Subcontractor, response.Subcontractor);
        Assert.Equal(request.CombinedPackaging, response.CombinedPackaging);
        Assert.Equal(request.Postage, response.Postage);
        Assert.Equal(request.CustomsVolume, response.CustomsVolume);
        Assert.Equal(request.SamplePackingInstructions, response.SamplePackingInstructions);
        Assert.Equal(request.RequiresAssessment, response.RequiresAssessment);
        Assert.Equal(request.CommentsRequired, response.CommentsRequired);
        Assert.Equal(request.Pilot, response.Pilot);
        Assert.Equal(request.LimitedSampleAvailability, response.LimitedSampleAvailability);
        Assert.Equal(request.Accredited, response.Accredited);
        Assert.Equal(request.NoVLALabs, response.NoVLALabs);
        Assert.Equal(request.ComerciallyAvailable, response.ComerciallyAvailable);
        Assert.Equal(request.CustomsDescription, response.CustomsDescription);
        Assert.Equal(request.DataConsentDeclarationActive, response.DataConsentDeclarationActive);
        Assert.Equal(request.DataConsentDeclarationText, response.DataConsentDeclarationText);
        Assert.Equal(request.Instructions, response.Instructions);
        Assert.Equal(request.DateOfReceipt, response.DateOfReceipt);
        Assert.Equal(request.StorageConditions, response.StorageConditions);
        Assert.Equal(request.ConditionOnReceipt, response.ConditionOnReceipt);
        Assert.Equal(request.TestConsultant1, response.TestConsultant1);
        Assert.Equal(request.TestConsultant2, response.TestConsultant2);
        Assert.Equal(request.TestConsultant3, response.TestConsultant3);
        Assert.Equal(request.TestConsultantTabulationId, response.TestConsultantTabulationId);
        Assert.Equal(request.UseExternalReference, response.UseExternalReference);
        Assert.Equal(request.StoreRatings, response.StoreRatings);
        Assert.Equal(request.Assessor1, response.Assessor1);
        Assert.Equal(request.Assessor2, response.Assessor2);
        Assert.Equal(request.Assessor3, response.Assessor3);
        Assert.Equal(request.Assessor4, response.Assessor4);
        Assert.Equal(request.StandardTabulationText, response.StandardTabulationText);
        Assert.True(response.LastModified > DateTime.MinValue);
        Assert.False(response.IsReadOnly);
    }

    [Fact]
    public async Task GetSchemes_ByYear_ReturnsMatch()
    {
        var controller = CreateController(new FakeSchemeRepository());
        var year = DateTime.UtcNow.Year + 1;
        await controller.CreateScheme(ValidCreateRequest(year), CancellationToken.None);

        var result = await controller.GetSchemes(year, null, 1, 20, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<SchemeSearchResponse>(ok.Value);
        Assert.Single(response.Items);
        Assert.Equal(year, response.Items[0].YearId);
    }

    [Fact]
    public async Task UpdateScheme_UnknownScheme_ReturnsNotFound()
    {
        var controller = CreateController(new FakeSchemeRepository());

        var result = await controller.UpdateScheme(Guid.NewGuid(), ToUpdateRequest(ValidCreateRequest()), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task UpdateScheme_ValidRequest_ReturnsOk()
    {
        var controller = CreateController(new FakeSchemeRepository());
        var created = await controller.CreateScheme(ValidCreateRequest(), CancellationToken.None);
        var schemeId = ((SchemeResponse)((CreatedAtActionResult)created.Result!).Value!).SchemeId;
        var updateRequest = ToUpdateRequest(ValidCreateRequest()) with { Name = "Renamed Scheme" };

        var result = await controller.UpdateScheme(schemeId, updateRequest, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal("Renamed Scheme", ((SchemeResponse)ok.Value!).Name);
    }

    [Fact]
    public async Task UpdateScheme_InvalidRequest_ReturnsValidationProblem()
    {
        var controller = CreateController(new FakeSchemeRepository());
        var created = await controller.CreateScheme(ValidCreateRequest(), CancellationToken.None);
        var schemeId = ((SchemeResponse)((CreatedAtActionResult)created.Result!).Value!).SchemeId;
        var invalidRequest = ToUpdateRequest(ValidCreateRequest()) with { Name = string.Empty };

        var result = await controller.UpdateScheme(schemeId, invalidRequest, CancellationToken.None);

        var objectResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.IsType<ValidationProblemDetails>(objectResult.Value);
    }

    [Fact]
    public async Task GetSchemeFamilyHistory_ReturnsCreatedScheme()
    {
        var controller = CreateController(new FakeSchemeRepository());
        var created = await controller.CreateScheme(ValidCreateRequest(), CancellationToken.None);
        var response = (SchemeResponse)((CreatedAtActionResult)created.Result!).Value!;

        var result = await controller.GetSchemeFamilyHistory(response.SharedId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var history = Assert.IsType<IReadOnlyList<SchemeHistoryResponse>>(ok.Value, exactMatch: false);
        Assert.Single(history);
    }

    [Fact]
    public async Task GetSchemesForParticipant_KnownParticipant_ReturnsMappedCommercialAndNonCommercialSchemes()
    {
        var schemeRepository = new FakeSchemeRepository
        {
            CommercialSchemesBySsoId = [new ParticipantSchemeEntity { SchemeId = Guid.NewGuid(), Identifier = "PT1111", Name = "Commercial", Year = "2025/26", IsParticipating = true }],
            NonCommercialSchemesBySsoId = [new ParticipantSchemeEntity { SchemeId = Guid.NewGuid(), Identifier = "PT2222", Name = "Non-Commercial", Year = "2025/26", IsParticipating = true }]
        };
        var participantRepository = new PTL.Api.Tests.Participant.FakeParticipantRepository();
        var participant = await participantRepository.CreateAsync(new PTL.Core.Participant.Participant { SsoId = Guid.NewGuid() });
        var service = new SchemeService(schemeRepository, new PTL.Api.Tests.Lookup.FakeLookupRepository(), new PTL.Api.Tests.Participant.FakeViewerRepository(), participantRepository, NullLogger<SchemeService>.Instance);
        var controller = new SchemeController(service, NullLogger<SchemeController>.Instance);

        var result = await controller.GetSchemesForParticipant(participant.ParticipantId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ParticipantSchemeListResponse>(ok.Value);
        Assert.Single(response.CommercialSchemes);
        Assert.Equal("PT1111", response.CommercialSchemes[0].Identifier);
        Assert.Single(response.NonCommercialSchemes);
        Assert.Equal("PT2222", response.NonCommercialSchemes[0].Identifier);
    }

    [Fact]
    public async Task GetSchemesForParticipant_UnknownParticipant_ReturnsOkWithEmptyLists()
    {
        var controller = CreateController(new FakeSchemeRepository());

        var result = await controller.GetSchemesForParticipant(Guid.NewGuid(), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ParticipantSchemeListResponse>(ok.Value);
        Assert.Empty(response.CommercialSchemes);
        Assert.Empty(response.NonCommercialSchemes);
    }

    private static SchemeController ControllerOver(FakeSchemeService service, string? signedInAs = null)
    {
        var context = new DefaultHttpContext { RequestServices = new ServiceCollection().AddMvc().Services.BuildServiceProvider() };
        if (signedInAs is not null)
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, signedInAs)], "Test"));
        }

        return new SchemeController(service, NullLogger<SchemeController>.Instance) { ControllerContext = new ControllerContext { HttpContext = context } };
    }

    [Fact]
    public async Task UpdateScheme_Updated_ReturnsOkWithTheSavedScheme()
    {
        var schemeId = Guid.NewGuid();
        var service = new FakeSchemeService { UpdateResult = new SchemeUpdateResult(SchemeUpdateStatus.Updated, new PTL.Core.Scheme.Scheme { SchemeId = schemeId, Name = "Saved" }) };
        var controller = ControllerOver(service);

        var result = await controller.UpdateScheme(schemeId, ValidCreateRequest(), CancellationToken.None);

        var response = Assert.IsType<SchemeResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(schemeId, response.SchemeId);
        Assert.Equal("Saved", response.Name);
    }

    [Fact]
    public async Task UpdateScheme_AuditReasonRequired_Returns428WithTheAuditObjectName()
    {
        var service = new FakeSchemeService { UpdateResult = new SchemeUpdateResult(SchemeUpdateStatus.AuditReasonRequired, null, "Scheme PT1234: Test Scheme") };
        var controller = ControllerOver(service);

        var result = await controller.UpdateScheme(Guid.NewGuid(), ValidCreateRequest(), CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status428PreconditionRequired, objectResult.StatusCode);
        Assert.Equal(new SchemeAuditRequiredResponse("Scheme PT1234: Test Scheme"), objectResult.Value);
    }

    [Fact]
    public async Task UpdateScheme_ServiceReportsNotFound_Returns404()
    {
        var service = new FakeSchemeService { UpdateResult = new SchemeUpdateResult(SchemeUpdateStatus.NotFound) };
        var controller = ControllerOver(service);

        var result = await controller.UpdateScheme(Guid.NewGuid(), ValidCreateRequest(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task UpdateScheme_ServiceThrowsAValidationError_ReturnsValidationProblemKeyedByField()
    {
        var service = new FakeSchemeService
        {
            UpdateException = new SchemeValidationException([new SchemeValidationError("AuditWhy", SchemeService.AuditWhyTooLongMessage)]),
        };
        var controller = ControllerOver(service);

        var result = await controller.UpdateScheme(Guid.NewGuid(), ValidCreateRequest() with { AuditWhy = new string('x', 2001) }, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var problem = Assert.IsType<ValidationProblemDetails>(badRequest.Value);
        Assert.Equal(SchemeService.AuditWhyTooLongMessage, Assert.Single(problem.Errors["AuditWhy"]));
    }

    [Fact]
    public async Task UpdateScheme_PassesTheRouteIdTheSignedInNameAndTheReasonToTheService()
    {
        var schemeId = Guid.NewGuid();
        var service = new FakeSchemeService { UpdateResult = new SchemeUpdateResult(SchemeUpdateStatus.NotFound) };
        var controller = ControllerOver(service, signedInAs: @"DEFRA\jsmith");

        await controller.UpdateScheme(schemeId, ValidCreateRequest() with { AuditWhy = "Corrected the order" }, CancellationToken.None);

        var update = service.LastUpdate!.Value;
        Assert.Equal(schemeId, update.SchemeId);
        Assert.Equal(schemeId, update.Scheme.SchemeId);
        Assert.Equal(@"DEFRA\jsmith", update.AuditWho);
        Assert.Equal("Corrected the order", update.AuditWhy);
    }

    [Fact]
    public async Task UpdateScheme_NobodySignedIn_AuditsAsGuest()
    {
        var service = new FakeSchemeService { UpdateResult = new SchemeUpdateResult(SchemeUpdateStatus.NotFound) };
        var controller = ControllerOver(service);

        await controller.UpdateScheme(Guid.NewGuid(), ValidCreateRequest(), CancellationToken.None);

        Assert.Equal("Guest", service.LastUpdate!.Value.AuditWho);
        Assert.Null(service.LastUpdate.Value.AuditWhy);
    }

    [Fact]
    public async Task UpdateScheme_NoHttpContext_AuditsAsGuest()
    {
        var service = new FakeSchemeService { UpdateResult = new SchemeUpdateResult(SchemeUpdateStatus.NotFound) };
        var controller = new SchemeController(service, NullLogger<SchemeController>.Instance);

        await controller.UpdateScheme(Guid.NewGuid(), ValidCreateRequest(), CancellationToken.None);

        Assert.Equal("Guest", service.LastUpdate!.Value.AuditWho);
    }

    [Fact]
    public async Task UpdateScheme_AuditableEdit_Returns428UntilAReasonIsSentThenSavesWithItsAudit()
    {
        var repository = new FakeSchemeRepository();
        var controller = CreateController(repository);
        var created = await controller.CreateScheme(ValidCreateRequest(), CancellationToken.None);
        var schemeId = ((SchemeResponse)((CreatedAtActionResult)created.Result!).Value!).SchemeId;
        var withNewTest = ValidCreateRequest() with { Tests = [new SchemeTestRequest(Guid.NewGuid(), Guid.NewGuid(), [], [], [])] };

        var asked = await controller.UpdateScheme(schemeId, withNewTest, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(asked.Result);
        Assert.Equal(StatusCodes.Status428PreconditionRequired, objectResult.StatusCode);
        Assert.Equal("Scheme PT1234: Test Scheme", Assert.IsType<SchemeAuditRequiredResponse>(objectResult.Value).AuditObjectName);
        Assert.Empty(repository.AuditWrites);

        var saved = await controller.UpdateScheme(schemeId, withNewTest with { AuditWhy = "Added a test" }, CancellationToken.None);

        Assert.IsType<OkObjectResult>(saved.Result);
        Assert.Equal("Added a test", repository.LastAudit!.AuditWhy);
        Assert.Equal("Guest", repository.LastAudit.AuditWho);
        Assert.Single(repository.LastAudit.Plan.Tests);
    }
}
