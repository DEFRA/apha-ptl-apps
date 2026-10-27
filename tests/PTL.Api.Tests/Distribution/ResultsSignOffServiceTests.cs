using Microsoft.AspNetCore.Mvc;
using PTL.Api.Controllers;
using PTL.Api.Tests.Audit;
using PTL.Contracts.Distribution;
using PTL.Core.Distribution;

namespace PTL.Api.Tests.Distribution;

// Results Sign-off queue - legacy Results Sign-off/SignOff.aspx. The wording (title, availability) is legacy's.
public class ResultsSignOffServiceTests
{
    private sealed class FakeRepository : IResultsSignOffRepository
    {
        public List<ResultsSignOffMonthEntity> Months { get; } = [];

        public ResultsSignOffOutcome RejectOutcome { get; set; } = ResultsSignOffOutcome.Done;

        public ResultsSignOffOutcome AuthoriseOutcome { get; set; } = ResultsSignOffOutcome.Done;

        public List<Guid> Rejected { get; } = [];

        public List<(Guid TabulationId, Guid? SignatoryUserId, DateTime CreationDate)> Authorised { get; } = [];

        public Task<IReadOnlyList<ResultsSignOffMonthEntity>> GetQueueAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ResultsSignOffMonthEntity>>(Months);

        public Task<ResultsSignOffOutcome> AuthoriseAsync(Guid tabulationId, Guid? signatoryUserId, DateTime creationDate, CancellationToken cancellationToken = default)
        {
            Authorised.Add((tabulationId, signatoryUserId, creationDate));
            return Task.FromResult(AuthoriseOutcome);
        }

        public Task<ResultsSignOffOutcome> RejectAsync(Guid tabulationId, CancellationToken cancellationToken = default)
        {
            Rejected.Add(tabulationId);
            return Task.FromResult(RejectOutcome);
        }
    }

    // A clock two hours ahead of UTC, so local time and UTC are distinguishable.
    private sealed class FixedLocalTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 26, 11, 30, 0, TimeSpan.Zero);

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.CreateCustomTimeZone("Test", TimeSpan.FromHours(2), "Test", "Test");
    }

    private readonly FakeRepository _repository = new();
    private readonly FakeDistributionRepository _distribution = new();
    private readonly FakeActingUserAccessor _actingUser = new() { ActingUserId = Guid.NewGuid() };

    private ResultsSignOffService Service => new(_repository, new FixedLocalTime());

    private ResultsSignOffController Controller => new(Service, new TabulationReportService(_distribution), _actingUser);

    [Fact]
    public async Task Controller_GetReview_UnknownTabulation_IsNotFound()
    {
        var result = await Controller.GetReview(Guid.NewGuid(), Guid.Empty, Guid.Empty, null, false, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Controller_GetReview_ReturnsTheDraftWithoutTheHiddenSamples()
    {
        var id = Guid.NewGuid();
        var source = new TabulationSource { TabulationId = id };
        var test = new TabulationSourceTest { TestId = Guid.NewGuid(), Name = "Culture" };
        test.Samples.Add(new(Guid.NewGuid(), null, "26/1", "One"));
        test.Samples.Add(new(Guid.NewGuid(), null, "26/2", "Two"));
        test.ResultItems.Add(new(Guid.NewGuid(), "Isolate"));
        source.Tests.Add(test);
        _distribution.TabulationSources[id] = source;

        var result = await Controller.GetReview(id, Guid.Empty, Guid.Empty, ["26/2"], false, CancellationToken.None);

        var review = Assert.IsType<ResultsSignOffReviewResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        var cells = review.Report.Tables.SelectMany(t => t.Rows).SelectMany(r => r.Cells).Select(c => c.Text).ToList();
        Assert.Equal(["26/1", "26/2"], review.SampleNumbers);
        Assert.Contains("26/1", cells);
        Assert.DoesNotContain("26/2", cells);
    }

    private static ResultsSignOffTabulationEntity Tabulation(string name = "FINAL REPORT", int publishCount = 0, bool single = false, bool participants = true, bool viewers = true) =>
        new() { DistributionTabulationId = Guid.NewGuid(), Name = name, PublishCount = publishCount, SingleParticipantTabulation = single, AvailableToParticipants = participants, AvailableToViewers = viewers };

    private static ResultsSignOffMonthEntity Month(params ResultsSignOffTabulationEntity[] tabulations)
    {
        var month = new ResultsSignOffMonthEntity { YearId = 2026, MonthId = 6 };
        var scheme = new ResultsSignOffSchemeEntity
        {
            MonthlyDistributionSchemeId = Guid.NewGuid(),
            DistributionReference = "17208",
            DistributionReferenceSuffix = "/BA",
            ScheduleCode = "PT0074",
            SchemeIdentifier = "PM",
            SchemeName = "Pasteurella",
            TargetDate = new DateTime(2026, 7, 21),
            TestConsultants = "Jane Smith, John Doe",
            Assessors = "Ann Assessor",
        };
        scheme.Tabulations.AddRange(tabulations);
        month.Schemes.Add(scheme);
        return month;
    }

    [Fact]
    public async Task GetQueueAsync_NothingAwaitingSignOff_IsEmpty() =>
        Assert.Empty((await Service.GetQueueAsync()).Months);

    [Fact]
    public async Task GetQueueAsync_ComposesTheLegacyDistributionTitleAndCarriesTheSchemeDetails()
    {
        _repository.Months.Add(Month(Tabulation()));

        var queue = await Service.GetQueueAsync();

        var month = Assert.Single(queue.Months);
        Assert.Equal((2026, 6), (month.YearId, month.MonthId));
        var scheme = Assert.Single(month.Schemes);
        Assert.Equal("17208/BA/PT0074 PM: Pasteurella", scheme.DistributionTitle);
        Assert.Equal((new DateTime(2026, 7, 21), "Jane Smith, John Doe", "Ann Assessor"), (scheme.TargetDate, scheme.TestConsultants, scheme.Assessors));
    }

    [Theory]
    [InlineData(true, true, "All")]
    [InlineData(true, false, "Participants Only")]
    [InlineData(false, true, "Viewers and TCs Only")]
    [InlineData(false, false, "Viewers and TCs Only")]
    public async Task GetQueueAsync_UsesLegacyAvailabilityWording(bool participants, bool viewers, string expected)
    {
        _repository.Months.Add(Month(Tabulation(participants: participants, viewers: viewers)));

        var tabulation = Assert.Single((await Service.GetQueueAsync()).Months[0].Schemes[0].Tabulations);

        Assert.Equal(expected, tabulation.Availability);
    }

    [Fact]
    public async Task GetQueueAsync_CarriesNamePublishCountSingleParticipantAndTheAwaitingStatus()
    {
        var entity = Tabulation("INTERIM", publishCount: 3, single: true);
        _repository.Months.Add(Month(entity));

        var tabulation = Assert.Single((await Service.GetQueueAsync()).Months[0].Schemes[0].Tabulations);

        Assert.Equal((entity.DistributionTabulationId, "INTERIM", 3, true, "Awaiting Sign-off"),
            (tabulation.TabulationId, tabulation.Name, tabulation.PublishCount, tabulation.IsSingleParticipant, tabulation.Status));
    }

    [Fact]
    public async Task GetQueueAsync_KeepsTheRepositoryOrder()
    {
        _repository.Months.Add(new ResultsSignOffMonthEntity { YearId = 2026, MonthId = 5 });
        _repository.Months.Add(new ResultsSignOffMonthEntity { YearId = 2026, MonthId = 6 });

        Assert.Equal([5, 6], (await Service.GetQueueAsync()).Months.Select(m => m.MonthId));
    }

    [Fact]
    public async Task Controller_GetQueue_ReturnsTheQueue()
    {
        _repository.Months.Add(Month(Tabulation()));

        var result = await Controller.GetQueue(CancellationToken.None);

        var response = Assert.IsType<ResultsSignOffQueueResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Single(response.Months);
    }

    [Theory]
    [InlineData(ResultsSignOffOutcome.Done)]
    [InlineData(ResultsSignOffOutcome.NotFound)]
    [InlineData(ResultsSignOffOutcome.NotAwaiting)]
    public async Task RejectAsync_ReturnsTheRepositoryOutcome(ResultsSignOffOutcome outcome)
    {
        _repository.RejectOutcome = outcome;
        var id = Guid.NewGuid();

        Assert.Equal(outcome, await Service.RejectAsync(id));
        Assert.Equal([id], _repository.Rejected);
    }

    [Theory]
    [InlineData(ResultsSignOffOutcome.Done, typeof(NoContentResult))]
    [InlineData(ResultsSignOffOutcome.NotFound, typeof(NotFoundResult))]
    [InlineData(ResultsSignOffOutcome.NotAwaiting, typeof(ConflictResult))]
    public async Task Controller_Reject_MapsTheOutcomeToTheStatusCode(ResultsSignOffOutcome outcome, Type expected)
    {
        _repository.RejectOutcome = outcome;

        var result = await Controller.Reject(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType(expected, result);
    }

    [Theory]
    [InlineData(ResultsSignOffOutcome.Done)]
    [InlineData(ResultsSignOffOutcome.NotFound)]
    [InlineData(ResultsSignOffOutcome.NotAwaiting)]
    [InlineData(ResultsSignOffOutcome.NoSignatory)]
    public async Task AuthoriseAsync_ReturnsTheRepositoryOutcome(ResultsSignOffOutcome outcome)
    {
        _repository.AuthoriseOutcome = outcome;

        Assert.Equal(outcome, await Service.AuthoriseAsync(Guid.NewGuid(), Guid.NewGuid()));
    }

    [Fact]
    public async Task AuthoriseAsync_PassesTheSignatoryAndTheLocalTimeAsTheCreationDate()
    {
        var tabulationId = Guid.NewGuid();
        var signatory = Guid.NewGuid();

        await Service.AuthoriseAsync(tabulationId, signatory);

        // Legacy stamped DateTime.Now, so the published CreationDate is local time, not UTC.
        Assert.Equal((tabulationId, (Guid?)signatory, new DateTime(2026, 10, 26, 13, 30, 0)), Assert.Single(_repository.Authorised));
    }

    [Theory]
    [InlineData(ResultsSignOffOutcome.Done, typeof(NoContentResult), null)]
    [InlineData(ResultsSignOffOutcome.NotFound, typeof(NotFoundResult), null)]
    [InlineData(ResultsSignOffOutcome.NotAwaiting, typeof(ConflictResult), null)]
    [InlineData(ResultsSignOffOutcome.NoSignatory, typeof(StatusCodeResult), 403)]
    public async Task Controller_Authorise_MapsTheOutcomeToTheStatusCode(ResultsSignOffOutcome outcome, Type expected, int? status)
    {
        _repository.AuthoriseOutcome = outcome;

        var result = await Controller.Authorise(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType(expected, result);
        if (status is { } code)
        {
            Assert.Equal(code, ((StatusCodeResult)result).StatusCode);
        }
    }

    [Fact]
    public async Task Controller_Authorise_SignsAsTheActingUser()
    {
        var tabulationId = Guid.NewGuid();

        await Controller.Authorise(tabulationId, CancellationToken.None);

        var call = Assert.Single(_repository.Authorised);
        Assert.Equal((tabulationId, _actingUser.ActingUserId), (call.TabulationId, call.SignatoryUserId));
    }
}
