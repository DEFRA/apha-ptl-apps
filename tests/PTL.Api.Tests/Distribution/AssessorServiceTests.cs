using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using PTL.Api.Controllers;
using PTL.Api.Tests.Audit;
using PTL.Contracts.Distribution;
using PTL.Core.Distribution;

namespace PTL.Api.Tests.Distribution;

// Assessor menu and assessment items - legacy Assessor/MenuAssessor.aspx and AssessmentItemList.aspx. The wording (titles) is legacy's.
public class AssessorServiceTests
{
    private static readonly Guid AssessorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TestId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid SchemeId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid SampleId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid SampleTestId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid ParticipantId = Guid.Parse("66666666-6666-6666-6666-666666666666");

    private sealed class FakeRepository : IAssessorRepository
    {
        public List<int> Years { get; } = [];

        public Dictionary<int, List<AssessorMonthEntity>> Months { get; } = [];

        public AssessorAccess Access { get; set; } = AssessorAccess.Granted;

        public AssessorItemListEntity? ItemList { get; set; }

        public List<(Guid AssessorId, int YearId)> MonthRequests { get; } = [];

        public List<(Guid TestId, Guid AssessorId)> AccessRequests { get; } = [];

        public int ItemListReads { get; private set; }

        public AssessorAccess EntryAccess { get; set; } = AssessorAccess.Granted;

        public AssessorEntryEntity? Entry { get; set; }

        public AssessorSaveOutcome SaveOutcome { get; set; } = AssessorSaveOutcome.Saved;

        public AssessorSubmitOutcome SubmitOutcome { get; set; } = AssessorSubmitOutcome.Submitted;

        public List<(Guid TestId, Guid AssessorId, DateTime SubmittedDate)> Submits { get; } = [];

        public Task<AssessorSubmitOutcome> SubmitAsync(Guid testId, Guid assessorId, DateTime submittedDate, CancellationToken cancellationToken = default)
        {
            Submits.Add((testId, assessorId, submittedDate));
            return Task.FromResult(SubmitOutcome);
        }

        public List<AssessorEntrySave> Saved { get; } = [];

        public List<(Guid SampleTestId, Guid ParticipantId, Guid AssessorId)> EntryAccessRequests { get; } = [];

        public int EntryReads { get; private set; }

        public Task<AssessorAccess> GetEntryAccessAsync(Guid sampleTestId, Guid participantId, Guid assessorId, CancellationToken cancellationToken = default)
        {
            EntryAccessRequests.Add((sampleTestId, participantId, assessorId));
            return Task.FromResult(EntryAccess);
        }

        public Task<AssessorEntryEntity?> GetEntryAsync(Guid sampleTestId, Guid participantId, CancellationToken cancellationToken = default)
        {
            EntryReads++;
            return Task.FromResult(Entry);
        }

        public Task<AssessorSaveOutcome> SaveEntryAsync(AssessorEntrySave save, CancellationToken cancellationToken = default)
        {
            Saved.Add(save);
            return Task.FromResult(SaveOutcome);
        }

        public Task<IReadOnlyList<int>> GetYearsAsync(Guid assessorId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<int>>(Years);

        public Task<IReadOnlyList<AssessorMonthEntity>> GetMonthsAsync(Guid assessorId, int yearId, CancellationToken cancellationToken = default)
        {
            MonthRequests.Add((assessorId, yearId));
            return Task.FromResult<IReadOnlyList<AssessorMonthEntity>>(Months.GetValueOrDefault(yearId) ?? []);
        }

        public Task<AssessorAccess> GetTestAccessAsync(Guid testId, Guid assessorId, CancellationToken cancellationToken = default)
        {
            AccessRequests.Add((testId, assessorId));
            return Task.FromResult(Access);
        }

        public Task<AssessorItemListEntity?> GetItemListAsync(Guid testId, CancellationToken cancellationToken = default)
        {
            ItemListReads++;
            return Task.FromResult(ItemList);
        }
    }

    // A clock two hours ahead of UTC, so local time and UTC are distinguishable.
    private sealed class FixedLocalTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 26, 11, 30, 0, TimeSpan.Zero);

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.CreateCustomTimeZone("Test", TimeSpan.FromHours(2), "Test", "Test");
    }

    private readonly FakeRepository _repository = new();
    private readonly FakeActingUserAccessor _actingUser = new() { ActingUserId = AssessorId };

    private AssessorService Service => new(_repository, new FixedLocalTime());

    // ValidationProblem() resolves ProblemDetailsFactory from HttpContext.RequestServices, which a bare controller does not have.
    private AssessorController Controller => new(Service, _actingUser)
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { RequestServices = new ServiceCollection().AddMvc().Services.BuildServiceProvider() } },
    };

    private static AssessorMonthEntity Month(int yearId, int monthId, DateTime? submitted = null)
    {
        var month = new AssessorMonthEntity { YearId = yearId, MonthId = monthId };
        var distribution = new AssessorDistributionEntity
        {
            MonthlyDistributionSchemeId = SchemeId,
            DistributionReference = "14468",
            DistributionReferenceSuffix = "/A",
            ScheduleCode = "HI",
            SchemeIdentifier = "PT0166",
            SchemeName = "Cellular Pathology Technique (CPT)",
        };
        distribution.Tests.Add(new AssessorTestEntity(TestId, "Alcian blue / PAS", submitted));
        month.Distributions.Add(distribution);
        return month;
    }

    private static AssessorItemListEntity ItemList(DateTime? submitted = null, bool withItems = true)
    {
        var list = new AssessorItemListEntity
        {
            YearId = 2021,
            MonthId = 2,
            DistributionReference = "14468",
            DistributionReferenceSuffix = "/A",
            ScheduleCode = "HI",
            SchemeIdentifier = "PT0166",
            SchemeName = "Cellular Pathology Technique (CPT)",
            TestName = "Alcian blue / PAS",
            AssessmentSubmitted = submitted,
            AssessorName = "Mary Dexter",
        };
        var sample = new AssessorSampleEntity { SampleId = SampleId, SampleNumber = "21/7140" };
        if (withItems)
        {
            sample.Items.Add(new AssessorItemEntity(SampleTestId, ParticipantId, "1126", new DateTime(2021, 3, 15, 16, 1, 19)));
            sample.Items.Add(new AssessorItemEntity(SampleTestId, Guid.NewGuid(), "1385", null));
        }

        list.Samples.Add(sample);
        return list;
    }

    // ---- Menu -----------------------------------------------------------------------------------

    [Fact]
    public async Task GetMenuAsync_NoDistributionsAtAll_IsEmptyAndReadsNoMonths()
    {
        var menu = await Service.GetMenuAsync(AssessorId, 2021);

        Assert.Empty(menu.Years);
        Assert.Equal(0, menu.SelectedYear);
        Assert.Empty(menu.Months);
        Assert.Empty(_repository.MonthRequests);
    }

    [Fact]
    public async Task GetMenuAsync_SelectsTheLatestYearAndListsTheYearsOldestFirst()
    {
        _repository.Years.AddRange([2021, 2019, 2020, 2020]);
        _repository.Months[2021] = [Month(2021, 2)];

        var menu = await Service.GetMenuAsync(AssessorId, null);

        Assert.Equal([2019, 2020, 2021], menu.Years);
        Assert.Equal(2021, menu.SelectedYear);
        Assert.Equal([(AssessorId, 2021)], _repository.MonthRequests);
    }

    [Fact]
    public async Task GetMenuAsync_ShowsTheYearAskedFor()
    {
        _repository.Years.AddRange([2019, 2020, 2021]);
        _repository.Months[2020] = [Month(2020, 5)];

        var menu = await Service.GetMenuAsync(AssessorId, 2020);

        Assert.Equal(2020, menu.SelectedYear);
        Assert.Equal(5, Assert.Single(menu.Months).MonthId);
    }

    [Fact]
    public async Task GetMenuAsync_AYearTheAssessorHasNothingIn_FallsBackToTheLatest()
    {
        _repository.Years.AddRange([2019, 2021]);

        var menu = await Service.GetMenuAsync(AssessorId, 2018);

        Assert.Equal(2021, menu.SelectedYear);
        Assert.Equal([(AssessorId, 2021)], _repository.MonthRequests);
    }

    [Fact]
    public async Task GetMenuAsync_ComposesTheLegacyDistributionTitleAndFlagsSubmittedTests()
    {
        _repository.Years.Add(2021);
        _repository.Months[2021] = [Month(2021, 2, submitted: new DateTime(2021, 3, 17))];

        var menu = await Service.GetMenuAsync(AssessorId, 2021);

        var distribution = Assert.Single(Assert.Single(menu.Months).Distributions);
        Assert.Equal("14468/A/HI: PT0166 Cellular Pathology Technique (CPT)", distribution.DistributionTitle);
        Assert.Equal(SchemeId, distribution.MonthlyDistributionSchemeId);
        var test = Assert.Single(distribution.Tests);
        Assert.Equal((TestId, "Alcian blue / PAS", true), (test.TestId, test.TestType, test.IsAssessmentSubmitted));
    }

    [Fact]
    public async Task GetMenuAsync_ATestNotYetSubmitted_IsNotFlagged()
    {
        _repository.Years.Add(2021);
        _repository.Months[2021] = [Month(2021, 2)];

        var menu = await Service.GetMenuAsync(AssessorId, 2021);

        Assert.False(Assert.Single(Assert.Single(Assert.Single(menu.Months).Distributions).Tests).IsAssessmentSubmitted);
    }

    [Fact]
    public async Task GetMenuAsync_KeepsTheRepositoryMonthOrder()
    {
        _repository.Years.Add(2021);
        _repository.Months[2021] = [Month(2021, 9), Month(2021, 2)];

        var menu = await Service.GetMenuAsync(AssessorId, 2021);

        Assert.Equal([9, 2], menu.Months.Select(m => m.MonthId));
    }

    // ---- Item list ------------------------------------------------------------------------------

    [Theory]
    [InlineData(AssessorAccess.Forbidden)]
    [InlineData(AssessorAccess.NotFound)]
    public async Task GetItemListAsync_NotAllowed_ReadsNothing(AssessorAccess access)
    {
        _repository.Access = access;
        _repository.ItemList = ItemList();

        var result = await Service.GetItemListAsync(AssessorId, TestId);

        Assert.Equal(access, result.Access);
        Assert.Null(result.Value);
        Assert.Equal([(TestId, AssessorId)], _repository.AccessRequests);
        Assert.Equal(0, _repository.ItemListReads);
    }

    [Fact]
    public async Task GetItemListAsync_TheTestIsNoLongerAvailable_IsNotFound()
    {
        var result = await Service.GetItemListAsync(AssessorId, TestId);

        Assert.Equal(AssessorAccess.NotFound, result.Access);
    }

    [Fact]
    public async Task GetItemListAsync_ComposesTheTitlesAndLabelsEachParticipantByLabCode()
    {
        _repository.ItemList = ItemList();

        var result = await Service.GetItemListAsync(AssessorId, TestId);

        var list = result.Value!;
        Assert.Equal((AssessorAccess.Granted, TestId, "February 2021", "14468/A/HI: PT0166 Cellular Pathology Technique (CPT)", "Alcian blue / PAS"),
            (result.Access, list.TestId, list.YearMonthTitle, list.DistributionTitle, list.TestName));
        var sample = Assert.Single(list.Samples);
        Assert.Equal(("21/7140", SampleId), (sample.SampleNumber, sample.SampleId));
        Assert.Equal(["1126", "1385"], sample.Items.Select(i => i.LabCode));
    }

    [Fact]
    public async Task GetItemListAsync_AnItemIsEnteredOnceItHasAnAssessmentDate()
    {
        _repository.ItemList = ItemList();

        var items = (await Service.GetItemListAsync(AssessorId, TestId)).Value!.Samples[0].Items;

        Assert.Equal([true, false], items.Select(i => i.HasAssessment));
        Assert.Equal((SampleTestId, ParticipantId), (items[0].SampleTestId, items[0].ParticipantId));
    }

    [Fact]
    public async Task GetItemListAsync_NotYetSubmitted_HasNoSubmissionDetails()
    {
        _repository.ItemList = ItemList();

        var list = (await Service.GetItemListAsync(AssessorId, TestId)).Value!;

        Assert.False(list.IsAssessmentSubmitted);
        Assert.Null(list.SubmittedDate);
    }

    [Fact]
    public async Task GetItemListAsync_Submitted_CarriesWhoAndWhen()
    {
        var submitted = new DateTime(2021, 3, 17, 13, 17, 39);
        _repository.ItemList = ItemList(submitted);

        var list = (await Service.GetItemListAsync(AssessorId, TestId)).Value!;

        Assert.True(list.IsAssessmentSubmitted);
        Assert.Equal(("Mary Dexter", (DateTime?)submitted), (list.SubmittedBy, list.SubmittedDate));
    }

    [Fact]
    public async Task GetItemListAsync_NoSamplesSubmitted_HasNoSamples()
    {
        var empty = ItemList();
        empty.Samples.Clear();
        _repository.ItemList = empty;

        Assert.Empty((await Service.GetItemListAsync(AssessorId, TestId)).Value!.Samples);
    }

    [Theory]
    [InlineData(1, "January 2026")]
    [InlineData(12, "December 2026")]
    [InlineData(0, "January 2026")]
    [InlineData(13, "December 2026")]
    public void YearMonthTitle_NamesTheMonthAndClampsTheMonthNumber(int monthId, string expected) =>
        Assert.Equal(expected, AssessorService.YearMonthTitle(monthId, 2026));

    // ---- Controller -----------------------------------------------------------------------------

    [Fact]
    public async Task Controller_GetMenu_WithoutAnActingUser_IsForbidden()
    {
        _actingUser.ActingUserId = null;

        var result = await Controller.GetMenu(2021, CancellationToken.None);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeResult>(result.Result).StatusCode);
        Assert.Empty(_repository.MonthRequests);
    }

    [Fact]
    public async Task Controller_GetMenu_ReturnsTheMenuOfTheActingUser()
    {
        _repository.Years.Add(2021);
        _repository.Months[2021] = [Month(2021, 2)];

        var result = await Controller.GetMenu(null, CancellationToken.None);

        var menu = Assert.IsType<AssessorMenuResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(2021, menu.SelectedYear);
        Assert.Equal([(AssessorId, 2021)], _repository.MonthRequests);
    }

    [Fact]
    public async Task Controller_GetItemList_WithoutAnActingUser_IsForbidden()
    {
        _actingUser.ActingUserId = null;

        var result = await Controller.GetItemList(TestId, CancellationToken.None);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeResult>(result.Result).StatusCode);
        Assert.Empty(_repository.AccessRequests);
    }

    [Fact]
    public async Task Controller_GetItemList_ForATestOfAnotherAssessor_IsForbidden()
    {
        _repository.Access = AssessorAccess.Forbidden;

        var result = await Controller.GetItemList(TestId, CancellationToken.None);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeResult>(result.Result).StatusCode);
    }

    [Fact]
    public async Task Controller_GetItemList_UnknownTest_IsNotFound()
    {
        _repository.Access = AssessorAccess.NotFound;

        Assert.IsType<NotFoundResult>((await Controller.GetItemList(TestId, CancellationToken.None)).Result);
    }

    [Fact]
    public async Task Controller_GetItemList_ReturnsTheItems()
    {
        _repository.ItemList = ItemList();

        var result = await Controller.GetItemList(TestId, CancellationToken.None);

        var list = Assert.IsType<AssessorItemListResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal("Alcian blue / PAS", list.TestName);
        Assert.Equal([(TestId, AssessorId)], _repository.AccessRequests);
    }

    // ---- Submit ---------------------------------------------------------------------------------

    [Fact]
    public async Task GetItemListAsync_EveryItemAssessed_IsComplete()
    {
        var list = ItemList();
        list.Samples[0].Items.Clear();
        list.Samples[0].Items.Add(new AssessorItemEntity(SampleTestId, ParticipantId, "1126", new DateTime(2021, 3, 15)));
        list.Samples[0].Items.Add(new AssessorItemEntity(SampleTestId, Guid.NewGuid(), "1385", new DateTime(2021, 3, 16)));
        _repository.ItemList = list;

        Assert.True((await Service.GetItemListAsync(AssessorId, TestId)).Value!.HasCompletedAssessment);
    }

    [Fact]
    public async Task GetItemListAsync_AnItemWithoutAnAssessment_IsNotComplete()
    {
        _repository.ItemList = ItemList();

        Assert.False((await Service.GetItemListAsync(AssessorId, TestId)).Value!.HasCompletedAssessment);
    }

    [Fact]
    public async Task GetItemListAsync_AnItemWithoutAnAssessmentInALaterSample_IsNotComplete()
    {
        var list = ItemList();
        list.Samples[0].Items.Clear();
        list.Samples[0].Items.Add(new AssessorItemEntity(SampleTestId, ParticipantId, "1126", new DateTime(2021, 3, 15)));
        var second = new AssessorSampleEntity { SampleId = Guid.NewGuid(), SampleNumber = "21/7141" };
        second.Items.Add(new AssessorItemEntity(SampleTestId, ParticipantId, "1126", null));
        list.Samples.Add(second);
        _repository.ItemList = list;

        Assert.False((await Service.GetItemListAsync(AssessorId, TestId)).Value!.HasCompletedAssessment);
    }

    [Fact]
    public async Task GetItemListAsync_NothingToAssess_IsCompleteAsInLegacy()
    {
        var list = ItemList();
        list.Samples.Clear();
        _repository.ItemList = list;

        Assert.True((await Service.GetItemListAsync(AssessorId, TestId)).Value!.HasCompletedAssessment);
    }

    [Theory]
    [InlineData(AssessorSubmitOutcome.Submitted)]
    [InlineData(AssessorSubmitOutcome.NotFound)]
    [InlineData(AssessorSubmitOutcome.Forbidden)]
    [InlineData(AssessorSubmitOutcome.AlreadySubmitted)]
    public async Task SubmitAsync_ReturnsTheRepositoryOutcome(AssessorSubmitOutcome outcome)
    {
        _repository.SubmitOutcome = outcome;

        Assert.Equal(outcome, await Service.SubmitAsync(AssessorId, TestId));
    }

    [Fact]
    public async Task SubmitAsync_PassesTheTestTheAssessorAndTheLocalTimeOn()
    {
        await Service.SubmitAsync(AssessorId, TestId);

        // Legacy stamped DateTime.Now, so the submission time is local time, not UTC.
        Assert.Equal((TestId, AssessorId, new DateTime(2026, 10, 26, 13, 30, 0)), Assert.Single(_repository.Submits));
    }

    [Fact]
    public async Task Controller_SubmitAssessment_WithoutAnActingUser_IsForbiddenAndSubmitsNothing()
    {
        _actingUser.ActingUserId = null;

        var result = await Controller.SubmitAssessment(TestId, CancellationToken.None);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeResult>(result).StatusCode);
        Assert.Empty(_repository.Submits);
    }

    [Fact]
    public async Task Controller_SubmitAssessment_SubmitsAsTheActingUser()
    {
        var result = await Controller.SubmitAssessment(TestId, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal(AssessorId, Assert.Single(_repository.Submits).AssessorId);
    }

    [Theory]
    [InlineData(AssessorSubmitOutcome.Forbidden, StatusCodes.Status403Forbidden)]
    [InlineData(AssessorSubmitOutcome.NotFound, StatusCodes.Status404NotFound)]
    [InlineData(AssessorSubmitOutcome.AlreadySubmitted, StatusCodes.Status409Conflict)]
    public async Task Controller_SubmitAssessment_MapsTheOutcomeToTheStatusCode(AssessorSubmitOutcome outcome, int status)
    {
        _repository.SubmitOutcome = outcome;

        var result = await Controller.SubmitAssessment(TestId, CancellationToken.None);

        Assert.Equal(status, Assert.IsAssignableFrom<IStatusCodeActionResult>(result).StatusCode);
    }

    // ---- Assessment entry -----------------------------------------------------------------------

    private static readonly Guid Morphology = Guid.Parse("b0000000-0000-0000-0000-000000000001");
    private static readonly Guid Staining = Guid.Parse("b0000000-0000-0000-0000-000000000002");
    private static readonly Guid Shape = Guid.Parse("c0000000-0000-0000-0000-000000000001");
    private static readonly Guid Colour = Guid.Parse("c0000000-0000-0000-0000-000000000002");
    private static readonly Guid Intensity = Guid.Parse("c0000000-0000-0000-0000-000000000004");

    private static AssessorEntryEntity Entry(DateTime? submitted = null, int score = 3, string comment = "Good")
    {
        var entry = new AssessorEntryEntity
        {
            SampleReceiptId = Guid.NewGuid(),
            TestId = TestId,
            YearId = 2021,
            MonthId = 2,
            DistributionReference = "14468",
            DistributionReferenceSuffix = "/A",
            ScheduleCode = "HI",
            SchemeIdentifier = "PT0166",
            SchemeName = "Cellular Pathology Technique (CPT)",
            TestName = "Alcian blue / PAS",
            LabCode = "1126",
            SampleNumber = "21/7140",
            Comment = comment,
            Score = score,
            AssessmentSubmitted = submitted,
        };
        var morphology = new AssessorCategoryEntity { CategoryItemId = Morphology, Name = "Morphology", ActualScore = 2 };
        morphology.Criteria.AddRange([new(Shape, "Shape", true), new(Colour, "Colour", false)]);
        var staining = new AssessorCategoryEntity { CategoryItemId = Staining, Name = "Staining" };
        staining.Criteria.Add(new(Intensity, "Intensity", false));
        entry.Categories.AddRange([morphology, staining]);
        entry.ResultItems.Add(new("Isolate", "Positive"));
        return entry;
    }

    private static AssessorEntrySaveRequest Request(string? comment = "Good", int overall = 3, int morphologyScore = 2, int stainingScore = 4, Guid[]? stainingCriteria = null) =>
        new(comment, overall, [new(Morphology, morphologyScore, [Shape]), new(Staining, stainingScore, stainingCriteria ?? [])]);

    [Theory]
    [InlineData(AssessorAccess.Forbidden)]
    [InlineData(AssessorAccess.NotFound)]
    public async Task GetEntryAsync_NotAllowed_ReadsNothing(AssessorAccess access)
    {
        _repository.EntryAccess = access;
        _repository.Entry = Entry();

        var result = await Service.GetEntryAsync(AssessorId, SampleTestId, ParticipantId);

        Assert.Equal(access, result.Access);
        Assert.Null(result.Value);
        Assert.Equal([(SampleTestId, ParticipantId, AssessorId)], _repository.EntryAccessRequests);
        Assert.Equal(0, _repository.EntryReads);
    }

    [Fact]
    public async Task GetEntryAsync_NoSuchSubmission_IsNotFound() =>
        Assert.Equal(AssessorAccess.NotFound, (await Service.GetEntryAsync(AssessorId, SampleTestId, ParticipantId)).Access);

    [Fact]
    public async Task GetEntryAsync_ComposesTheLegacyTitlesAndIdentifiesTheParticipantByLabCode()
    {
        _repository.Entry = Entry();

        var entry = (await Service.GetEntryAsync(AssessorId, SampleTestId, ParticipantId)).Value!;

        Assert.Equal((TestId, SampleTestId, ParticipantId, "February 2021", "14468/A/HI: PT0166 Cellular Pathology Technique (CPT)"),
            (entry.TestId, entry.SampleTestId, entry.ParticipantId, entry.YearMonthTitle, entry.DistributionTitle));
        Assert.Equal(("1126", "21/7140", "Alcian blue / PAS"), (entry.LabCode, entry.SampleNumber, entry.TestName));
        Assert.Equal(("Isolate", "Positive"), (Assert.Single(entry.ResultItems).Name, entry.ResultItems[0].Result));
    }

    [Fact]
    public async Task GetEntryAsync_CarriesTheSavedCommentScoresAndTickedCriteria()
    {
        _repository.Entry = Entry();

        var entry = (await Service.GetEntryAsync(AssessorId, SampleTestId, ParticipantId)).Value!;

        Assert.Equal(("Good", 3, false), (entry.Comment, entry.OverallScore, entry.IsAssessmentSubmitted));
        Assert.Equal([2, 0], entry.Categories.Select(c => c.Score));
        Assert.Equal(["Morphology", "Staining"], entry.Categories.Select(c => c.Name));
        Assert.Equal([true, false], entry.Categories[0].Criteria.Select(c => c.IsSelected));
        Assert.Equal(("Shape", Shape), (entry.Categories[0].Criteria[0].Name, entry.Categories[0].Criteria[0].CriterionItemId));
    }

    [Fact]
    public async Task GetEntryAsync_Submitted_IsFlagged()
    {
        _repository.Entry = Entry(submitted: new DateTime(2021, 3, 17));

        Assert.True((await Service.GetEntryAsync(AssessorId, SampleTestId, ParticipantId)).Value!.IsAssessmentSubmitted);
    }

    [Theory]
    [InlineData(AssessorAccess.Forbidden, AssessorSaveOutcome.Forbidden)]
    [InlineData(AssessorAccess.NotFound, AssessorSaveOutcome.NotFound)]
    public async Task SaveEntryAsync_NotAllowed_SavesNothing(AssessorAccess access, AssessorSaveOutcome expected)
    {
        _repository.EntryAccess = access;
        _repository.Entry = Entry();

        var result = await Service.SaveEntryAsync(AssessorId, SampleTestId, ParticipantId, Request());

        Assert.Equal(expected, result.Outcome);
        Assert.Empty(_repository.Saved);
        Assert.Equal(0, _repository.EntryReads);
    }

    [Fact]
    public async Task SaveEntryAsync_NoSuchSubmission_IsNotFound()
    {
        Assert.Equal(AssessorSaveOutcome.NotFound, (await Service.SaveEntryAsync(AssessorId, SampleTestId, ParticipantId, Request())).Outcome);
        Assert.Empty(_repository.Saved);
    }

    [Fact]
    public async Task SaveEntryAsync_TheTestIsAlreadySubmitted_SavesNothing()
    {
        _repository.Entry = Entry(submitted: new DateTime(2021, 3, 17));

        var result = await Service.SaveEntryAsync(AssessorId, SampleTestId, ParticipantId, Request());

        Assert.Equal(AssessorSaveOutcome.Submitted, result.Outcome);
        Assert.Empty(_repository.Saved);
    }

    [Fact]
    public async Task SaveEntryAsync_Valid_PassesTheValuesTheAssessorAndTheLocalTimeOn()
    {
        _repository.Entry = Entry();

        var result = await Service.SaveEntryAsync(AssessorId, SampleTestId, ParticipantId, Request(comment: "Fine work", overall: 4, stainingCriteria: [Intensity]));

        Assert.Equal(AssessorSaveOutcome.Saved, result.Outcome);
        var save = Assert.Single(_repository.Saved);
        Assert.Equal((SampleTestId, ParticipantId, AssessorId, "Fine work", 4), (save.SampleTestId, save.ParticipantId, save.AssessorUserId, save.Comment, save.OverallScore));
        // Legacy stamped DateTime.Now, so the assessment date is local time, not UTC.
        Assert.Equal(new DateTime(2026, 10, 26, 13, 30, 0), save.AssessmentDate);
        Assert.Equal([Morphology, Staining], save.Categories.Select(c => c.CategoryItemId));
        Assert.Equal([2, 4], save.Categories.Select(c => c.Score));
        Assert.Equal([Shape], save.Categories[0].CriterionItemIds);
        Assert.Equal([Intensity], save.Categories[1].CriterionItemIds);
    }

    [Theory]
    [InlineData(AssessorSaveOutcome.Forbidden)]
    [InlineData(AssessorSaveOutcome.Submitted)]
    [InlineData(AssessorSaveOutcome.NotFound)]
    public async Task SaveEntryAsync_TheRepositoryRefusesInsideTheTransaction_ReportsIt(AssessorSaveOutcome outcome)
    {
        _repository.Entry = Entry();
        _repository.SaveOutcome = outcome;

        Assert.Equal(outcome, (await Service.SaveEntryAsync(AssessorId, SampleTestId, ParticipantId, Request())).Outcome);
    }

    [Fact]
    public async Task SaveEntryAsync_ANullComment_IsSavedAsEmptyText()
    {
        _repository.Entry = Entry();

        await Service.SaveEntryAsync(AssessorId, SampleTestId, ParticipantId, Request(comment: null));

        Assert.Equal(string.Empty, Assert.Single(_repository.Saved).Comment);
    }

    [Theory]
    [InlineData(2000, true)]
    [InlineData(2001, false)]
    public async Task SaveEntryAsync_TheCommentHoldsTwoThousandCharacters(int length, bool valid)
    {
        _repository.Entry = Entry();

        var result = await Service.SaveEntryAsync(AssessorId, SampleTestId, ParticipantId, Request(comment: new string('x', length)));

        Assert.Equal(valid ? AssessorSaveOutcome.Saved : AssessorSaveOutcome.Invalid, result.Outcome);
        if (!valid)
        {
            Assert.Equal(("Comment", "The assessment comment cannot exceed 2000 characters"), (Assert.Single(result.Errors).Key, result.Errors[0].Message));
            Assert.Empty(_repository.Saved);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public async Task SaveEntryAsync_TheOverallScoreMustBeOneToFive(int overall)
    {
        _repository.Entry = Entry();

        var result = await Service.SaveEntryAsync(AssessorId, SampleTestId, ParticipantId, Request(overall: overall));

        Assert.Equal(AssessorSaveOutcome.Invalid, result.Outcome);
        Assert.Equal(("OverallScore", "An overall score is required"), (Assert.Single(result.Errors).Key, result.Errors[0].Message));
        Assert.Empty(_repository.Saved);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public async Task SaveEntryAsync_TheScoreLimitsAreAllowed(int score)
    {
        _repository.Entry = Entry();

        Assert.Equal(AssessorSaveOutcome.Saved, (await Service.SaveEntryAsync(AssessorId, SampleTestId, ParticipantId, Request(overall: score, morphologyScore: score, stainingScore: score))).Outcome);
    }

    [Fact]
    public async Task SaveEntryAsync_AnUnscoredCategory_IsNamedByItsPositionOnThePage()
    {
        _repository.Entry = Entry();

        var result = await Service.SaveEntryAsync(AssessorId, SampleTestId, ParticipantId, Request(stainingScore: 0));

        Assert.Equal(AssessorSaveOutcome.Invalid, result.Outcome);
        Assert.Equal(("Categories[1].Score", "A category score is required"), (Assert.Single(result.Errors).Key, result.Errors[0].Message));
    }

    [Fact]
    public async Task SaveEntryAsync_ACategoryTheRequestLeavesOut_NeedsAScore()
    {
        _repository.Entry = Entry();
        var request = new AssessorEntrySaveRequest("Good", 3, [new(Morphology, 2, [Shape])]);

        var result = await Service.SaveEntryAsync(AssessorId, SampleTestId, ParticipantId, request);

        Assert.Equal(["Categories[1].Score"], result.Errors.Select(e => e.Key));
    }

    [Fact]
    public async Task SaveEntryAsync_EveryProblemIsReportedAtOnce()
    {
        _repository.Entry = Entry();

        var result = await Service.SaveEntryAsync(AssessorId, SampleTestId, ParticipantId, Request(comment: new string('x', 2001), overall: 0, morphologyScore: 0, stainingScore: 9));

        Assert.Equal(["Comment", "OverallScore", "Categories[0].Score", "Categories[1].Score"], result.Errors.Select(e => e.Key));
    }

    [Fact]
    public async Task SaveEntryAsync_ACategoryOrCriterionOfAnotherTest_IsRefused()
    {
        _repository.Entry = Entry();
        var foreignCategory = new AssessorEntrySaveRequest("Good", 3, [new(Morphology, 2, [Shape]), new(Staining, 4, []), new(Guid.NewGuid(), 3, [])]);
        var foreignCriterion = new AssessorEntrySaveRequest("Good", 3, [new(Morphology, 2, [Guid.NewGuid()]), new(Staining, 4, [])]);
        var duplicate = new AssessorEntrySaveRequest("Good", 3, [new(Morphology, 2, [Shape]), new(Morphology, 3, [Shape]), new(Staining, 4, [])]);

        foreach (var request in new[] { foreignCategory, foreignCriterion, duplicate })
        {
            var result = await Service.SaveEntryAsync(AssessorId, SampleTestId, ParticipantId, request);

            Assert.Equal(AssessorSaveOutcome.Invalid, result.Outcome);
            Assert.Equal(("Categories", "The assessment does not match the categories and criteria of this test"), (Assert.Single(result.Errors).Key, result.Errors[0].Message));
        }

        Assert.Empty(_repository.Saved);
    }

    [Fact]
    public async Task SaveEntryAsync_NullRequest_Throws() =>
        await Assert.ThrowsAsync<ArgumentNullException>(() => Service.SaveEntryAsync(AssessorId, SampleTestId, ParticipantId, null!));

    [Fact]
    public async Task Controller_GetEntry_WithoutAnActingUser_IsForbidden()
    {
        _actingUser.ActingUserId = null;

        var result = await Controller.GetEntry(SampleTestId, ParticipantId, CancellationToken.None);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeResult>(result.Result).StatusCode);
        Assert.Empty(_repository.EntryAccessRequests);
    }

    [Theory]
    [InlineData(AssessorAccess.Forbidden, StatusCodes.Status403Forbidden)]
    [InlineData(AssessorAccess.NotFound, StatusCodes.Status404NotFound)]
    public async Task Controller_GetEntry_MapsTheAccessToTheStatusCode(AssessorAccess access, int status)
    {
        _repository.EntryAccess = access;

        var result = (await Controller.GetEntry(SampleTestId, ParticipantId, CancellationToken.None)).Result;

        Assert.Equal(status, Assert.IsAssignableFrom<IStatusCodeActionResult>(result).StatusCode);
    }

    [Fact]
    public async Task Controller_GetEntry_ReturnsTheEntry()
    {
        _repository.Entry = Entry();

        var result = await Controller.GetEntry(SampleTestId, ParticipantId, CancellationToken.None);

        Assert.Equal("1126", Assert.IsType<AssessorEntryResponse>(Assert.IsType<OkObjectResult>(result.Result).Value).LabCode);
    }

    [Fact]
    public async Task Controller_SaveEntry_WithoutAnActingUser_IsForbidden()
    {
        _actingUser.ActingUserId = null;

        var result = await Controller.SaveEntry(SampleTestId, ParticipantId, Request(), CancellationToken.None);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeResult>(result).StatusCode);
        Assert.Empty(_repository.Saved);
    }

    [Fact]
    public async Task Controller_SaveEntry_Saved_IsNoContentAndSignsAsTheActingUser()
    {
        _repository.Entry = Entry();

        var result = await Controller.SaveEntry(SampleTestId, ParticipantId, Request(), CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal(AssessorId, Assert.Single(_repository.Saved).AssessorUserId);
    }

    [Fact]
    public async Task Controller_SaveEntry_Invalid_IsBadRequestNamingTheFields()
    {
        _repository.Entry = Entry();

        var result = await Controller.SaveEntry(SampleTestId, ParticipantId, Request(overall: 0), CancellationToken.None);

        var body = Assert.IsType<ValidationProblemDetails>(Assert.IsType<ObjectResult>(result, exactMatch: false).Value);
        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsAssignableFrom<IStatusCodeActionResult>(result).StatusCode);
        var error = Assert.Single(body.Errors);
        Assert.Equal("OverallScore", error.Key);
        Assert.Equal(["An overall score is required"], error.Value);
    }

    [Theory]
    [InlineData(AssessorSaveOutcome.Forbidden, StatusCodes.Status403Forbidden)]
    [InlineData(AssessorSaveOutcome.Submitted, StatusCodes.Status409Conflict)]
    [InlineData(AssessorSaveOutcome.NotFound, StatusCodes.Status404NotFound)]
    public async Task Controller_SaveEntry_MapsTheOutcomeToTheStatusCode(AssessorSaveOutcome outcome, int status)
    {
        _repository.Entry = Entry();
        _repository.SaveOutcome = outcome;

        var result = await Controller.SaveEntry(SampleTestId, ParticipantId, Request(), CancellationToken.None);

        Assert.Equal(status, Assert.IsAssignableFrom<IStatusCodeActionResult>(result).StatusCode);
    }
}
