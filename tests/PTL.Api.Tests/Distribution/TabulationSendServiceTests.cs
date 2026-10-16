using PTL.Contracts.Distribution;
using PTL.Core.Distribution;
using static PTL.Api.Tests.Distribution.TabulationsTestData;

namespace PTL.Api.Tests.Distribution;

// Tabulations "Send" actions - staged in memory, written only on Save (legacy Tabulations.aspx
// GridViewMDS_RowCommand + ButtonSave_Click -> MonthlyDistribution.Save).
public class TabulationSendServiceTests
{
    private const string ToTestConsultant = nameof(TabulationCommand.SendToTestConsultant);
    private const string ForAssessment = nameof(TabulationCommand.SendForAssessment);
    private const string ToSignOff = nameof(TabulationCommand.SendToResultsSignOff);

    private static readonly Guid MonthlyDistributionId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly FakeDistributionRepository _repository = new();
    private readonly FakeTabulationEmailService _emails = new();
    private readonly TabulationSendService _service;

    public TabulationSendServiceTests() => _service = new TabulationSendService(_repository, _emails, Clock());

    private void SeedMonth(params TabulationsSchemeEntity[] schemes)
    {
        _repository.Schedules[(2026, 5)] = (MonthlyDistributionId, []);
        _repository.Tabulations[MonthlyDistributionId] = schemes;
    }

    private static TabulationStagedSendRequest Send(TabulationsSchemeEntity scheme, string command) => new(scheme.MonthlyDistributionSchemeId, command);

    private static TabulationsSchemeEntity AssessmentScheme(Action<TabulationsSchemeEntity>? configure = null) =>
        NewScheme(s =>
        {
            s.RequiresAssessment = true;
            s.AssessmentStatuses = [Status()];
            configure?.Invoke(s);
        });

    // ---- PreviewAsync ----

    [Fact]
    public async Task Preview_MonthMissing_ReturnsNull()
    {
        Assert.Null(await _service.PreviewAsync(2026, 5, []));
        Assert.Empty(_repository.TabulationRequests);
    }

    [Fact]
    public async Task Preview_NothingStaged_ReturnsTheStoredState()
    {
        var scheme = NewScheme();
        SeedMonth(scheme);

        var page = await _service.PreviewAsync(2026, 5, null!);

        Assert.NotNull(page);
        Assert.Equal((2026, 5), (page.YearId, page.MonthId));
        var mapped = Assert.Single(page.Schemes);
        Assert.Equal("Send To Test Consultant", mapped.Actions.SendToTestConsultant.Text);
        Assert.True(mapped.Actions.SendToTestConsultant.IsEnabled);
        Assert.Equal([MonthlyDistributionId], _repository.TabulationRequests);
    }

    [Fact]
    public async Task Preview_StagedSendToTestConsultant_ShowsTheSentStateWithoutWriting()
    {
        var scheme = NewScheme();
        SeedMonth(scheme);

        var page = await _service.PreviewAsync(2026, 5, [Send(scheme, ToTestConsultant)]);

        var action = page!.Schemes[0].Actions.SendToTestConsultant;
        Assert.Equal("Waiting to send Email 12/05/2026", action.Text);
        Assert.False(action.IsEnabled);
        Assert.Empty(_repository.TabulationSends);
    }

    [Fact]
    public async Task Preview_StagedSignOff_ShowsWaitingForSignOff()
    {
        var scheme = NewScheme();
        SeedMonth(scheme);

        var page = await _service.PreviewAsync(2026, 5, [Send(scheme, ToSignOff)]);

        var actions = page!.Schemes[0].Actions;
        Assert.Equal("Waiting for Sign-Off", actions.SendToResultsSignOff.Text);
        Assert.False(actions.SendToResultsSignOff.IsEnabled);
        Assert.Equal("Comments not submitted", actions.SendToTestConsultant.Text);
    }

    [Fact]
    public async Task Preview_UnavailableStagedSends_AreIgnored()
    {
        var notYet = NewScheme(s => s.DeadlineDate = Today);
        var open = NewScheme();
        SeedMonth(notYet, open);

        var page = await _service.PreviewAsync(
            2026,
            5,
            [
                Send(notYet, ToTestConsultant),
                new TabulationStagedSendRequest(Guid.NewGuid(), ToTestConsultant),
                Send(open, "Bogus"),
                Send(open, nameof(TabulationCommand.ViewComments)),
            ]);

        Assert.All(page!.Schemes, s => Assert.Equal("Send To Test Consultant", s.Actions.SendToTestConsultant.Text));
        Assert.Null(notYet.IsReadyForTestConsultant);
        Assert.Null(open.IsReadyForTestConsultant);
    }

    // ---- StageAsync ----

    [Fact]
    public async Task Stage_NullRequest_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _service.StageAsync(2026, 5, null!));
    }

    [Fact]
    public async Task Stage_MonthMissing_ReturnsNull()
    {
        Assert.Null(await _service.StageAsync(2026, 5, new TabulationStageRequest([], Guid.NewGuid(), ToTestConsultant)));
    }

    [Theory]
    [InlineData("Bogus")]
    [InlineData("")]
    public async Task Stage_UnknownCommand_ReturnsNull(string command)
    {
        var scheme = NewScheme();
        SeedMonth(scheme);

        Assert.Null(await _service.StageAsync(2026, 5, new TabulationStageRequest([], scheme.MonthlyDistributionSchemeId, command)));
    }

    [Theory]
    [InlineData(nameof(TabulationCommand.None))]
    [InlineData(nameof(TabulationCommand.ViewComments))]
    public async Task Stage_NonSendCommand_ReturnsNull(string command)
    {
        var scheme = NewScheme();
        SeedMonth(scheme);

        Assert.Null(await _service.StageAsync(2026, 5, new TabulationStageRequest([], scheme.MonthlyDistributionSchemeId, command)));
    }

    [Fact]
    public async Task Stage_SchemeMissing_ReturnsNull()
    {
        SeedMonth(NewScheme());

        Assert.Null(await _service.StageAsync(2026, 5, new TabulationStageRequest([], Guid.NewGuid(), ToTestConsultant)));
    }

    [Fact]
    public async Task Stage_DeadlineNotPassed_ReturnsNull()
    {
        var scheme = NewScheme(s => s.DeadlineDate = Today);
        SeedMonth(scheme);

        Assert.Null(await _service.StageAsync(2026, 5, new TabulationStageRequest([], scheme.MonthlyDistributionSchemeId, ToSignOff)));
    }

    [Fact]
    public async Task Stage_SendToTestConsultant_ReturnsTheSchemeAndTheLegacyQuestion()
    {
        var scheme = NewScheme();
        SeedMonth(scheme);

        var send = await _service.StageAsync(2026, 5, new TabulationStageRequest(null!, scheme.MonthlyDistributionSchemeId, ToTestConsultant));

        Assert.Equal(
            new TabulationSendResponse(
                scheme.MonthlyDistributionSchemeId, "BT: Brucella", "D1a/S1", ToTestConsultant, "Send To Test Consultant", TabulationSendService.SendToTestConsultantQuestion),
            send);
        Assert.Null(scheme.IsReadyForTestConsultant);
        Assert.Empty(_repository.TabulationSends);
    }

    [Fact]
    public async Task Stage_SendForAssessment_AsksTheAssessmentQuestion()
    {
        var scheme = AssessmentScheme();
        SeedMonth(scheme);

        var send = await _service.StageAsync(2026, 5, new TabulationStageRequest([], scheme.MonthlyDistributionSchemeId, ForAssessment));

        Assert.Equal((ForAssessment, "Send For Assessment"), (send!.Command, send.ActionText));
        Assert.Equal("Are you sure this Distribution is ready to be sent for assessment?", send.ConfirmationQuestion);
    }

    [Fact]
    public async Task Stage_SignOffWithSubmittedComments_AsksTheSignOffQuestion()
    {
        var scheme = NewScheme(s => s.IsCommentSubmitted = true);
        SeedMonth(scheme);

        var send = await _service.StageAsync(2026, 5, new TabulationStageRequest([], scheme.MonthlyDistributionSchemeId, ToSignOff));

        Assert.Equal("Are you sure this Distribution is ready to be sent for Results Sign-Off?", send!.ConfirmationQuestion);
    }

    [Fact]
    public async Task Stage_SignOffWithUnsubmittedComments_WarnsThatCommentsAreDiscarded()
    {
        var scheme = NewScheme();
        SeedMonth(scheme);

        var send = await _service.StageAsync(2026, 5, new TabulationStageRequest([], scheme.MonthlyDistributionSchemeId, ToSignOff));

        Assert.Equal(TabulationSendService.SendToResultsSignOffDiscardQuestion, send!.ConfirmationQuestion);
        Assert.StartsWith("Test Consultant Comments have not been Submitted", send.ConfirmationQuestion, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Stage_SignOffForAnAssessmentSchemeWithoutSubmittedComments_HasNoQuestion()
    {
        var scheme = AssessmentScheme();
        SeedMonth(scheme);

        var send = await _service.StageAsync(2026, 5, new TabulationStageRequest([], scheme.MonthlyDistributionSchemeId, ToSignOff));

        Assert.Equal(string.Empty, send!.ConfirmationQuestion);
        Assert.Equal("Send For Results Sign-Off", send.ActionText);
    }

    [Fact]
    public async Task Stage_SameSchemeSignedOffTwice_SecondIsUnavailable()
    {
        var scheme = NewScheme();
        SeedMonth(scheme);

        var send = await _service.StageAsync(2026, 5, new TabulationStageRequest([Send(scheme, ToSignOff)], scheme.MonthlyDistributionSchemeId, ToSignOff));

        Assert.Null(send);
    }

    [Fact]
    public async Task Stage_TestConsultantAfterAStagedSignOff_IsUnavailable()
    {
        var scheme = NewScheme();
        SeedMonth(scheme);

        var send = await _service.StageAsync(2026, 5, new TabulationStageRequest([Send(scheme, ToSignOff)], scheme.MonthlyDistributionSchemeId, ToTestConsultant));

        Assert.Null(send);
    }

    [Fact]
    public async Task Stage_SignOffAfterAStagedTestConsultantSend_IsAvailable()
    {
        var scheme = NewScheme();
        SeedMonth(scheme);

        var send = await _service.StageAsync(2026, 5, new TabulationStageRequest([Send(scheme, ToTestConsultant)], scheme.MonthlyDistributionSchemeId, ToSignOff));

        Assert.NotNull(send);
        Assert.Equal(ToSignOff, send.Command);
    }

    [Fact]
    public async Task Stage_ReSendAfterADecision_UsesTheReSendText()
    {
        var scheme = NewScheme(s => s.Tabulations = [Tabulation(publishState: (int)TabulationPublishState.Authorized)]);
        SeedMonth(scheme);

        var send = await _service.StageAsync(2026, 5, new TabulationStageRequest([], scheme.MonthlyDistributionSchemeId, ToSignOff));

        Assert.Equal("Re-Send For Results Sign-Off", send!.ActionText);
    }

    // ---- SaveAsync ----

    [Fact]
    public async Task Save_MonthMissing_ReturnsFalseWithoutWriting()
    {
        Assert.False(await _service.SaveAsync(2026, 5, []));
        Assert.Empty(_repository.TabulationSends);
    }

    [Fact]
    public async Task Save_NothingStaged_ReturnsTrueWithoutWriting()
    {
        SeedMonth(NewScheme());

        Assert.True(await _service.SaveAsync(2026, 5, null!));
        Assert.Empty(_repository.TabulationSends);
    }

    [Fact]
    public async Task Save_AnyStagedSendUnavailable_ReturnsFalseAndWritesNothing()
    {
        var open = NewScheme();
        var notYet = NewScheme(s => s.DeadlineDate = Today);
        SeedMonth(open, notYet);

        Assert.False(await _service.SaveAsync(2026, 5, [Send(open, ToTestConsultant), Send(notYet, ToTestConsultant)]));
        Assert.Empty(_repository.TabulationSends);
    }

    [Theory]
    [InlineData("Bogus")]
    [InlineData(nameof(TabulationCommand.ViewComments))]
    public async Task Save_UnknownCommand_ReturnsFalse(string command)
    {
        var scheme = NewScheme();
        SeedMonth(scheme);

        Assert.False(await _service.SaveAsync(2026, 5, [Send(scheme, command)]));
        Assert.Empty(_repository.TabulationSends);
    }

    [Fact]
    public async Task Save_UnknownScheme_ReturnsFalse()
    {
        SeedMonth(NewScheme());

        Assert.False(await _service.SaveAsync(2026, 5, [new TabulationStagedSendRequest(Guid.NewGuid(), ToTestConsultant)]));
        Assert.Empty(_repository.TabulationSends);
    }

    [Fact]
    public async Task Save_SameSchemeSignedOffTwice_ReturnsFalse()
    {
        var scheme = NewScheme();
        SeedMonth(scheme);

        Assert.False(await _service.SaveAsync(2026, 5, [Send(scheme, ToSignOff), Send(scheme, ToSignOff)]));
        Assert.Empty(_repository.TabulationSends);
    }

    [Fact]
    public async Task Save_SendToTestConsultant_WritesTodayOnlyForThatScheme()
    {
        var sent = NewScheme();
        var untouched = NewScheme();
        SeedMonth(untouched, sent);

        Assert.True(await _service.SaveAsync(2026, 5, [Send(sent, ToTestConsultant)]));

        var write = Assert.Single(Assert.Single(_repository.TabulationSends));
        Assert.Equal(sent.MonthlyDistributionSchemeId, write.MonthlyDistributionSchemeId);
        Assert.Equal(Today, write.IsReadyForTestConsultant);
        Assert.Null(write.IsReadyForResultsSignOff);
        Assert.Null(write.IsReadyForAssessor);
        Assert.False(write.SubmitComments);
        Assert.Empty(write.PublishStates);
    }

    [Fact]
    public async Task Save_SendForAssessment_SetsIsReadyForAssessor()
    {
        var scheme = AssessmentScheme();
        SeedMonth(scheme);

        Assert.True(await _service.SaveAsync(2026, 5, [Send(scheme, ForAssessment)]));

        var write = Assert.Single(Assert.Single(_repository.TabulationSends));
        Assert.Equal((scheme.MonthlyDistributionSchemeId, (DateTime?)null, (DateTime?)null, (DateTime?)Today, false), (write.MonthlyDistributionSchemeId, write.IsReadyForTestConsultant, write.IsReadyForResultsSignOff, write.IsReadyForAssessor, write.SubmitComments));
        Assert.Empty(write.PublishStates);
    }

    [Fact]
    public async Task Save_SignOff_SetsTheDateClearsTestConsultantSubmitsCommentsAndWritesOnlyChangedPublishStates()
    {
        var consultantTab = Guid.NewGuid();
        var awaitingTab = Guid.NewGuid();
        var authorisedTab = Guid.NewGuid();
        var scheme = NewScheme(s =>
        {
            s.TestConsultantTabulationId = consultantTab;
            s.Tabulations =
            [
                Tabulation("TC", (int)TabulationPublishState.AwaitingSignOff, id: consultantTab),
                Tabulation("Awaiting", (int)TabulationPublishState.AwaitingSignOff, id: awaitingTab),
                Tabulation("Authorised", (int)TabulationPublishState.Authorized, id: authorisedTab),
            ];
        });
        SeedMonth(scheme);

        Assert.True(await _service.SaveAsync(2026, 5, [Send(scheme, ToSignOff)]));

        var write = Assert.Single(Assert.Single(_repository.TabulationSends));
        Assert.Equal(scheme.MonthlyDistributionSchemeId, write.MonthlyDistributionSchemeId);
        Assert.Null(write.IsReadyForTestConsultant);
        Assert.Equal(Today, write.IsReadyForResultsSignOff);
        Assert.Null(write.IsReadyForAssessor);
        Assert.True(write.SubmitComments);
        Assert.Equal(
            [(consultantTab, (int)TabulationPublishState.NotAuthorized), (authorisedTab, (int)TabulationPublishState.AwaitingSignOff)],
            write.PublishStates);
    }

    [Fact]
    public async Task Save_SignOffWithSubmittedComments_DoesNotSubmitAgain()
    {
        var scheme = NewScheme(s => s.IsCommentSubmitted = true);
        SeedMonth(scheme);

        Assert.True(await _service.SaveAsync(2026, 5, [Send(scheme, ToSignOff)]));

        Assert.False(Assert.Single(Assert.Single(_repository.TabulationSends)).SubmitComments);
    }

    [Fact]
    public async Task Save_SignOffForAnAssessmentScheme_KeepsIsReadyForAssessor()
    {
        var assessorDate = Today.AddDays(-5);
        var scheme = AssessmentScheme(s => s.IsReadyForAssessor = assessorDate);
        SeedMonth(scheme);

        Assert.True(await _service.SaveAsync(2026, 5, [Send(scheme, ToSignOff)]));

        var write = Assert.Single(Assert.Single(_repository.TabulationSends));
        Assert.Equal(assessorDate, write.IsReadyForAssessor);
        Assert.Equal(Today, write.IsReadyForResultsSignOff);
    }

    [Fact]
    public async Task Save_AssessmentThenSignOffOnTheSameScheme_WritesOneCombinedRow()
    {
        var scheme = AssessmentScheme();
        SeedMonth(scheme);

        Assert.True(await _service.SaveAsync(2026, 5, [Send(scheme, ForAssessment), Send(scheme, ToSignOff)]));

        var write = Assert.Single(Assert.Single(_repository.TabulationSends));
        Assert.Equal((Today, Today), (write.IsReadyForAssessor!.Value, write.IsReadyForResultsSignOff!.Value));
    }

    [Fact]
    public async Task Save_TestConsultantThenSignOff_ClearsTheTestConsultantDate()
    {
        var scheme = NewScheme();
        SeedMonth(scheme);

        Assert.True(await _service.SaveAsync(2026, 5, [Send(scheme, ToTestConsultant), Send(scheme, ToSignOff)]));

        var write = Assert.Single(Assert.Single(_repository.TabulationSends));
        Assert.Null(write.IsReadyForTestConsultant);
        Assert.Equal(Today, write.IsReadyForResultsSignOff);
        Assert.True(write.SubmitComments);
    }

    [Fact]
    public async Task Save_SeveralSchemes_WritesOneRowPerChangedSchemeInPageOrderInOneCall()
    {
        var first = NewScheme();
        var unchanged = NewScheme();
        var last = NewScheme();
        SeedMonth(first, unchanged, last);

        Assert.True(await _service.SaveAsync(2026, 5, [Send(last, ToSignOff), Send(first, ToTestConsultant)]));

        var writes = Assert.Single(_repository.TabulationSends);
        Assert.Equal([first.MonthlyDistributionSchemeId, last.MonthlyDistributionSchemeId], writes.Select(w => w.MonthlyDistributionSchemeId));
    }

    [Fact]
    public async Task Save_ExistingDates_AreKeptBySendToTestConsultant()
    {
        // ??= : a date already stored (here the earlier assessor date) is never overwritten.
        var assessorDate = Today.AddDays(-2);
        var scheme = NewScheme(s => s.IsReadyForAssessor = assessorDate);
        SeedMonth(scheme);

        Assert.True(await _service.SaveAsync(2026, 5, [Send(scheme, ToTestConsultant)]));

        var write = Assert.Single(Assert.Single(_repository.TabulationSends));
        Assert.Equal((Today, assessorDate), (write.IsReadyForTestConsultant!.Value, write.IsReadyForAssessor!.Value));
    }

    // ---- SaveAsync: first Test Consultant email ----

    [Fact]
    public async Task Save_SendToTestConsultant_RequestsTheFirstEmailAfterTheRepositoryWrite()
    {
        var scheme = NewScheme();
        SeedMonth(scheme);
        var writesWhenRequested = -1;
        _emails.OnSendFirstEmails = () => writesWhenRequested = _repository.TabulationSends.Count;

        Assert.True(await _service.SaveAsync(2026, 5, [Send(scheme, ToTestConsultant)]));

        Assert.Equal([scheme.MonthlyDistributionSchemeId], Assert.Single(_emails.FirstEmailRequests));
        Assert.Equal(1, writesWhenRequested);
    }

    [Fact]
    public async Task Save_SeveralTestConsultantSends_RequestsExactlyThoseSchemesAndNotOthers()
    {
        var first = NewScheme();
        var assessment = AssessmentScheme();
        var signedOff = NewScheme();
        var second = NewScheme();
        SeedMonth(first, assessment, signedOff, second);

        Assert.True(await _service.SaveAsync(
            2026,
            5,
            [Send(first, ToTestConsultant), Send(assessment, ForAssessment), Send(signedOff, ToSignOff), Send(second, ToTestConsultant)]));

        Assert.Equal(
            [first.MonthlyDistributionSchemeId, second.MonthlyDistributionSchemeId],
            Assert.Single(_emails.FirstEmailRequests));
    }

    [Fact]
    public async Task Save_OnlyAssessmentAndSignOffSends_DoesNotRequestAnyEmail()
    {
        var assessment = AssessmentScheme();
        var signedOff = NewScheme();
        SeedMonth(assessment, signedOff);

        Assert.True(await _service.SaveAsync(2026, 5, [Send(assessment, ForAssessment), Send(signedOff, ToSignOff)]));

        Assert.Single(_repository.TabulationSends);
        Assert.Empty(_emails.FirstEmailRequests);
    }

    [Fact]
    public async Task Save_StagedSendUnavailable_DoesNotRequestAnyEmail()
    {
        var open = NewScheme();
        var notYet = NewScheme(s => s.DeadlineDate = Today);
        SeedMonth(open, notYet);

        Assert.False(await _service.SaveAsync(2026, 5, [Send(open, ToTestConsultant), Send(notYet, ToTestConsultant)]));

        Assert.Empty(_emails.FirstEmailRequests);
    }

    [Fact]
    public async Task Save_MonthMissing_DoesNotRequestAnyEmail()
    {
        Assert.False(await _service.SaveAsync(2026, 5, [new TabulationStagedSendRequest(Guid.NewGuid(), ToTestConsultant)]));

        Assert.Empty(_emails.FirstEmailRequests);
    }

    [Fact]
    public async Task Save_NothingStaged_DoesNotRequestAnyEmail()
    {
        SeedMonth(NewScheme());

        Assert.True(await _service.SaveAsync(2026, 5, []));

        Assert.Empty(_repository.TabulationSends);
        Assert.Empty(_emails.FirstEmailRequests);
    }

    [Fact]
    public async Task Save_RepositoryWriteFails_DoesNotRequestAnyEmail()
    {
        var scheme = NewScheme();
        SeedMonth(scheme);
        _repository.TabulationSendError = new InvalidOperationException("write failed");

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.SaveAsync(2026, 5, [Send(scheme, ToTestConsultant)]));

        Assert.Empty(_emails.FirstEmailRequests);
    }
}
