using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PTL.Api.Controllers;
using PTL.Api.Tests.Scheme;
using PTL.Contracts.Scheme;
using PTL.Core.Scheme;

namespace PTL.Api.Tests.Endpoints;

public class SchemeControllerTests
{
    private static SchemeController CreateController(FakeSchemeRepository repository)
    {
        var controller = new SchemeController(new SchemeService(repository, NullLogger<SchemeService>.Instance), NullLogger<SchemeController>.Instance);

        // ValidationProblem() resolves ProblemDetailsFactory from HttpContext.RequestServices,
        // which a bare controller instance does not have without this wiring.
        var services = new ServiceCollection().AddMvc().Services.BuildServiceProvider();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { RequestServices = services }
        };

        return controller;
    }

    private static CreateSchemeRequest ValidCreateRequest(int? yearId = null, string identifier = "PT1234") => new(
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
        TestConsultant1: null,
        TestConsultant2: null,
        TestConsultant3: null,
        TestConsultantTabulationId: null,
        UseExternalReference: false,
        StoreRatings: false,
        Assessor1: null,
        Assessor2: null,
        Assessor3: null,
        Assessor4: null,
        StandardTabulationText: null);

    private static UpdateSchemeRequest ToUpdateRequest(CreateSchemeRequest request) => new(
        request.YearId, request.Identifier, request.Name, request.ScheduleId, request.ScheduleCodeId, request.StartDate,
        request.DistributionMonthApr, request.DistributionMonthMay, request.DistributionMonthJun, request.DistributionMonthJul,
        request.DistributionMonthAug, request.DistributionMonthSep, request.DistributionAsAvailable, request.DistributionMonthOct,
        request.DistributionMonthNov, request.DistributionMonthDec, request.DistributionMonthJan, request.DistributionMonthFeb,
        request.DistributionMonthMar, request.WeekNumber, request.DayOfWeekId, request.NumberOfSamples, request.SampleOrigin,
        request.Deadline, request.Subcontractor, request.CombinedPackaging, request.Postage, request.CustomsVolume,
        request.SamplePackingInstructions, request.RequiresAssessment, request.CommentsRequired, request.Pilot,
        request.LimitedSampleAvailability, request.Accredited, request.NoVLALabs, request.ComerciallyAvailable,
        request.CustomsDescription, request.DataConsentDeclarationActive, request.DataConsentDeclarationText,
        request.Instructions, request.DateOfReceipt, request.StorageConditions, request.ConditionOnReceipt,
        request.TestConsultant1, request.TestConsultant2, request.TestConsultant3, request.TestConsultantTabulationId,
        request.UseExternalReference, request.StoreRatings, request.Assessor1, request.Assessor2, request.Assessor3,
        request.Assessor4, request.StandardTabulationText);

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

        var badRequest = Assert.IsAssignableFrom<ObjectResult>(result.Result);
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
    public async Task GetSchemeFamilyHistory_ReturnsCreatedScheme()
    {
        var controller = CreateController(new FakeSchemeRepository());
        var created = await controller.CreateScheme(ValidCreateRequest(), CancellationToken.None);
        var response = (SchemeResponse)((CreatedAtActionResult)created.Result!).Value!;

        var result = await controller.GetSchemeFamilyHistory(response.SharedId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var history = Assert.IsAssignableFrom<IReadOnlyList<SchemeHistoryResponse>>(ok.Value);
        Assert.Single(history);
    }
}
