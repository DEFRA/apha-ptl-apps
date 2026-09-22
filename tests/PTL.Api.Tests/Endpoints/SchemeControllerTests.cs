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
        Assert.Equal(request.StartDate, response.StartDate);
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
}
