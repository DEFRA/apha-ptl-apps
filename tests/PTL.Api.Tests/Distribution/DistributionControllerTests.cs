using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using PTL.Api.Controllers;
using PTL.Contracts.Distribution;
using PTL.Core.Distribution;

namespace PTL.Api.Tests.Distribution;

public class DistributionControllerTests
{
    private static DistributionController CreateController(FakeDistributionRepository repository, FakeSchemeService? schemeService = null) =>
        new(
            new DistributionService(repository),
            new SampleNumbersService(repository, schemeService ?? new FakeSchemeService()),
            new IntendedResultsService(repository),
            new DistributionParticipantsService(repository),
            new PreparationService(repository),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<DistributionController>.Instance);

    // ValidationProblem() resolves ProblemDetailsFactory from HttpContext.RequestServices,
    // which a bare controller instance does not have without this wiring.
    private static DistributionController CreateControllerWithProblemDetails(FakeDistributionRepository repository, FakeSchemeService? schemeService = null)
    {
        var controller = CreateController(repository, schemeService);
        var services = new ServiceCollection().AddMvc().Services.BuildServiceProvider();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { RequestServices = services }
        };

        return controller;
    }

    [Fact]
    public async Task GetDashboard_ReturnsTwelveMonthsWithMonthDescription()
    {
        var repository = new FakeDistributionRepository();
        var controller = CreateController(repository);

        var result = await controller.GetDashboard(2026, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var months = Assert.IsType<IReadOnlyList<DistributionDashboardMonthResponse>>(ok.Value, exactMatch: false);
        Assert.Equal(12, months.Count);
        Assert.Equal("Apr", months[0].MonthDescription);
        Assert.Equal("Mar", months[^1].MonthDescription);
    }

    [Fact]
    public async Task GetDashboard_InitialisedMonth_ReportsMonthlyDistributionIdAndPercentages()
    {
        var monthlyDistributionId = Guid.NewGuid();
        var repository = new FakeDistributionRepository();
        repository.Summaries.Add(new DistributionMonthSummaryEntity
        {
            MonthlyDistributionId = monthlyDistributionId,
            YearId = 2026,
            MonthId = 4,
            NumberOfSchemes = 4,
            NumberOfSampleNumbersDefined = 2,
        });
        var controller = CreateController(repository);

        var result = await controller.GetDashboard(2026, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var months = Assert.IsType<IReadOnlyList<DistributionDashboardMonthResponse>>(ok.Value, exactMatch: false);
        var april = months.Single(m => m.MonthId == 4 && m.YearId == 2026);
        Assert.Equal(monthlyDistributionId, april.MonthlyDistributionId);
        Assert.False(april.IsInitialisable);
        Assert.Equal(50m, april.SchedulingCompletePercentage);
    }

    [Fact]
    public async Task GetYears_ReturnsMappedYearOptions()
    {
        var repository = new FakeDistributionRepository();
        repository.Years.Add(new DistributionMonthYearEntity { YearId = 2026, MonthId = 5 });
        var controller = CreateController(repository);

        var result = await controller.GetYears(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var years = Assert.IsType<IReadOnlyList<DistributionYearOptionResponse>>(ok.Value, exactMatch: false);
        Assert.Contains(years, y => y.YearId == 2026 && y.Label == "2026/27");
    }

    [Fact]
    public async Task GetSchedule_MonthNotInitialised_ReturnsEmptyResponseWithNullId()
    {
        var repository = new FakeDistributionRepository();
        var controller = CreateController(repository);

        var result = await controller.GetSchedule(2026, 4, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<MonthlyDistributionResponse>(ok.Value);
        Assert.Null(response.MonthlyDistributionId);
        Assert.Empty(response.Schemes);
    }

    [Fact]
    public async Task GetSchedule_MonthInitialised_ReturnsMappedSchemes()
    {
        var monthlyDistributionId = Guid.NewGuid();
        var schemeId = Guid.NewGuid();
        var repository = new FakeDistributionRepository();
        repository.Schedules[(2026, 4)] = (monthlyDistributionId, [new MonthlyDistributionSchemeEntity
        {
            MonthlyDistributionSchemeId = schemeId,
            SchemeIdentifier = "S001",
            SchemeName = "Test Scheme",
            DistributionReference = "D26-01",
            DistributionReferenceSuffix = "A",
            ScheduleCode = "BA",
        }]);
        var controller = CreateController(repository);

        var result = await controller.GetSchedule(2026, 4, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<MonthlyDistributionResponse>(ok.Value);
        Assert.Equal(monthlyDistributionId, response.MonthlyDistributionId);
        var scheme = Assert.Single(response.Schemes);
        Assert.Equal("D26-01A/BA", scheme.DistributionReferenceFull);
    }

    [Fact]
    public async Task SaveSchedule_InvalidRow_ReturnsUnsuccessfulWithFieldErrors()
    {
        var schemeId = Guid.NewGuid();
        var repository = new FakeDistributionRepository();
        repository.Schedules[(2026, 4)] = (Guid.NewGuid(), [new MonthlyDistributionSchemeEntity { MonthlyDistributionSchemeId = schemeId }]);
        var controller = CreateController(repository);

        var rows = new List<MonthlyDistributionScheduleRowRequest>
        {
            new(schemeId, new DateTime(2026, 4, 1), new DateTime(2026, 4, 1), new DateTime(2026, 3, 30), new DateTime(2026, 4, 10), false),
        };

        var result = await controller.SaveSchedule(2026, 4, rows, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<MonthlyDistributionScheduleSaveResult>(ok.Value);
        Assert.False(response.Success);
        Assert.True(response.FieldErrorsBySchemeId.ContainsKey(schemeId));
    }

    private static readonly Guid SampleNumbersSchemeId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid SampleNumbersSampleId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid SampleNumbersTestId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid SampleNumbersSampleTestId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

    private static FakeDistributionRepository SampleNumbersRepository()
    {
        var repository = new FakeDistributionRepository();
        repository.SchemesById[SampleNumbersSchemeId] = new MonthlyDistributionSchemeEntity
        {
            MonthlyDistributionSchemeId = SampleNumbersSchemeId,
            SchemeIdentifier = "PT0060",
            SchemeName = "Mastitis bovine",
            DistributionReference = "17152",
            DistributionReferenceSuffix = "A",
            ScheduleCode = "BA",
            TotalSetsOfSamplesRequired = 9,
        };
        repository.IntendedResults[SampleNumbersSchemeId] = new IntendedResultsData(
            new IntendedResultsHeaderEntity { MonthlyDistributionSchemeId = SampleNumbersSchemeId, Comments = "Existing" },
            [new DistributionSampleEntity { SampleId = SampleNumbersSampleId, DistributionSchemeId = SampleNumbersSchemeId, SampleNumber = "S1", InternalDescription = "Internal 1" }],
            [new DistributionSampleTestEntity { SampleTestId = SampleNumbersSampleTestId, SampleId = SampleNumbersSampleId, TestId = SampleNumbersTestId, IsApplicable = true }],
            [new DistributionTestEntity { TestId = SampleNumbersTestId, TestName = "Somatic cell count", Order = 1 }],
            [new DistributionTestMethodItemEntity { TestId = SampleNumbersTestId, Name = "Method A", Order = 1 }],
            [new DistributionTestResultItemEntity { TestId = SampleNumbersTestId, Name = "Result A", Order = 1 }],
            [],
            []);
        return repository;
    }

    [Fact]
    public async Task GetSampleNumbers_UnknownScheme_ReturnsNotFound()
    {
        var controller = CreateController(new FakeDistributionRepository());

        var result = await controller.GetSampleNumbers(SampleNumbersSchemeId, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetSampleNumbers_KnownScheme_ReturnsMappedResponse()
    {
        var controller = CreateController(SampleNumbersRepository());

        var result = await controller.GetSampleNumbers(SampleNumbersSchemeId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<SampleNumbersResponse>(ok.Value);
        Assert.Equal(SampleNumbersSchemeId, response.MonthlyDistributionSchemeId);
        Assert.Equal("17152A/BA", response.DistributionReferenceFull);
        Assert.Equal("PT0060", response.SchemeIdentifier);
        Assert.Equal("Mastitis bovine", response.SchemeName);
        Assert.Equal(9, response.NumberOfSetsRequired);
        Assert.Equal("Existing", response.Comments);
        Assert.False(response.RequiresAssessment);
        Assert.False(response.IsReadyForAssessment);

        var sample = Assert.Single(response.Samples);
        Assert.Equal(SampleNumbersSampleId, sample.SampleId);
        Assert.Equal("S1", sample.SampleNumber);
        Assert.Equal("Internal 1", sample.InternalDescription);
        var allocation = Assert.Single(sample.Allocations);
        Assert.Equal(SampleNumbersSampleTestId, allocation.SampleTestId);
        Assert.Equal(SampleNumbersTestId, allocation.TestId);
        Assert.True(allocation.IsApplicable);

        var test = Assert.Single(response.Tests);
        Assert.Equal("Somatic cell count", test.TestName);
        Assert.Equal(["Result A"], test.TestResultItemNames);
        Assert.Equal(["Method A"], test.TestMethodItemNames);
        Assert.Empty(test.CategoryItemNames);
    }

    // A GET can fail with a business reason, because opening Sample Numbers for a distribution
    // that has never been defined materialises its structure from the Scheme.
    [Fact]
    public async Task GetSampleNumbers_StructureCannotBeBuilt_ReturnsValidationProblemWithTheReason()
    {
        var schemeId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
        var repository = new FakeDistributionRepository();
        repository.SchemesById[SampleNumbersSchemeId] = new MonthlyDistributionSchemeEntity
        {
            MonthlyDistributionSchemeId = SampleNumbersSchemeId,
            SchemeId = schemeId,
            SchemeIdentifier = "PT0060",
            YearId = 2026,
            MonthId = 4,
        };
        repository.IntendedResults[SampleNumbersSchemeId] = new IntendedResultsData(
            new IntendedResultsHeaderEntity { MonthlyDistributionSchemeId = SampleNumbersSchemeId }, [], [], [], [], [], [], []);

        var schemeService = new FakeSchemeService();
        schemeService.Schemes[schemeId] = new PTL.Core.Scheme.Scheme { SchemeId = schemeId, Identifier = "PT0060", TestConsultantTabulationId = null };

        var controller = CreateControllerWithProblemDetails(repository, schemeService);

        var result = await controller.GetSampleNumbers(SampleNumbersSchemeId, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result.Result, exactMatch: false);
        var problem = Assert.IsType<ValidationProblemDetails>(objectResult.Value);
        var messages = Assert.Single(problem.Errors).Value;
        Assert.Contains(messages, m => m.Contains("has no Test Consultant Tabulation", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SaveSampleNumbers_InvalidModelState_ReturnsValidationProblem()
    {
        var controller = CreateControllerWithProblemDetails(SampleNumbersRepository());
        controller.ModelState.AddModelError("Samples", "The Samples field is required.");

        var request = new SampleNumbersSaveRequest("ok", []);
        var result = await controller.SaveSampleNumbers(SampleNumbersSchemeId, request, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result.Result, exactMatch: false);
        Assert.IsType<ValidationProblemDetails>(objectResult.Value);
    }

    [Fact]
    public async Task SaveSampleNumbers_Valid_ReturnsSuccessfulResult()
    {
        var repository = SampleNumbersRepository();
        var controller = CreateController(repository);

        var request = new SampleNumbersSaveRequest("New comments",
        [
            new SampleNumberRowRequest(SampleNumbersSampleId, "S1-edited", "Internal edited", "External", "Tabulation", "TC",
                [new SampleAllocationRequest(SampleNumbersSampleTestId, false)]),
        ], AuditWhy: "Corrected the tabulation description");

        var result = await controller.SaveSampleNumbers(SampleNumbersSchemeId, request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<SampleNumbersSaveResult>(ok.Value);
        Assert.True(response.Success);
        Assert.Empty(response.PageErrors);
        Assert.Empty(response.FieldErrorsBySampleId);
        Assert.False(response.AuditReasonRequired);
        Assert.Null(response.AuditObjectName);
        var saved = Assert.Single(repository.SavedSampleNumbers);
        Assert.Equal("S1-edited", Assert.Single(saved.Samples).SampleNumber);
        Assert.False(Assert.Single(saved.SampleTests).IsApplicable);
    }

    // Legacy Audit.aspx: a changed Tabulation Description cannot be saved without a reason.
    [Fact]
    public async Task SaveSampleNumbers_TabulationChangedWithoutAReason_ReportsAuditReasonRequired()
    {
        var repository = SampleNumbersRepository();
        var controller = CreateController(repository);

        var request = new SampleNumbersSaveRequest("New comments",
        [
            new SampleNumberRowRequest(SampleNumbersSampleId, "S1", "Internal 1", string.Empty, "Tabulation", string.Empty, []),
        ]);

        var result = await controller.SaveSampleNumbers(SampleNumbersSchemeId, request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<SampleNumbersSaveResult>(ok.Value);
        Assert.False(response.Success);
        Assert.True(response.AuditReasonRequired);
        Assert.Equal("Intended Results Set", response.AuditObjectName);
        Assert.Empty(repository.SavedSampleNumbers);
    }

    // AuditWho is never accepted from the body - it is taken from the authenticated identity.
    [Fact]
    public async Task SaveSampleNumbers_AuditWho_ComesFromTheAuthenticatedIdentity()
    {
        var repository = SampleNumbersRepository();
        var controller = CreateController(repository);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "APHA\\jbloggs")], "TestAuth")),
            }
        };

        var request = new SampleNumbersSaveRequest("New comments",
        [
            new SampleNumberRowRequest(SampleNumbersSampleId, "S1", "Internal 1", string.Empty, "Tabulation", string.Empty, []),
        ], AuditWhy: "Corrected the tabulation description");

        await controller.SaveSampleNumbers(SampleNumbersSchemeId, request, CancellationToken.None);

        var saved = Assert.Single(repository.SavedSampleNumbers);
        Assert.Equal("APHA\\jbloggs", saved.AuditWho);
        Assert.Equal("Corrected the tabulation description", saved.AuditWhy);
    }

    // The contract has no AuditWho member at all, so a caller has no way to supply one.
    [Fact]
    public void SampleNumbersSaveRequest_DoesNotExposeAuditWho()
    {
        Assert.Null(typeof(SampleNumbersSaveRequest).GetProperty("AuditWho"));
    }

    [Fact]
    public async Task SaveSampleNumbers_UnauthenticatedCaller_RecordsGuestAsAuditWho()
    {
        var repository = SampleNumbersRepository();
        var controller = CreateController(repository);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var request = new SampleNumbersSaveRequest("New comments",
        [
            new SampleNumberRowRequest(SampleNumbersSampleId, "S1", "Internal 1", string.Empty, "Tabulation", string.Empty, []),
        ], AuditWhy: "Corrected the tabulation description");

        await controller.SaveSampleNumbers(SampleNumbersSchemeId, request, CancellationToken.None);

        Assert.Equal("Guest", Assert.Single(repository.SavedSampleNumbers).AuditWho);
    }

    [Fact]
    public async Task SaveSampleNumbers_PrincipalWithNoIdentity_RecordsGuestAsAuditWho()
    {
        var repository = SampleNumbersRepository();
        var controller = CreateController(repository);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal() }
        };

        var request = new SampleNumbersSaveRequest("New comments",
        [
            new SampleNumberRowRequest(SampleNumbersSampleId, "S1", "Internal 1", string.Empty, "Tabulation", string.Empty, []),
        ], AuditWhy: "Corrected the tabulation description");

        await controller.SaveSampleNumbers(SampleNumbersSchemeId, request, CancellationToken.None);

        Assert.Equal("Guest", Assert.Single(repository.SavedSampleNumbers).AuditWho);
    }

    [Fact]
    public async Task SaveSampleNumbers_Invalid_ReturnsPageAndFieldErrors()
    {
        var repository = SampleNumbersRepository();
        var controller = CreateController(repository);

        var request = new SampleNumbersSaveRequest(new string('x', 8001),
        [
            new SampleNumberRowRequest(SampleNumbersSampleId, string.Empty, "Internal", string.Empty, string.Empty, string.Empty, []),
        ]);

        var result = await controller.SaveSampleNumbers(SampleNumbersSchemeId, request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<SampleNumbersSaveResult>(ok.Value);
        Assert.False(response.Success);
        Assert.Equal("Comments must not exceed 8000 characters", Assert.Single(response.PageErrors));
        var fieldErrors = Assert.Single(response.FieldErrorsBySampleId);
        Assert.Equal(SampleNumbersSampleId, fieldErrors.Key);
        Assert.Contains(fieldErrors.Value, e => e.Field == "SampleNumber" && e.Message == "Enter the Sample Number");
        Assert.Empty(repository.SavedSampleNumbers);
    }

    private static readonly Guid IntendedResultsSchemeId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
    private static readonly Guid IntendedResultsSampleId = Guid.Parse("11111111-2222-2222-2222-222222222222");
    private static readonly Guid IntendedResultsTestId = Guid.Parse("33333333-4444-4444-4444-444444444444");
    private static readonly Guid IntendedResultsSampleTestId = Guid.Parse("55555555-6666-6666-6666-666666666666");
    private static readonly Guid IntendedResultsMethodItemId = Guid.Parse("77777777-8888-8888-8888-888888888888");
    private static readonly Guid IntendedResultsResultItemId = Guid.Parse("99999999-0000-0000-0000-000000000000");
    private static readonly Guid IntendedResultsSampleResultItemId = Guid.Parse("aaaaaaaa-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static FakeDistributionRepository IntendedResultsRepository()
    {
        var repository = new FakeDistributionRepository();
        repository.SchemesById[IntendedResultsSchemeId] = new MonthlyDistributionSchemeEntity
        {
            MonthlyDistributionSchemeId = IntendedResultsSchemeId,
            SchemeIdentifier = "PT0060",
            SchemeName = "Mastitis bovine",
            DistributionReference = "17152",
            DistributionReferenceSuffix = "A",
            ScheduleCode = "BA",
            HasSampleNumbersDefined = true,
        };
        repository.IntendedResults[IntendedResultsSchemeId] = new IntendedResultsData(
            new IntendedResultsHeaderEntity { MonthlyDistributionSchemeId = IntendedResultsSchemeId },
            [new DistributionSampleEntity { SampleId = IntendedResultsSampleId, DistributionSchemeId = IntendedResultsSchemeId, SampleNumber = "S1", InternalDescription = "Internal 1" }],
            [new DistributionSampleTestEntity { SampleTestId = IntendedResultsSampleTestId, SampleId = IntendedResultsSampleId, TestId = IntendedResultsTestId, IsApplicable = true }],
            [new DistributionTestEntity { TestId = IntendedResultsTestId, TestName = "Somatic cell count", Order = 1 }],
            [new DistributionTestMethodItemEntity { TestMethodItemId = IntendedResultsMethodItemId, TestId = IntendedResultsTestId, Name = "Method A", Order = 1, FormatString = "^[0-9]+$", ReturnValueType = "Numeric" }],
            [new DistributionTestResultItemEntity { TestResultItemId = IntendedResultsResultItemId, TestId = IntendedResultsTestId, Name = "Result A", Order = 1, FormatString = "^[0-9]+$", ReturnValueType = "Numeric" }],
            [],
            [],
            [new DistributionSampleResultItemEntity { SampleResultItemId = IntendedResultsSampleResultItemId, SampleId = IntendedResultsSampleId, SampleTestId = IntendedResultsSampleTestId, TestResultItemId = IntendedResultsResultItemId }]);
        return repository;
    }

    [Fact]
    public async Task GetIntendedResults_UnknownScheme_ReturnsNotFound()
    {
        var controller = CreateController(new FakeDistributionRepository());

        var result = await controller.GetIntendedResults(IntendedResultsSchemeId, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    // Legacy Page_Load: "Samples numbers must be defined before setting the Intended Results".
    [Fact]
    public async Task GetIntendedResults_SampleNumbersNotYetDefined_ReturnsNotFound()
    {
        var repository = IntendedResultsRepository();
        repository.SchemesById[IntendedResultsSchemeId].HasSampleNumbersDefined = false;
        var controller = CreateController(repository);

        var result = await controller.GetIntendedResults(IntendedResultsSchemeId, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetIntendedResults_KnownScheme_ReturnsMappedResponse()
    {
        var controller = CreateController(IntendedResultsRepository());

        var result = await controller.GetIntendedResults(IntendedResultsSchemeId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<IntendedResultsResponse>(ok.Value);
        Assert.Equal(IntendedResultsSchemeId, response.MonthlyDistributionSchemeId);
        Assert.Equal("17152A/BA", response.DistributionReferenceFull);
        Assert.Equal("PT0060", response.SchemeIdentifier);
        Assert.False(response.RequiresAuditReason);

        var test = Assert.Single(response.Tests);
        Assert.Equal("Somatic cell count", test.TestName);
        var resultItem = Assert.Single(test.ResultItems);
        Assert.Equal(IntendedResultsResultItemId, resultItem.TestResultItemId);
        var methodItem = Assert.Single(test.MethodItems);
        Assert.Equal(IntendedResultsMethodItemId, methodItem.TestMethodItemId);
        var sample = Assert.Single(test.Samples);
        Assert.Equal(IntendedResultsSampleId, sample.SampleId);
        Assert.True(sample.IsApplicable);
        var value = Assert.Single(sample.Values);
        Assert.Equal(IntendedResultsSampleResultItemId, value.SampleResultItemId);
    }

    [Fact]
    public async Task SaveIntendedResults_InvalidModelState_ReturnsValidationProblem()
    {
        var controller = CreateControllerWithProblemDetails(IntendedResultsRepository());
        controller.ModelState.AddModelError("Tests", "The Tests field is required.");

        var request = new IntendedResultsSaveRequest([], false);
        var result = await controller.SaveIntendedResults(IntendedResultsSchemeId, request, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result.Result, exactMatch: false);
        Assert.IsType<ValidationProblemDetails>(objectResult.Value);
    }

    [Fact]
    public async Task SaveIntendedResults_Valid_ReturnsSuccessfulResultAndWritesTheChange()
    {
        var repository = IntendedResultsRepository();
        var controller = CreateController(repository);

        var request = new IntendedResultsSaveRequest(
        [
            new IntendedResultsTestRequest(
                IntendedResultsTestId,
                [],
                [new IntendedResultsMethodItemRequest(IntendedResultsMethodItemId, "42")],
                [new IntendedResultsSampleRowRequest(IntendedResultsSampleId, true, [])]),
        ], false);

        var result = await controller.SaveIntendedResults(IntendedResultsSchemeId, request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<IntendedResultsSaveResult>(ok.Value);
        Assert.True(response.Success);
        Assert.Empty(response.FieldErrorsByItemId);
        Assert.False(response.AuditReasonRequired);
        var saved = Assert.Single(repository.SavedIntendedResults);
        Assert.Equal("42", Assert.Single(saved.TestMethodItems).IntendedResult);
    }

    [Fact]
    public async Task SaveIntendedResults_InvalidValue_ReturnsFieldErrorsAndWritesNothing()
    {
        var repository = IntendedResultsRepository();
        var controller = CreateController(repository);

        var request = new IntendedResultsSaveRequest(
        [
            new IntendedResultsTestRequest(
                IntendedResultsTestId,
                [],
                [new IntendedResultsMethodItemRequest(IntendedResultsMethodItemId, "not-numeric")],
                [new IntendedResultsSampleRowRequest(IntendedResultsSampleId, true, [])]),
        ], false);

        var result = await controller.SaveIntendedResults(IntendedResultsSchemeId, request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<IntendedResultsSaveResult>(ok.Value);
        Assert.False(response.Success);
        var fieldErrors = Assert.Single(response.FieldErrorsByItemId);
        Assert.Equal(IntendedResultsMethodItemId, fieldErrors.Key);
        Assert.Contains(fieldErrors.Value, e => e.Field == "IntendedResult");
        Assert.Empty(repository.SavedIntendedResults);
    }

    // AuditWho is never accepted from the body - it is taken from the authenticated identity.
    [Fact]
    public async Task SaveIntendedResults_AuditWho_ComesFromTheAuthenticatedIdentity()
    {
        var repository = IntendedResultsRepository();
        var controller = CreateController(repository);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "APHA\\jbloggs")], "TestAuth")),
            }
        };

        var request = new IntendedResultsSaveRequest(
        [
            new IntendedResultsTestRequest(
                IntendedResultsTestId,
                [],
                [new IntendedResultsMethodItemRequest(IntendedResultsMethodItemId, "42")],
                [new IntendedResultsSampleRowRequest(IntendedResultsSampleId, true, [])]),
        ], false);

        await controller.SaveIntendedResults(IntendedResultsSchemeId, request, CancellationToken.None);

        Assert.Equal("APHA\\jbloggs", Assert.Single(repository.SavedIntendedResults).AuditWho);
    }

    [Fact]
    public async Task SaveIntendedResults_UnauthenticatedCaller_RecordsGuestAsAuditWho()
    {
        var repository = IntendedResultsRepository();
        var controller = CreateController(repository);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var request = new IntendedResultsSaveRequest(
        [
            new IntendedResultsTestRequest(
                IntendedResultsTestId,
                [],
                [new IntendedResultsMethodItemRequest(IntendedResultsMethodItemId, "42")],
                [new IntendedResultsSampleRowRequest(IntendedResultsSampleId, true, [])]),
        ], false);

        await controller.SaveIntendedResults(IntendedResultsSchemeId, request, CancellationToken.None);

        Assert.Equal("Guest", Assert.Single(repository.SavedIntendedResults).AuditWho);
    }

    [Fact]
    public async Task SaveIntendedResults_DistributionCannotBeReRead_ReturnsUnsuccessfulWithNoFieldErrors()
    {
        var controller = CreateController(new FakeDistributionRepository());

        var request = new IntendedResultsSaveRequest([], false);
        var result = await controller.SaveIntendedResults(IntendedResultsSchemeId, request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<IntendedResultsSaveResult>(ok.Value);
        Assert.False(response.Success);
        Assert.Empty(response.FieldErrorsByItemId);
    }

    private static readonly Guid ParticipantsSchemeId = Guid.Parse("bbbbbbbb-1111-1111-1111-111111111111");
    private static readonly Guid DistributionParticipantId = Guid.Parse("bbbbbbbb-2222-2222-2222-222222222222");

    private static FakeDistributionRepository ParticipantsRepository()
    {
        var repository = new FakeDistributionRepository();
        repository.ParticipantsByScheme[ParticipantsSchemeId] = new DistributionParticipantsData(
            new MonthlyDistributionSchemeEntity
            {
                MonthlyDistributionSchemeId = ParticipantsSchemeId,
                YearId = 2026,
                MonthId = 4,
                SchemeIdentifier = "PT0060",
                SchemeName = "Mastitis bovine",
                DistributionReference = "17152",
                DistributionReferenceSuffix = "A",
                ScheduleCode = "BA",
                DeadlineDate = new DateTime(2026, 4, 5),
            },
            [new DistributionParticipantEntity
            {
                DistributionParticipantId = DistributionParticipantId,
                ParticipantId = Guid.NewGuid(),
                LabCode = "L001",
                LabName = "Lab One",
                MonthlyDistributionSchemeId = ParticipantsSchemeId,
                DeadlineDate = new DateTime(2026, 4, 5),
            }],
            []);
        return repository;
    }

    [Fact]
    public async Task GetParticipants_UnknownScheme_ReturnsNotFound()
    {
        var controller = CreateController(new FakeDistributionRepository());

        var result = await controller.GetParticipants(ParticipantsSchemeId, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetParticipants_KnownScheme_ReturnsMappedResponse()
    {
        var controller = CreateController(ParticipantsRepository());

        var result = await controller.GetParticipants(ParticipantsSchemeId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<DistributionParticipantsResponse>(ok.Value);
        Assert.Equal(ParticipantsSchemeId, response.MonthlyDistributionSchemeId);
        Assert.Equal("17152A/BA", response.DistributionReferenceFull);
        Assert.True(response.IsRepeatDistribution);
        var participant = Assert.Single(response.Participants);
        Assert.Equal(DistributionParticipantId, participant.DistributionParticipantId);
        Assert.Equal("L001: Lab One", participant.FullName);
    }

    [Fact]
    public async Task ChangeDeadline_Valid_ReturnsSuccessfulResultAndWritesTheChange()
    {
        var repository = ParticipantsRepository();
        var controller = CreateController(repository);

        var result = await controller.ChangeDeadline(
            ParticipantsSchemeId, DistributionParticipantId, new ChangeDeadlineRequest(new DateTime(2026, 5, 1), "A reason"), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ChangeDeadlineResult>(ok.Value);
        Assert.True(response.Success);
        Assert.Single(repository.UpdatedParticipantDeadlines);
    }

    [Fact]
    public async Task ChangeDeadline_Invalid_EchoesBackTheValidationErrors()
    {
        var controller = CreateController(ParticipantsRepository());

        var result = await controller.ChangeDeadline(
            ParticipantsSchemeId, DistributionParticipantId, new ChangeDeadlineRequest(null, null), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ChangeDeadlineResult>(ok.Value);
        Assert.False(response.Success);
        Assert.Equal("Deadline Date required.", response.NewDeadlineDateError);
        Assert.Equal("A reason for changing the deadline date is required.", response.ReasonError);
    }

    [Fact]
    public async Task RepeatDistribution_Valid_ReturnsSuccessfulResultAndWritesANewScheme()
    {
        var repository = ParticipantsRepository();
        var controller = CreateController(repository);

        var request = new RepeatDistributionRequest([new ParticipantSelectionRequest(DistributionParticipantId, false)]);
        var result = await controller.RepeatDistribution(ParticipantsSchemeId, request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<RepeatDistributionResult>(ok.Value);
        Assert.True(response.Success);
        Assert.Single(repository.RepeatedDistributions);
    }

    [Fact]
    public async Task RepeatDistribution_NoSelections_ReturnsLegacyValidationMessage()
    {
        var controller = CreateController(ParticipantsRepository());

        var request = new RepeatDistributionRequest([]);
        var result = await controller.RepeatDistribution(ParticipantsSchemeId, request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<RepeatDistributionResult>(ok.Value);
        Assert.False(response.Success);
        Assert.Equal("Please tick at least one participant to be included in the repeat of this distribution.", response.Error);
    }

    private static readonly Guid PreparationMonthlyDistributionId = Guid.Parse("cccccccc-1111-1111-1111-111111111111");
    private static readonly Guid PreparationSchemeId = Guid.Parse("cccccccc-2222-2222-2222-222222222222");
    private static readonly Guid PreparationSampleId = Guid.Parse("cccccccc-3333-3333-3333-333333333333");

    private static FakeDistributionRepository PreparationRepository()
    {
        var repository = new FakeDistributionRepository();
        repository.Schedules[(2026, 4)] = (PreparationMonthlyDistributionId, [new MonthlyDistributionSchemeEntity { MonthlyDistributionSchemeId = PreparationSchemeId }]);
        repository.PreparationSheets[PreparationMonthlyDistributionId] = new PreparationSheetData(
            PreparationMonthlyDistributionId,
            2026,
            4,
            [
                new PreparationSchemeEntity
                {
                    MonthlyDistributionSchemeId = PreparationSchemeId,
                    SchemeIdentifier = "PT0060",
                    SchemeName = "Mastitis bovine",
                    DistributionReference = "17152",
                    DistributionReferenceSuffix = "A",
                    ScheduleCode = "BA",
                    NumberOfSetsRequired = 9,
                    SamplePackingInstructions = "Keep chilled",
                    Samples = [new PreparationSampleEntity { MonthlyDistributionSchemeId = PreparationSchemeId, SampleId = PreparationSampleId, SampleNumber = "S1", InternalDescription = "Internal 1" }],
                },
            ]);
        return repository;
    }

    [Fact]
    public async Task GetPreparation_MonthNotInitialised_ReturnsNotFound()
    {
        var controller = CreateController(new FakeDistributionRepository());

        var result = await controller.GetPreparation(2026, 4, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetPreparation_MonthInitialised_ReturnsMappedResponse()
    {
        var controller = CreateController(PreparationRepository());

        var result = await controller.GetPreparation(2026, 4, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<PreparationSheetResponse>(ok.Value);
        Assert.Equal(PreparationMonthlyDistributionId, response.MonthlyDistributionId);
        Assert.Equal(2026, response.YearId);
        Assert.Equal(4, response.MonthId);
        var scheme = Assert.Single(response.Schemes);
        Assert.Equal(PreparationSchemeId, scheme.MonthlyDistributionSchemeId);
        Assert.Equal("17152A/BA", scheme.DistributionReferenceFull);
        Assert.Equal(9, scheme.NumberOfSetsRequired);
        var sample = Assert.Single(scheme.Samples);
        Assert.Equal(PreparationSampleId, sample.SampleId);
        Assert.Equal("S1", sample.SampleNumber);
    }

    [Fact]
    public async Task SavePreparation_MonthInitialised_PersistsAndReturnsSuccess()
    {
        var repository = PreparationRepository();
        var controller = CreateController(repository);

        var request = new PreparationSaveRequest([new PreparationStageUpdateRequest(PreparationSchemeId, true, false, false)]);
        var result = await controller.SavePreparation(2026, 4, request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<PreparationSaveResult>(ok.Value);
        Assert.True(response.Success);
        Assert.Single(repository.UpdatedPreparationSchemes);
    }

    [Fact]
    public async Task SavePreparation_MonthNotInitialised_ReturnsNotFound()
    {
        var controller = CreateController(new FakeDistributionRepository());

        var request = new PreparationSaveRequest([new PreparationStageUpdateRequest(PreparationSchemeId, true, false, false)]);
        var result = await controller.SavePreparation(2026, 4, request, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }
}
