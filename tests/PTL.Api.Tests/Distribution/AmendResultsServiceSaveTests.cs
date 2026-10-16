using PTL.Contracts.Distribution;
using PTL.Core.Distribution;
using static PTL.Api.Tests.Distribution.AmendResultsTestData;

namespace PTL.Api.Tests.Distribution;

// AmendResultsService.SaveAsync - legacy BtnSave/BtnApply over MonthlyDistributionScheme.Save: only changed
// values are validated and written, and audited changes need a reason first (Audit.aspx).
public class AmendResultsServiceSaveTests
{
    private const string Who = "DOMAIN\\tc";

    private readonly FakeDistributionRepository _repository = new();

    public AmendResultsServiceSaveTests()
    {
        _repository.AmendResults[SchemeId] = Source();
        _repository.RatingOptions.AddRange(RatingOptions());
    }

    private AmendResultsService Service => new(_repository);

    private Task<AmendResultsSaveResult?> SaveAsync(AmendResultsSaveRequest request) => Service.SaveAsync(SchemeId, request, Who);

    private async Task<AmendResultsWrite> SavedAsync(AmendResultsSaveRequest request)
    {
        var result = await SaveAsync(request);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.False(result.AuditReasonRequired);
        Assert.Empty(result.FieldErrors);
        return Assert.Single(_repository.SavedAmendResults);
    }

    private async Task<IReadOnlyDictionary<string, string>> ErrorsAsync(AmendResultsSaveRequest request)
    {
        var result = await SaveAsync(request);

        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.False(result.AuditReasonRequired);
        Assert.Equal(string.Empty, result.AuditObjectName);
        Assert.Empty(_repository.SavedAmendResults);
        return result.FieldErrors;
    }

    private static AmendParticipantWrite OnlyParticipant(AmendResultsWrite write, Guid participantId)
    {
        var participant = Assert.Single(write.Participants);
        Assert.Equal(participantId, participant.DistributionParticipantId);
        return participant;
    }

    // ---- Guards -------------------------------------------------------------------------------

    [Fact]
    public async Task SaveAsync_NullRequest_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => Service.SaveAsync(SchemeId, null!, Who));
    }

    [Fact]
    public async Task SaveAsync_NoSource_ReturnsNullWithoutWriting()
    {
        Assert.Null(await Service.SaveAsync(Guid.NewGuid(), Request(auditWhy: "why"), Who));
        Assert.Empty(_repository.SavedAmendResults);
    }

    [Fact]
    public async Task SaveAsync_EverythingPostedBackUnchanged_SucceedsWithoutWriting()
    {
        var result = await SaveAsync(UnchangedRequest());

        Assert.True(result!.Success);
        Assert.False(result.AuditReasonRequired);
        Assert.Equal(string.Empty, result.AuditObjectName);
        Assert.Empty(result.FieldErrors);
        Assert.Empty(_repository.SavedAmendResults);
    }

    [Fact]
    public async Task SaveAsync_NullListsInTheRequest_AreTreatedAsNothingPosted()
    {
        var result = await SaveAsync(new AmendResultsSaveRequest(null!, null!, null!, null!, null!, null!, null!, null!, null!));

        Assert.True(result!.Success);
        Assert.Empty(_repository.SavedAmendResults);
    }

    [Fact]
    public async Task SaveAsync_ValuesForUnknownItemsParticipantsOrUnstoredRows_AreIgnored()
    {
        var unknown = Guid.NewGuid();
        var request = Request(
            [new(unknown, Guid.Empty, "1", null), new(MethodList, Guid.Empty, "Neg", null)],
            [new(unknown, SampleA, "1", null), new(ResultSingle, SampleB, "1", null)],
            [new(unknown, true)],
            [new(unknown, MethodRegex, Guid.Empty, "1"), new(Lab1, unknown, Guid.Empty, "1"), new(Lab3, MethodRegex, Guid.Empty, "1")],
            [new(unknown, ResultSingle, SampleA, "1"), new(Lab1, unknown, SampleA, "1"), new(Lab1, ResultSingle, SampleB, "1")],
            [new(unknown, Category, SampleA, 4), new(Lab1, unknown, SampleA, 4), new(Lab1, Category, SampleB, 4)],
            [new(unknown, Culture, SampleA, "x", 1), new(Lab1, Culture, SampleB, "x", 1)],
            [new(unknown, "x")],
            [new(unknown, SampleA, Rating1), new(Lab1, SampleB, Rating1)]);

        var result = await SaveAsync(request);

        Assert.True(result!.Success);
        Assert.Empty(_repository.SavedAmendResults);
    }

    // ---- Audit reason -------------------------------------------------------------------------

    public static TheoryData<string, AmendResultsSaveRequest> AuditableChanges => new()
    {
        { "intended method", Request(intendedMethods: [new(MethodRegex, Guid.Empty, "6", null)]) },
        { "intended result", Request(intendedResults: [new(ResultSingle, SampleA, "11", null)]) },
        { "participant method", Request(methodResults: [new(Lab1, MethodRegex, Guid.Empty, "8")]) },
        { "participant result", Request(resultItemResults: [new(Lab1, ResultSingle, SampleA, "13")]) },
        { "category", Request(categoryResults: [new(Lab1, Category, SampleA, 4)]) },
        { "assessment", Request(assessments: [new(Lab1, Culture, SampleA, "changed", 2)]) },
    };

    [Theory]
    [MemberData(nameof(AuditableChanges))]
    public async Task SaveAsync_AuditableChangeWithoutAReason_AsksForTheReasonWithoutWriting(string change, AmendResultsSaveRequest request)
    {
        var result = await SaveAsync(request);

        Assert.NotNull(result);
        Assert.False(result.Success, change);
        Assert.True(result.AuditReasonRequired, change);
        Assert.Equal(AuditObject, result.AuditObjectName);
        Assert.Empty(result.FieldErrors);
        Assert.Empty(_repository.SavedAmendResults);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SaveAsync_BlankReason_StillAsksForTheReason(string? auditWhy)
    {
        var result = await SaveAsync(Request(intendedMethods: [new(MethodRegex, Guid.Empty, "6", null)], auditWhy: auditWhy));

        Assert.True(result!.AuditReasonRequired);
        Assert.Empty(_repository.SavedAmendResults);
    }

    [Theory]
    [MemberData(nameof(AuditableChanges))]
    public async Task SaveAsync_AuditableChangeWithAReason_WritesWithTheAuditWhoAndWhy(string change, AmendResultsSaveRequest request)
    {
        var write = await SavedAsync(request with { AuditWhy = "Typo" });

        Assert.Equal((Who, "Typo"), (write.AuditWho, write.AuditWhy));
        Assert.True(write.IntendedMethods.Count + write.IntendedResults.Count + write.Participants.Count > 0, change);
    }

    public static TheoryData<string, AmendResultsSaveRequest> NonAuditableChanges => new()
    {
        { "range", Request(ranges: [new(ResultSingle, true)]) },
        { "nil return", Request(nilReturns: [new(Lab2, "Damaged")]) },
        { "rating", Request(ratings: [new(Lab1, SampleA, Rating1)]) },
    };

    [Theory]
    [MemberData(nameof(NonAuditableChanges))]
    public async Task SaveAsync_OnlyNonAuditableChanges_AreSavedWithoutAnAuditReason(string change, AmendResultsSaveRequest request)
    {
        var write = await SavedAsync(request);

        Assert.Equal(Who, write.AuditWho);
        Assert.Equal(string.Empty, write.AuditWhy);
        Assert.True(write.Ranges.Count + write.Participants.Count > 0, change);
    }

    [Fact]
    public async Task SaveAsync_OnlyNonAuditableChanges_IgnoreAPostedReason()
    {
        var write = await SavedAsync(Request(ratings: [new(Lab1, SampleA, Rating1)], auditWhy: "not needed"));

        Assert.Equal(string.Empty, write.AuditWhy);
    }

    // ---- Intended method ----------------------------------------------------------------------

    [Fact]
    public async Task SaveAsync_IntendedMethodChanged_WritesTheTestItemAndValue()
    {
        var write = await SavedAsync(Request(intendedMethods: [new(MethodRegex, Guid.Empty, "6", null)], auditWhy: "why"));

        Assert.Equal([new AmendIntendedMethodWrite(Culture, MethodRegex, "6")], write.IntendedMethods);
        Assert.Empty(write.IntendedResults);
        Assert.Empty(write.Ranges);
        Assert.Empty(write.Participants);
    }

    [Fact]
    public async Task SaveAsync_IntendedMethodCleared_IsAChangeAndAnEmptyValueIsNotFormatChecked()
    {
        var write = await SavedAsync(Request(intendedMethods: [new(MethodRegex, Guid.Empty, null, null)], auditWhy: "why"));

        Assert.Equal([new AmendIntendedMethodWrite(Culture, MethodRegex, "")], write.IntendedMethods);
    }

    [Fact]
    public async Task SaveAsync_IntendedMethodWrongFormat_IsKeyedToTheIntendedMethodInput()
    {
        var errors = await ErrorsAsync(Request(intendedMethods: [new(MethodRegex, Guid.Empty, "abc", null)], auditWhy: "why"));

        Assert.Equal(new Dictionary<string, string> { [AmendResultsFieldKeys.IntendedMethod(MethodRegex)] = "Input must be of format: Numeric" }, errors);
    }

    [Fact]
    public async Task SaveAsync_UnchangedValueInTheWrongFormat_IsNotValidated()
    {
        _repository.AmendResults[SchemeId].IntendedMethodResults[MethodRegex] = "legacy-text";

        var result = await SaveAsync(Request(intendedMethods: [new(MethodRegex, Guid.Empty, "legacy-text", null)]));

        Assert.True(result!.Success);
        Assert.Empty(_repository.SavedAmendResults);
    }

    // ---- Intended result + Range --------------------------------------------------------------

    [Fact]
    public async Task SaveAsync_IntendedResultChanged_NonRangeItemKeepsTheStoredSecondValue()
    {
        var write = await SavedAsync(Request(intendedResults: [new(ResultSingle, SampleA, "11", "99")], auditWhy: "why"));

        Assert.Equal([new AmendIntendedResultWrite(Culture, SampleA, ResultSingle, "11", true, "", false)], write.IntendedResults);
    }

    [Fact]
    public async Task SaveAsync_RangeItemSecondValueChanged_WritesOnlyTheSecondValueAsChanged()
    {
        var write = await SavedAsync(Request(intendedResults: [new(ResultRange, SampleA, "1", "8")], auditWhy: "why"));

        Assert.Equal([new AmendIntendedResultWrite(Culture, SampleA, ResultRange, "1", false, "8", true)], write.IntendedResults);
    }

    [Fact]
    public async Task SaveAsync_RangeItemWithoutAPostedSecondValue_KeepsTheStoredOne()
    {
        var write = await SavedAsync(Request(intendedResults: [new(ResultRange, SampleA, "2", null)], auditWhy: "why"));

        Assert.Equal([new AmendIntendedResultWrite(Culture, SampleA, ResultRange, "2", true, "9", false)], write.IntendedResults);
    }

    [Fact]
    public async Task SaveAsync_RangeTurnedOn_WritesTheRangeAndTakesThePostedSecondValue()
    {
        var write = await SavedAsync(Request(
            intendedResults: [new(ResultSingle, SampleA, "10", "20")], ranges: [new(ResultSingle, true)], auditWhy: "why"));

        Assert.Equal([new AmendRangeWrite(ResultSingle, Culture, ResultTypeId, 3, true)], write.Ranges);
        Assert.Equal([new AmendIntendedResultWrite(Culture, SampleA, ResultSingle, "10", false, "20", true)], write.IntendedResults);
        Assert.Equal("why", write.AuditWhy);
    }

    [Fact]
    public async Task SaveAsync_RangeTurnedOff_WritesTheRangeAndIgnoresThePostedSecondValue()
    {
        var write = await SavedAsync(Request(intendedResults: [new(ResultRange, SampleA, "1", "77")], ranges: [new(ResultRange, false)]));

        Assert.Equal([new AmendRangeWrite(ResultRange, Culture, ResultTypeId, 4, false)], write.Ranges);
        Assert.Empty(write.IntendedResults);
        Assert.Equal(string.Empty, write.AuditWhy);
    }

    [Fact]
    public async Task SaveAsync_RangeUnchanged_IsNotWritten()
    {
        var result = await SaveAsync(Request(ranges: [new(ResultSingle, false), new(ResultRange, true)]));

        Assert.True(result!.Success);
        Assert.Empty(_repository.SavedAmendResults);
    }

    [Fact]
    public async Task SaveAsync_IntendedResultWrongFormat_IsKeyedPerValue()
    {
        var errors = await ErrorsAsync(Request(intendedResults: [new(ResultRange, SampleA, "x", "y")], auditWhy: "why"));

        Assert.Equal(
            new Dictionary<string, string>
            {
                [AmendResultsFieldKeys.IntendedResult(SampleA, ResultRange, 1)] = "Input must be of format: Numeric",
                [AmendResultsFieldKeys.IntendedResult(SampleA, ResultRange, 2)] = "Input must be of format: Numeric",
            },
            errors);
    }

    [Fact]
    public async Task SaveAsync_DropDownFormatValues_AreNotValidated()
    {
        var write = await SavedAsync(Request(
            intendedResults: [new(ResultSerology, SampleC, "Anything", null)],
            methodResults: [new(Lab1, MethodList, Guid.Empty, "Whatever")],
            auditWhy: "why"));

        Assert.Equal([new AmendIntendedResultWrite(Serology, SampleC, ResultSerology, "Anything", true, "", false)], write.IntendedResults);
        Assert.Equal([new AmendMethodResultWrite(Culture, MethodList, "Whatever")], OnlyParticipant(write, Lab1).MethodResults);
    }

    // ---- Participant method / result ----------------------------------------------------------

    [Fact]
    public async Task SaveAsync_ParticipantMethodAndResultChanged_WriteUnderTheParticipantWithTheStoredNilReason()
    {
        var write = await SavedAsync(Request(
            methodResults: [new(Lab1, MethodRegex, Guid.Empty, "8")],
            resultItemResults: [new(Lab1, ResultSingle, SampleA, "13"), new(Lab1, ResultRange, SampleA, null)],
            auditWhy: "why"));

        var lab1 = OnlyParticipant(write, Lab1);
        Assert.Equal("stale", lab1.NilReturnReason);
        Assert.Equal([new AmendMethodResultWrite(Culture, MethodRegex, "8")], lab1.MethodResults);
        Assert.Equal(
            [new AmendResultItemWrite(Culture, SampleA, ResultSingle, "13"), new AmendResultItemWrite(Culture, SampleA, ResultRange, "")],
            lab1.ResultItemResults);
        Assert.Empty(lab1.Assessments);
        Assert.Empty(lab1.Ratings);
        Assert.Empty(lab1.CategoryResults);
    }

    [Fact]
    public async Task SaveAsync_ParticipantValuesWrongFormat_AreKeyedToTheParticipantInputs()
    {
        var errors = await ErrorsAsync(Request(
            methodResults: [new(Lab1, MethodRegex, Guid.Empty, "x")],
            resultItemResults: [new(Lab1, ResultSingle, SampleA, "y")],
            auditWhy: "why"));

        Assert.Equal(
            new Dictionary<string, string>
            {
                [AmendResultsFieldKeys.ParticipantMethod(Lab1, MethodRegex)] = "Input must be of format: Numeric",
                [AmendResultsFieldKeys.ParticipantResult(Lab1, SampleA, ResultSingle)] = "Input must be of format: Numeric",
            },
            errors);
    }

    [Fact]
    public async Task SaveAsync_AnyFieldError_BlocksEveryOtherValidChange()
    {
        var errors = await ErrorsAsync(Request(
            intendedMethods: [new(MethodRegex, Guid.Empty, "6", null)],
            methodResults: [new(Lab1, MethodRegex, Guid.Empty, "x")],
            ratings: [new(Lab1, SampleA, Rating1)],
            auditWhy: "why"));

        Assert.Equal([AmendResultsFieldKeys.ParticipantMethod(Lab1, MethodRegex)], errors.Keys);
    }

    // ---- Category -----------------------------------------------------------------------------

    [Fact]
    public async Task SaveAsync_CategoryScoreChanged_WritesItWithTheSampleTestId()
    {
        var write = await SavedAsync(Request(categoryResults: [new(Lab1, Category, SampleA, 5)], auditWhy: "why"));

        Assert.Equal([new AmendCategoryWrite(Culture, SampleA, Category, SampleTestId, 5)], OnlyParticipant(write, Lab1).CategoryResults);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    [InlineData(3)]
    public async Task SaveAsync_CategoryScoreOutOfRangeOrUnchanged_IsIgnored(int score)
    {
        var result = await SaveAsync(Request(categoryResults: [new(Lab1, Category, SampleA, score)]));

        Assert.True(result!.Success);
        Assert.Empty(_repository.SavedAmendResults);
    }

    // ---- Assessment ---------------------------------------------------------------------------

    [Fact]
    public async Task SaveAsync_AssessmentCommentAndScoreChanged_WritesBothFlags()
    {
        var write = await SavedAsync(Request(assessments: [new(Lab1, Culture, SampleA, "better", 5)], auditWhy: "why"));

        Assert.Equal([new AmendAssessmentWrite(Receipt1, "better", true, 5, true)], OnlyParticipant(write, Lab1).Assessments);
    }

    [Fact]
    public async Task SaveAsync_AssessmentScoreOnlyChanged_KeepsTheStoredComment()
    {
        var write = await SavedAsync(Request(assessments: [new(Lab3, Culture, SampleB, "late", 1)], auditWhy: "why"));

        var lab3 = OnlyParticipant(write, Lab3);
        Assert.Equal([new AmendAssessmentWrite(Receipt3, "late", false, 1, true)], lab3.Assessments);
        Assert.Equal(string.Empty, lab3.NilReturnReason);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public async Task SaveAsync_AssessmentScoreOutOfRange_KeepsTheStoredScoreButSavesTheComment(int score)
    {
        var write = await SavedAsync(Request(assessments: [new(Lab1, Culture, SampleA, null, score)], auditWhy: "why"));

        Assert.Equal([new AmendAssessmentWrite(Receipt1, "", true, 2, false)], OnlyParticipant(write, Lab1).Assessments);
    }

    [Fact]
    public async Task SaveAsync_AssessmentScoreOutOfRangeAndCommentUnchanged_IsIgnored()
    {
        var result = await SaveAsync(Request(assessments: [new(Lab1, Culture, SampleA, "ok", 9)]));

        Assert.True(result!.Success);
        Assert.Empty(_repository.SavedAmendResults);
    }

    [Fact]
    public async Task SaveAsync_AssessmentOnATestNotSubmittedForAssessment_IsIgnored()
    {
        var result = await SaveAsync(Request(assessments: [new(Lab1, Serology, SampleA, "new", 5)]));

        Assert.True(result!.Success);
        Assert.Empty(_repository.SavedAmendResults);
    }

    [Fact]
    public async Task SaveAsync_AssessmentCommentOver2000Characters_IsKeyedToTheCommentInput()
    {
        var errors = await ErrorsAsync(Request(assessments: [new(Lab1, Culture, SampleA, new string('x', 2001), 5)], auditWhy: "why"));

        Assert.Equal(
            new Dictionary<string, string>
            {
                [AmendResultsFieldKeys.AssessmentComment(Lab1, Culture, SampleA)] = "Input must be of format: The assessment comment cannot exceed 2000 characters",
            },
            errors);
    }

    [Fact]
    public async Task SaveAsync_AssessmentCommentOfExactly2000Characters_IsAccepted()
    {
        var comment = new string('x', 2000);

        var write = await SavedAsync(Request(assessments: [new(Lab1, Culture, SampleA, comment, 2)], auditWhy: "why"));

        Assert.Equal(comment, Assert.Single(OnlyParticipant(write, Lab1).Assessments).Comment);
    }

    // ---- Nil return ---------------------------------------------------------------------------

    [Fact]
    public async Task SaveAsync_NilReturnChanged_WritesTheNewReasonWithoutAudit()
    {
        var write = await SavedAsync(Request(nilReturns: [new(Lab2, "Damaged")]));

        var lab2 = OnlyParticipant(write, Lab2);
        Assert.Equal("Damaged", lab2.NilReturnReason);
        Assert.Empty(lab2.MethodResults);
        Assert.Empty(lab2.Ratings);
        Assert.Equal(string.Empty, write.AuditWhy);
    }

    [Fact]
    public async Task SaveAsync_NilReturnCleared_IsAChange()
    {
        var write = await SavedAsync(Request(nilReturns: [new(Lab2, null)]));

        Assert.Equal(string.Empty, OnlyParticipant(write, Lab2).NilReturnReason);
    }

    [Theory]
    [InlineData("Lost", "Damaged", "Damaged")]
    [InlineData("Damaged", "Lost", "Damaged")]
    [InlineData("First", "Second", "Second")]
    public async Task SaveAsync_RepeatedNilReturnBoxes_TheLastChangedOneWins(string first, string second, string expected)
    {
        var write = await SavedAsync(Request(nilReturns: [new(Lab2, first), new(Lab2, second)]));

        Assert.Equal(expected, OnlyParticipant(write, Lab2).NilReturnReason);
    }

    [Fact]
    public async Task SaveAsync_NilReturnForAParticipantWithResults_IsIgnored()
    {
        var result = await SaveAsync(Request(nilReturns: [new(Lab1, "x"), new(Lab3, "y")]));

        Assert.True(result!.Success);
        Assert.Empty(_repository.SavedAmendResults);
    }

    [Fact]
    public async Task SaveAsync_NilReturnOver100Characters_IsKeyedToTheNilReturnInput()
    {
        var errors = await ErrorsAsync(Request(nilReturns: [new(Lab2, new string('x', 101))]));

        Assert.Equal(
            new Dictionary<string, string> { [AmendResultsFieldKeys.NilReturn(Lab2)] = "Input must be of format: Less than 100 characters" },
            errors);
    }

    [Fact]
    public async Task SaveAsync_NilReturnOfExactly100Characters_IsAccepted()
    {
        var write = await SavedAsync(Request(nilReturns: [new(Lab2, new string('x', 100))]));

        Assert.Equal(100, OnlyParticipant(write, Lab2).NilReturnReason.Length);
    }

    // ---- Ratings ------------------------------------------------------------------------------

    [Fact]
    public async Task SaveAsync_RatingChangedToAKnownRating_WritesTheSampleRating()
    {
        var write = await SavedAsync(Request(ratings: [new(Lab1, SampleA, RatingNotScored)]));

        var lab1 = OnlyParticipant(write, Lab1);
        Assert.Equal([new AmendRatingWrite(SampleRating1, RatingNotScored)], lab1.Ratings);
        Assert.Equal("stale", lab1.NilReturnReason);
    }

    [Fact]
    public async Task SaveAsync_RatingThatIsUnknownUnchangedOrForANilReturn_IsIgnored()
    {
        var result = await SaveAsync(Request(ratings:
        [
            new(Lab1, SampleA, Guid.NewGuid()),
            new(Lab1, SampleA, Rating2),
            new(Lab2, SampleA, Rating1),
        ]));

        Assert.True(result!.Success);
        Assert.Empty(_repository.SavedAmendResults);
    }

    // ---- Combined -----------------------------------------------------------------------------

    [Fact]
    public async Task SaveAsync_EveryKindOfChange_IsCollectedIntoOneWriteInSourceParticipantOrder()
    {
        var write = await SavedAsync(Request(
            [new(MethodRegex, Guid.Empty, "6", null)],
            [new(ResultSingle, SampleA, "11", null)],
            [new(ResultSingle, true)],
            [new(Lab1, MethodRegex, Guid.Empty, "8")],
            [new(Lab1, ResultSingle, SampleA, "13")],
            [new(Lab1, Category, SampleA, 4)],
            [new(Lab3, Culture, SampleB, "later", 4), new(Lab1, Culture, SampleA, "ok", 3)],
            [new(Lab2, "Damaged")],
            [new(Lab1, SampleA, Rating0)],
            "Corrections"));

        Assert.Equal((Who, "Corrections"), (write.AuditWho, write.AuditWhy));
        Assert.Single(write.Ranges);
        Assert.Single(write.IntendedMethods);
        Assert.Single(write.IntendedResults);
        Assert.Equal([Lab1, Lab2, Lab3], write.Participants.Select(p => p.DistributionParticipantId));
        var lab1 = write.Participants[0];
        Assert.Single(lab1.MethodResults);
        Assert.Single(lab1.ResultItemResults);
        Assert.Single(lab1.CategoryResults);
        Assert.Equal([new AmendAssessmentWrite(Receipt1, "ok", false, 3, true)], lab1.Assessments);
        Assert.Single(lab1.Ratings);
        Assert.Equal("Damaged", write.Participants[1].NilReturnReason);
        Assert.Equal([new AmendAssessmentWrite(Receipt3, "later", true, 4, false)], write.Participants[2].Assessments);
    }
}
