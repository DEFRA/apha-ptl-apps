using PTL.Contracts.Distribution;
using PTL.Core.Distribution;
using static PTL.Api.Tests.Distribution.AmendResultsTestData;

namespace PTL.Api.Tests.Distribution;

// AmendResultsService.GetAsync - legacy Scheduling/AmendResults.aspx BuildTable + CalculateStats().
public class AmendResultsServiceGetTests
{
    private readonly FakeDistributionRepository _repository = new();

    private AmendResultsService Service => new(_repository);

    private async Task<AmendResultsResponse> GetAsync(AmendResultsSource source)
    {
        _repository.AmendResults[SchemeId] = source;
        _repository.RatingOptions.AddRange(RatingOptions());
        var page = await Service.GetAsync(SchemeId);
        Assert.NotNull(page);
        return page;
    }

    [Fact]
    public async Task GetAsync_NoSource_ReturnsNull()
    {
        Assert.Null(await Service.GetAsync(SchemeId));
    }

    [Fact]
    public async Task GetAsync_MapsTheHeaderTitleAndFlags()
    {
        var page = await GetAsync(Source());

        Assert.Equal(SchemeId, page.MonthlyDistributionSchemeId);
        Assert.Equal(Title, page.DistributionTitle);
        Assert.True(page.RequiresAssessment);
        Assert.True(page.StoreRatings);
        Assert.Equal(
            ["-1", "0", "1", "2", "Not Scored", "N/A"],
            page.RatingOptions.Select(o => o.Rating));
        Assert.Equal(RatingNotScored, page.RatingOptions[4].RatingId);
    }

    [Fact]
    public async Task GetAsync_TestsWithoutSamplesAreLeftOut_AndItemsAreMappedInOrder()
    {
        var page = await GetAsync(Source());

        Assert.Equal(["Culture", "Serology"], page.Tests.Select(t => t.Name));
        var culture = page.Tests[0];
        Assert.Equal(Culture, culture.TestId);
        Assert.True(culture.IsAssessmentSubmitted);
        Assert.False(page.Tests[1].IsAssessmentSubmitted);
        Assert.Equal(
            [new AmendItemResponse(MethodRegex, "Titre", "^[0-9]+$", "Numeric", false), new AmendItemResponse(MethodList, "Kit", "Pos|Neg", "List", false)],
            culture.MethodItems);
        Assert.Equal(
            [new AmendItemResponse(ResultSingle, "Count", "^[0-9]+$", "Numeric", false), new AmendItemResponse(ResultRange, "Span", "^[0-9]+$", "Numeric", true)],
            culture.ResultItems);
        Assert.Equal([new AmendCategoryItemResponse(Category, "Clinical")], culture.CategoryItems);
        Assert.Equal([new AmendSampleResponse(SampleA, "S02"), new AmendSampleResponse(SampleB, "S01")], culture.Samples);
    }

    [Fact]
    public async Task GetAsync_IntendedValues_OnlyWhereStored()
    {
        var page = await GetAsync(Source());

        var culture = page.Tests[0];
        Assert.Equal([new AmendIntendedValueResponse(MethodRegex, Guid.Empty, "5", string.Empty)], culture.IntendedMethodResults);
        Assert.Equal(
            [new AmendIntendedValueResponse(ResultSingle, SampleA, "10", ""), new AmendIntendedValueResponse(ResultRange, SampleA, "1", "9")],
            culture.IntendedResultItemResults);
        Assert.Empty(page.Tests[1].IntendedMethodResults);
        Assert.Equal([new AmendIntendedValueResponse(ResultSerology, SampleC, "Pos", "")], page.Tests[1].IntendedResultItemResults);
    }

    [Fact]
    public async Task GetAsync_Participants_CarryResultsAndTheLegacyResultFlags()
    {
        var page = await GetAsync(Source());

        Assert.Equal(["L001", "L002", "L003"], page.Participants.Select(p => p.LabCode));

        var lab1 = page.Participants[0];
        Assert.Equal((Lab1, "stale", true, true), (lab1.DistributionParticipantId, lab1.NilReturnReason, lab1.HasResults, lab1.HasRatingResults));
        Assert.Equal([(MethodRegex, Guid.Empty, "7"), (MethodList, Guid.Empty, "Pos")], lab1.MethodResults.Select(m => (m.ItemId, m.SampleId, m.Value)));
        Assert.Equal([(ResultSingle, SampleA, "12"), (ResultRange, SampleA, "3")], lab1.ResultItemResults.Select(r => (r.ItemId, r.SampleId, r.Value)));
        Assert.Equal([(Category, SampleA, 3)], lab1.CategoryResults.Select(c => (c.CategoryItemId, c.SampleId, c.Score)));
        Assert.Equal(new AmendAssessmentResponse(Culture, SampleA, "ok", 2), lab1.Assessments[0]);
        Assert.Equal([new AmendRatingResponse(SampleA, Rating2)], lab1.Ratings);

        // Nil return: no results at all, so the Nil Return box and no rating drop-down.
        var lab2 = page.Participants[1];
        Assert.Equal(("Lost", false, false), (lab2.NilReturnReason, lab2.HasResults, lab2.HasRatingResults));

        // Assessment rows alone count as results but do not give a rating drop-down.
        var lab3 = page.Participants[2];
        Assert.Equal((true, false), (lab3.HasResults, lab3.HasRatingResults));
    }

    [Fact]
    public async Task GetAsync_RatingSamples_AreDistinctAndOrdinallySortedBySampleNumber()
    {
        var source = Source();
        source.Tests[2].Samples.Add(new(Guid.NewGuid(), "s01"));

        var page = await GetAsync(source);

        Assert.Equal(["S01", "S02", "S10", "s01"], page.RatingSamples.Select(s => s.SampleNumber));
        Assert.Equal(SampleB, page.RatingSamples[0].SampleId);
        Assert.Equal(SampleA, page.RatingSamples[1].SampleId);
    }

    [Fact]
    public async Task GetAsync_StoreRatingsOff_HasNoStatistics()
    {
        var page = await GetAsync(Source(storeRatings: false));

        Assert.False(page.StoreRatings);
        Assert.NotEmpty(page.RatingSamples);
        Assert.Empty(page.Statistics);
    }

    [Fact]
    public async Task GetAsync_StoreRatings_HasStatisticsForEveryRatingSample()
    {
        var page = await GetAsync(Source());

        Assert.Equal(page.RatingSamples.Select(s => s.SampleId), page.Statistics.Select(s => s.SampleId));
        // SampleA: only Lab1 ("2") counts - Lab2 has a rating row but no method/result items.
        Assert.Equal(new AmendSampleStatisticsResponse(SampleA, "2.0", "1", "1", "100.0%"), page.Statistics[1]);
        Assert.Equal(new AmendSampleStatisticsResponse(SampleB, "0.0", "0", "0", "0.0%"), page.Statistics[0]);
    }

    // Port of legacy CalculateStats(): "2" is a lab reporting the intended result; -1/0/1/2 are counted and
    // summed; any "Not Scored" turns the mean, labs and percentage into "Not Scored"; other texts are ignored.
    [Theory]
    [InlineData(new[] { "2", "2", "1" }, "1.7", "2", "3", "66.7%")]
    [InlineData(new[] { "0", "0" }, "0.0", "0", "2", "0.0%")]
    [InlineData(new string[0], "0.0", "0", "0", "0.0%")]
    [InlineData(new[] { "2", "Not Scored" }, "Not Scored", "Not Scored", "2", "Not Scored")]
    [InlineData(new[] { "Not Scored" }, "Not Scored", "Not Scored", "1", "Not Scored")]
    [InlineData(new[] { "-1", "-1", "0", "0" }, "-0.5", "0", "4", "0.0%")]
    [InlineData(new[] { "N/A", "2" }, "2.0", "1", "1", "100.0%")]
    [InlineData(new[] { "1", "0", "0", "0" }, "0.3", "0", "4", "0.0%")]
    [InlineData(new[] { "2", "-1", "-1", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0" }, "0.0", "1", "16", "6.3%")]
    [InlineData(new[] { "-1", "1", "2" }, "0.7", "1", "3", "33.3%")]
    public async Task GetAsync_Statistics_PortLegacyCalculateStats(string[] ratings, string mean, string labs, string count, string percent)
    {
        var page = await GetAsync(StatisticsSource(ratings));

        Assert.Equal(new AmendSampleStatisticsResponse(SampleA, mean, labs, count, percent), Assert.Single(page.Statistics));
    }

    [Fact]
    public async Task GetAsync_Statistics_IgnoreParticipantsWithoutRatingResultsOrRatingRowOrKnownRating()
    {
        var source = StatisticsSource(["2"]);
        var noResults = new AmendSourceParticipant { DistributionParticipantId = Guid.NewGuid(), LabCode = "NR" };
        noResults.Ratings[SampleA] = (Guid.NewGuid(), Rating0);
        var noRatingRow = new AmendSourceParticipant { DistributionParticipantId = Guid.NewGuid(), LabCode = "NO" };
        noRatingRow.ResultItemResults[(SampleA, ResultSingle)] = "1";
        var unknownRating = new AmendSourceParticipant { DistributionParticipantId = Guid.NewGuid(), LabCode = "UK" };
        unknownRating.ResultItemResults[(SampleA, ResultSingle)] = "1";
        unknownRating.Ratings[SampleA] = (Guid.NewGuid(), Guid.NewGuid());
        source.Participants.AddRange([noResults, noRatingRow, unknownRating]);

        var page = await GetAsync(source);

        Assert.Equal(new AmendSampleStatisticsResponse(SampleA, "2.0", "1", "1", "100.0%"), Assert.Single(page.Statistics));
    }

    private static readonly Dictionary<string, Guid> RatingByText = RatingOptions().ToDictionary(r => r.Rating, r => r.RatingId);

    private static AmendResultsSource StatisticsSource(string[] ratings)
    {
        var source = new AmendResultsSource { MonthlyDistributionSchemeId = SchemeId, StoreRatings = true };
        var test = new AmendSourceTest { TestId = Culture, Name = "Culture" };
        test.MethodItems.Add(new(MethodRegex, "Titre", "^.*$", "Any"));
        test.Samples.Add(new(SampleA, "S01"));
        source.Tests.Add(test);

        foreach (var rating in ratings)
        {
            var participant = new AmendSourceParticipant { DistributionParticipantId = Guid.NewGuid(), LabCode = rating };
            participant.MethodResults[MethodRegex] = "x";
            participant.Ratings[SampleA] = (Guid.NewGuid(), RatingByText[rating]);
            source.Participants.Add(participant);
        }

        return source;
    }
}
