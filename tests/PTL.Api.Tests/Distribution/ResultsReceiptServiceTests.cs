using PTL.Core.Distribution;

namespace PTL.Api.Tests.Distribution;

// Results Receipt rules from legacy ResultsReceipt.aspx / PtaBusinessObjects ResultsReceipt.* classes.
public class ResultsReceiptServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 12, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTime SchemeDeadline = new(2026, 6, 30);

    private readonly FakeDistributionRepository _repository = new();
    private readonly ResultsReceiptService _service;

    public ResultsReceiptServiceTests() => _service = new ResultsReceiptService(_repository, new FixedTimeProvider(Now));

    private static ResultsReceiptParticipantEntity Participant(
        DateTime? deadline = null, DateTime? first = null, DateTime? returned = null, bool extra = false, bool unaccounted = false) =>
        new()
        {
            DistributionParticipantId = Guid.NewGuid(),
            LabCode = "L001",
            LabName = "Lab One",
            DeadlineDate = deadline,
            FirstEditDate = first,
            ReturnedDate = returned,
            ExtraDocumentsReturned = extra,
            HasUnaccountedSamples = unaccounted,
        };

    private ResultsReceiptData Seed(bool requiresAssessment, params ResultsReceiptParticipantEntity[] participants)
    {
        var scheme = new ResultsReceiptSchemeEntity
        {
            MonthlyDistributionSchemeId = Guid.NewGuid(),
            SchemeIdentifier = "BT",
            SchemeName = "Brucella",
            DistributionReference = "D1",
            DistributionReferenceSuffix = "a",
            ScheduleCode = "S1",
            HasSampleNumbersDefined = true,
            DeadlineDate = SchemeDeadline,
            RequiresAssessment = requiresAssessment,
            Participants = [.. participants],
        };
        var data = new ResultsReceiptData(Guid.NewGuid(), 2026, 5, [scheme]);
        _repository.ResultsReceipts[(2026, 5)] = data;
        return data;
    }

    [Fact]
    public async Task Get_WhenNoDistribution_ReturnsNull() =>
        Assert.Null(await _service.GetResultsReceiptAsync(2026, 5));

    [Fact]
    public async Task Get_ParticipantWithoutDeadline_FallsBackToSchemeDeadline()
    {
        Seed(true, Participant(deadline: null), Participant(deadline: default(DateTime)), Participant(deadline: new DateTime(2026, 7, 1)));

        var result = await _service.GetResultsReceiptAsync(2026, 5);

        var deadlines = result!.Schemes[0].Participants.Select(p => p.DeadlineDate).ToList();
        Assert.Equal([SchemeDeadline, SchemeDeadline, new DateTime(2026, 7, 1)], deadlines);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task Get_UnaccountedSamples_OnlyShownForAssessmentSchemes(bool requiresAssessment, bool expected)
    {
        Seed(requiresAssessment, Participant(unaccounted: true));

        var result = await _service.GetResultsReceiptAsync(2026, 5);

        Assert.Equal(expected, result!.Schemes[0].Participants[0].HasUnaccountedSamples);
    }

    [Fact]
    public async Task Save_WhenNoDistribution_ReturnsFailureAndPersistsNothing()
    {
        var outcome = await _service.SaveResultsReceiptAsync(2026, 5, [new(Guid.NewGuid(), true, true)]);

        Assert.False(outcome.Success);
        Assert.Empty(_repository.UpdatedResultsReceiptParticipants);
    }

    [Fact]
    public async Task Save_ExtraDocumentsChanged_PersistsOnlyChangedParticipant()
    {
        var changed = Participant();
        var unchanged = Participant(extra: true);
        Seed(false, changed, unchanged);

        var outcome = await _service.SaveResultsReceiptAsync(2026, 5,
            [new(changed.DistributionParticipantId, true, false), new(unchanged.DistributionParticipantId, true, false)]);

        Assert.True(outcome.Success);
        var saved = Assert.Single(_repository.UpdatedResultsReceiptParticipants);
        Assert.Equal(changed.DistributionParticipantId, saved.DistributionParticipantId);
        Assert.True(saved.ExtraDocumentsReturned);
        Assert.Null(saved.ReturnedDate);
    }

    [Fact]
    public async Task Save_UntickingExtraDocuments_IsPersisted()
    {
        var participant = Participant(extra: true);
        Seed(false, participant);

        await _service.SaveResultsReceiptAsync(2026, 5, [new(participant.DistributionParticipantId, false, false)]);

        Assert.False(Assert.Single(_repository.UpdatedResultsReceiptParticipants).ExtraDocumentsReturned);
    }

    [Fact]
    public async Task Save_ReturnedTickedOnAssessmentScheme_StampsTodayWhenNotYetSet()
    {
        var participant = Participant();
        Seed(true, participant);

        await _service.SaveResultsReceiptAsync(2026, 5, [new(participant.DistributionParticipantId, false, true)]);

        Assert.Equal(new DateTime(2026, 5, 12), Assert.Single(_repository.UpdatedResultsReceiptParticipants).ReturnedDate);
    }

    [Fact]
    public async Task Save_ReturnedTickedWhenAlreadySet_KeepsExistingDateAndPersistsNothing()
    {
        var participant = Participant(returned: new DateTime(2026, 4, 1));
        Seed(true, participant);

        await _service.SaveResultsReceiptAsync(2026, 5, [new(participant.DistributionParticipantId, false, true)]);

        Assert.Empty(_repository.UpdatedResultsReceiptParticipants);
        Assert.Equal(new DateTime(2026, 4, 1), participant.ReturnedDate);
    }

    [Fact]
    public async Task Save_ReturnedTickedOnNonAssessmentScheme_IsIgnored()
    {
        var participant = Participant();
        Seed(false, participant);

        await _service.SaveResultsReceiptAsync(2026, 5, [new(participant.DistributionParticipantId, false, true)]);

        Assert.Empty(_repository.UpdatedResultsReceiptParticipants);
        Assert.Null(participant.ReturnedDate);
    }

    [Fact]
    public async Task Save_ReturnedNotTicked_DoesNotStampDate()
    {
        var participant = Participant();
        Seed(true, participant);

        await _service.SaveResultsReceiptAsync(2026, 5, [new(participant.DistributionParticipantId, false, false)]);

        Assert.Empty(_repository.UpdatedResultsReceiptParticipants);
    }

    [Fact]
    public async Task Save_UnknownParticipantIds_AreIgnored()
    {
        Seed(true, Participant());

        var outcome = await _service.SaveResultsReceiptAsync(2026, 5, [new(Guid.NewGuid(), true, true)]);

        Assert.True(outcome.Success);
        Assert.Empty(_repository.UpdatedResultsReceiptParticipants);
    }

    [Fact]
    public async Task Save_NothingChanged_DoesNotCallRepository()
    {
        var participant = Participant();
        Seed(true, participant);

        var outcome = await _service.SaveResultsReceiptAsync(2026, 5, []);

        Assert.True(outcome.Success);
        Assert.Empty(_repository.UpdatedResultsReceiptParticipants);
    }

    [Fact]
    public async Task Save_DuplicateIds_DoesNotThrowAndLastUpdateWins()
    {
        var participant = Participant();
        var other = Participant();
        Seed(true, participant, other);
        var id = participant.DistributionParticipantId;

        var outcome = await _service.SaveResultsReceiptAsync(2026, 5,
            [new(id, false, false), new(other.DistributionParticipantId, false, false), new(id, true, true)]);

        Assert.True(outcome.Success);
        var saved = Assert.Single(_repository.UpdatedResultsReceiptParticipants);
        Assert.Equal(id, saved.DistributionParticipantId);
        Assert.True(saved.ExtraDocumentsReturned);
        Assert.Equal(new DateTime(2026, 5, 12), saved.ReturnedDate);
    }

    [Fact]
    public async Task Save_DuplicateIds_LastUpdateRevertingToNoChangePersistsNothing()
    {
        var participant = Participant();
        Seed(true, participant);
        var id = participant.DistributionParticipantId;

        var outcome = await _service.SaveResultsReceiptAsync(2026, 5, [new(id, true, true), new(id, false, false)]);

        Assert.True(outcome.Success);
        Assert.Empty(_repository.UpdatedResultsReceiptParticipants);
        Assert.False(participant.ExtraDocumentsReturned);
        Assert.Null(participant.ReturnedDate);
    }

    [Fact]
    public async Task Save_ParticipantsAcrossSchemes_AreAllPersisted()
    {
        var first = Participant();
        var second = Participant();
        var seeded = Seed(false, first);
        var secondScheme = new ResultsReceiptSchemeEntity { RequiresAssessment = true, Participants = [second] };
        _repository.ResultsReceipts[(2026, 5)] = new ResultsReceiptData(seeded.MonthlyDistributionId, 2026, 5, [.. seeded.Schemes, secondScheme]);

        await _service.SaveResultsReceiptAsync(2026, 5,
            [new(first.DistributionParticipantId, true, false), new(second.DistributionParticipantId, false, true)]);

        Assert.Equal(2, _repository.UpdatedResultsReceiptParticipants.Count);
    }

    [Fact]
    public async Task Save_BothChanges_OnOneParticipantPersistedOnce()
    {
        var participant = Participant();
        Seed(true, participant);

        await _service.SaveResultsReceiptAsync(2026, 5, [new(participant.DistributionParticipantId, true, true)]);

        var saved = Assert.Single(_repository.UpdatedResultsReceiptParticipants);
        Assert.True(saved.ExtraDocumentsReturned);
        Assert.Equal(new DateTime(2026, 5, 12), saved.ReturnedDate);
    }
}

public class ResultsReceiptDataTests
{
    private static ResultsReceiptParticipantEntity P(DateTime? first) => new() { FirstEditDate = first };

    [Fact]
    public void Progress_NoParticipants_IsZero() =>
        Assert.Equal(0, new ResultsReceiptSchemeEntity().ProgressPercentage);

    [Theory]
    [InlineData(1, 8, 12)] // 12.5 -> banker's rounding to even (12), not 13
    [InlineData(3, 8, 38)] // 37.5 -> 38 (even)
    [InlineData(1, 3, 33)]
    [InlineData(2, 3, 67)]
    [InlineData(3, 3, 100)]
    [InlineData(0, 3, 0)]
    public void Progress_IsBankersRoundedShareWithFirstEdit(int withEdit, int total, int expected)
    {
        var scheme = new ResultsReceiptSchemeEntity
        {
            Participants = [.. Enumerable.Range(0, total).Select(i => P(i < withEdit ? DateTime.Today : null))],
        };

        Assert.Equal(expected, scheme.ProgressPercentage);
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public void CanViewResults_RequiresSampleNumbersAndPostedDate(bool sampleNumbers, bool posted, bool expected)
    {
        var scheme = new ResultsReceiptSchemeEntity { HasSampleNumbersDefined = sampleNumbers };
        var participant = new ResultsReceiptParticipantEntity { PostedDate = posted ? DateTime.Today : null };

        Assert.Equal(expected, scheme.CanViewResults(participant));
    }

    [Fact]
    public void Descriptions_FollowLegacyFormats()
    {
        var scheme = new ResultsReceiptSchemeEntity
        {
            DistributionReference = "D1",
            DistributionReferenceSuffix = "a",
            ScheduleCode = "S1",
            SchemeIdentifier = "BT",
            SchemeName = "Brucella",
        };

        Assert.Equal("D1a/S1", scheme.DistributionReferenceFull);
        Assert.Equal("D1a/S1: BT Brucella", scheme.Description);
        Assert.Equal("L001: Lab One", new ResultsReceiptParticipantEntity { LabCode = "L001", LabName = "Lab One" }.FullName);
    }
}
