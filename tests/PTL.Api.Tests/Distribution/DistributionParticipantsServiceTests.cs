using PTL.Core.Distribution;

namespace PTL.Api.Tests.Distribution;

// Distribution Participants screen - legacy Scheduling/DistributionParticipantList.aspx. Covers
// GetParticipantsAsync passthrough, ChangeDeadlineAsync (SetNewDeadlineDate validation) and
// RepeatDistributionAsync (MonthlyDistribution.RepeatDistribution) business rules.
public class DistributionParticipantsServiceTests
{
    private static readonly Guid SchemeId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ParticipantId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid AdditionalParticipantSchemeId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static MonthlyDistributionSchemeEntity Scheme(bool isAsAvailable = false) => new()
    {
        MonthlyDistributionSchemeId = SchemeId,
        MonthlyDistributionId = Guid.NewGuid(),
        SchemeId = Guid.NewGuid(),
        YearId = 2026,
        MonthId = 4,
        SchemeIdentifier = "PT0060",
        SchemeName = "Mastitis bovine",
        DistributionReference = "17152",
        DistributionReferenceSuffix = string.Empty,
        ScheduleCode = "BA",
        DeadlineDate = new DateTime(2026, 4, 5),
        IsAsAvailable = isAsAvailable,
    };

    private static DistributionParticipantEntity Participant(Guid? distributionParticipantId = null, DateTime? deadlineDate = null) => new()
    {
        DistributionParticipantId = distributionParticipantId ?? ParticipantId,
        ParticipantId = Guid.NewGuid(),
        LabCode = "L001",
        LabName = "Lab One",
        MonthlyDistributionSchemeId = SchemeId,
        DeadlineDate = deadlineDate ?? new DateTime(2026, 4, 5),
        DeadlineChangeReason = string.Empty,
        ContractId = Guid.NewGuid(),
        ParticipantSchemeId = Guid.NewGuid(),
        AdditionalParticipant = false,
        NumberOfSetsRequired = 2,
    };

    private static AdditionalParticipantEntity Additional(Guid? participantSchemeId = null) => new()
    {
        ParticipantId = Guid.NewGuid(),
        LabCode = "L002",
        LabName = "Lab Two",
        ParticipantSchemeId = participantSchemeId ?? AdditionalParticipantSchemeId,
        ContractId = Guid.NewGuid(),
        NumberOfSetsRequired = 3,
    };

    private static (DistributionParticipantsService Service, FakeDistributionRepository Repository) CreateService(DistributionParticipantsData? data = null)
    {
        var repository = new FakeDistributionRepository();
        if (data is not null)
        {
            repository.ParticipantsByScheme[SchemeId] = data;
        }

        return (new DistributionParticipantsService(repository), repository);
    }

    // ---- GetParticipantsAsync ----

    [Fact]
    public async Task GetParticipantsAsync_KnownScheme_ReturnsDataFromRepository()
    {
        var data = new DistributionParticipantsData(Scheme(), [Participant()], [Additional()]);
        var (service, _) = CreateService(data);

        var result = await service.GetParticipantsAsync(SchemeId, CancellationToken.None);

        Assert.Same(data, result);
    }

    [Fact]
    public async Task GetParticipantsAsync_UnknownScheme_ReturnsNull()
    {
        var (service, _) = CreateService();

        Assert.Null(await service.GetParticipantsAsync(SchemeId, CancellationToken.None));
    }

    // ---- ChangeDeadlineAsync ----

    [Fact]
    public async Task ChangeDeadlineAsync_MissingDate_ReturnsDateErrorOnly()
    {
        var data = new DistributionParticipantsData(Scheme(), [Participant()], []);
        var (service, repository) = CreateService(data);

        var outcome = await service.ChangeDeadlineAsync(SchemeId, ParticipantId, null, "A good reason", CancellationToken.None);

        Assert.False(outcome.Success);
        Assert.Equal("Deadline Date required.", outcome.NewDeadlineDateError);
        Assert.Null(outcome.ReasonError);
        Assert.Empty(repository.UpdatedParticipantDeadlines);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task ChangeDeadlineAsync_MissingReason_ReturnsReasonErrorOnly(string? reason)
    {
        var data = new DistributionParticipantsData(Scheme(), [Participant()], []);
        var (service, repository) = CreateService(data);

        var outcome = await service.ChangeDeadlineAsync(SchemeId, ParticipantId, new DateTime(2026, 5, 1), reason, CancellationToken.None);

        Assert.False(outcome.Success);
        Assert.Null(outcome.NewDeadlineDateError);
        Assert.Equal("A reason for changing the deadline date is required.", outcome.ReasonError);
        Assert.Empty(repository.UpdatedParticipantDeadlines);
    }

    [Fact]
    public async Task ChangeDeadlineAsync_BothMissing_ReturnsBothErrors()
    {
        var data = new DistributionParticipantsData(Scheme(), [Participant()], []);
        var (service, repository) = CreateService(data);

        var outcome = await service.ChangeDeadlineAsync(SchemeId, ParticipantId, null, null, CancellationToken.None);

        Assert.False(outcome.Success);
        Assert.Equal("Deadline Date required.", outcome.NewDeadlineDateError);
        Assert.Equal("A reason for changing the deadline date is required.", outcome.ReasonError);
        Assert.Empty(repository.UpdatedParticipantDeadlines);
    }

    // CSLA StringMaxLength("DeadlineChangeReason", 255) on DistributionParticipant.vb.
    [Fact]
    public async Task ChangeDeadlineAsync_ReasonExceeds255Characters_ReturnsLengthError()
    {
        var data = new DistributionParticipantsData(Scheme(), [Participant()], []);
        var (service, repository) = CreateService(data);
        var reason = new string('x', 256);

        var outcome = await service.ChangeDeadlineAsync(SchemeId, ParticipantId, new DateTime(2026, 5, 1), reason, CancellationToken.None);

        Assert.False(outcome.Success);
        Assert.Null(outcome.NewDeadlineDateError);
        Assert.Equal("Input can not exceed 255 characters", outcome.ReasonError);
        Assert.Empty(repository.UpdatedParticipantDeadlines);
    }

    [Fact]
    public async Task ChangeDeadlineAsync_ReasonExactly255Characters_IsAccepted()
    {
        var data = new DistributionParticipantsData(Scheme(), [Participant()], []);
        var (service, repository) = CreateService(data);
        var reason = new string('x', 255);

        var outcome = await service.ChangeDeadlineAsync(SchemeId, ParticipantId, new DateTime(2026, 5, 1), reason, CancellationToken.None);

        Assert.True(outcome.Success);
        Assert.Single(repository.UpdatedParticipantDeadlines);
    }

    [Fact]
    public async Task ChangeDeadlineAsync_DistributionNotFound_ReturnsUnsuccessfulWithNoErrors()
    {
        var (service, repository) = CreateService();

        var outcome = await service.ChangeDeadlineAsync(SchemeId, ParticipantId, new DateTime(2026, 5, 1), "A good reason", CancellationToken.None);

        Assert.False(outcome.Success);
        Assert.Null(outcome.NewDeadlineDateError);
        Assert.Null(outcome.ReasonError);
        Assert.Empty(repository.UpdatedParticipantDeadlines);
    }

    [Fact]
    public async Task ChangeDeadlineAsync_ParticipantNotFound_ReturnsUnsuccessfulWithNoErrors()
    {
        var data = new DistributionParticipantsData(Scheme(), [Participant()], []);
        var (service, repository) = CreateService(data);

        var outcome = await service.ChangeDeadlineAsync(SchemeId, Guid.NewGuid(), new DateTime(2026, 5, 1), "A good reason", CancellationToken.None);

        Assert.False(outcome.Success);
        Assert.Null(outcome.NewDeadlineDateError);
        Assert.Null(outcome.ReasonError);
        Assert.Empty(repository.UpdatedParticipantDeadlines);
    }

    [Fact]
    public async Task ChangeDeadlineAsync_Success_PersistsNewDateAndReasonOnTheParticipant()
    {
        var participant = Participant();
        var data = new DistributionParticipantsData(Scheme(), [participant], []);
        var (service, repository) = CreateService(data);
        var newDate = new DateTime(2026, 5, 15);

        var outcome = await service.ChangeDeadlineAsync(SchemeId, ParticipantId, newDate, "Supplier delay", CancellationToken.None);

        Assert.True(outcome.Success);
        Assert.Null(outcome.NewDeadlineDateError);
        Assert.Null(outcome.ReasonError);
        var saved = Assert.Single(repository.UpdatedParticipantDeadlines);
        Assert.Equal(ParticipantId, saved.DistributionParticipantId);
        Assert.Equal(newDate, saved.DeadlineDate);
        Assert.Equal("Supplier delay", saved.DeadlineChangeReason);
    }

    // ---- RepeatDistributionAsync ----

    [Fact]
    public async Task RepeatDistributionAsync_NoSelections_ReturnsLegacyMessageAndWritesNothing()
    {
        var (service, repository) = CreateService();

        var outcome = await service.RepeatDistributionAsync(SchemeId, [], CancellationToken.None);

        Assert.False(outcome.Success);
        Assert.Equal("Please tick at least one participant to be included in the repeat of this distribution.", outcome.Error);
        Assert.Empty(repository.RepeatedDistributions);
    }

    [Fact]
    public async Task RepeatDistributionAsync_SchemeNotFound_ReturnsNotFoundErrorAndWritesNothing()
    {
        var (service, repository) = CreateService();

        var outcome = await service.RepeatDistributionAsync(SchemeId, [new ParticipantSelection(ParticipantId, false)], CancellationToken.None);

        Assert.False(outcome.Success);
        Assert.Equal("This distribution could not be found.", outcome.Error);
        Assert.Empty(repository.RepeatedDistributions);
    }

    // No other scheme in the month shares this DistributionReference yet - legacy ChrW(count + 96)
    // with count defaulting to 1 when the month schedule itself cannot be re-read.
    [Fact]
    public async Task RepeatDistributionAsync_FirstRepeat_SuffixIsA()
    {
        var data = new DistributionParticipantsData(Scheme(), [Participant()], []);
        var (service, repository) = CreateService(data);

        var outcome = await service.RepeatDistributionAsync(SchemeId, [new ParticipantSelection(ParticipantId, false)], CancellationToken.None);

        Assert.True(outcome.Success);
        var (newScheme, _) = Assert.Single(repository.RepeatedDistributions);
        Assert.Equal("a", newScheme.DistributionReferenceSuffix);
    }

    // Two schemes in the month already share "17152" (the original plus one prior repeat) -
    // ChrW(2 + 96) = 'b'.
    [Fact]
    public async Task RepeatDistributionAsync_SecondRepeat_SuffixIsB()
    {
        var data = new DistributionParticipantsData(Scheme(), [Participant()], []);
        var (service, repository) = CreateService(data);
        repository.Schedules[(2026, 4)] = (Guid.NewGuid(),
        [
            new MonthlyDistributionSchemeEntity { DistributionReference = "17152" },
            new MonthlyDistributionSchemeEntity { DistributionReference = "17152" },
        ]);

        var outcome = await service.RepeatDistributionAsync(SchemeId, [new ParticipantSelection(ParticipantId, false)], CancellationToken.None);

        Assert.True(outcome.Success);
        var (newScheme, _) = Assert.Single(repository.RepeatedDistributions);
        Assert.Equal("b", newScheme.DistributionReferenceSuffix);
    }

    // Schemes sharing the month but a DIFFERENT DistributionReference must not inflate the count.
    [Fact]
    public async Task RepeatDistributionAsync_OtherReferencesInTheMonth_AreNotCounted()
    {
        var data = new DistributionParticipantsData(Scheme(), [Participant()], []);
        var (service, repository) = CreateService(data);
        repository.Schedules[(2026, 4)] = (Guid.NewGuid(),
        [
            new MonthlyDistributionSchemeEntity { DistributionReference = "17152" },
            new MonthlyDistributionSchemeEntity { DistributionReference = "99999" },
        ]);

        var outcome = await service.RepeatDistributionAsync(SchemeId, [new ParticipantSelection(ParticipantId, false)], CancellationToken.None);

        Assert.True(outcome.Success);
        var (newScheme, _) = Assert.Single(repository.RepeatedDistributions);
        Assert.Equal("a", newScheme.DistributionReferenceSuffix);
    }

    [Fact]
    public async Task RepeatDistributionAsync_MixesOriginalAndAdditionalParticipantSelections()
    {
        var participant = Participant();
        var additional = Additional();
        var data = new DistributionParticipantsData(Scheme(), [participant], [additional]);
        var (service, repository) = CreateService(data);

        var selections = new List<ParticipantSelection>
        {
            new(participant.DistributionParticipantId, false),
            new(additional.ParticipantSchemeId, true),
        };

        var outcome = await service.RepeatDistributionAsync(SchemeId, selections, CancellationToken.None);

        Assert.True(outcome.Success);
        var (_, newParticipants) = Assert.Single(repository.RepeatedDistributions);
        Assert.Equal(2, newParticipants.Count);
        Assert.Contains(newParticipants, p => p.ParticipantId == participant.ParticipantId && !p.AdditionalParticipant);
        var fromAdditional = Assert.Single(newParticipants, p => p.AdditionalParticipant);
        Assert.Equal(additional.ParticipantId, fromAdditional.ParticipantId);
        Assert.Equal(additional.ParticipantSchemeId, fromAdditional.ParticipantSchemeId);
        Assert.Equal(additional.ContractId, fromAdditional.ContractId);
        Assert.Equal(additional.NumberOfSetsRequired, fromAdditional.NumberOfSetsRequired);
    }

    [Fact]
    public async Task RepeatDistributionAsync_UnknownSelectionIds_AreSkipped()
    {
        var data = new DistributionParticipantsData(Scheme(), [Participant()], [Additional()]);
        var (service, repository) = CreateService(data);

        var selections = new List<ParticipantSelection> { new(Guid.NewGuid(), false), new(Guid.NewGuid(), true) };

        var outcome = await service.RepeatDistributionAsync(SchemeId, selections, CancellationToken.None);

        Assert.True(outcome.Success);
        var (_, newParticipants) = Assert.Single(repository.RepeatedDistributions);
        Assert.Empty(newParticipants);
    }

    // Legacy NewMonthlyDistributionScheme: IsCancelled starts equal to IsAsAvailable.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RepeatDistributionAsync_NewScheme_IsCancelledDefaultsToIsAsAvailable(bool isAsAvailable)
    {
        var data = new DistributionParticipantsData(Scheme(isAsAvailable), [Participant()], []);
        var (service, repository) = CreateService(data);

        await service.RepeatDistributionAsync(SchemeId, [new ParticipantSelection(ParticipantId, false)], CancellationToken.None);

        var (newScheme, _) = Assert.Single(repository.RepeatedDistributions);
        Assert.Equal(isAsAvailable, newScheme.IsCancelled);
        Assert.Equal(isAsAvailable, newScheme.IsAsAvailable);
    }

    // New participants always take the NEW scheme's deadline, never the old participant's own
    // (possibly individually changed) deadline date - and start with no individual override
    // (legacy NewParticipant stores it into mMDSDeadlineDate, leaving mDeadlineDate empty).
    [Fact]
    public async Task RepeatDistributionAsync_NewParticipant_DeadlineDateComesFromNewSchemeNotOldParticipant()
    {
        var participant = Participant(deadlineDate: new DateTime(2026, 3, 1));
        var data = new DistributionParticipantsData(Scheme(), [participant], []);
        var (service, repository) = CreateService(data);

        await service.RepeatDistributionAsync(SchemeId, [new ParticipantSelection(ParticipantId, false)], CancellationToken.None);

        var (newScheme, newParticipants) = Assert.Single(repository.RepeatedDistributions);
        var newParticipant = Assert.Single(newParticipants);
        Assert.Null(newParticipant.DeadlineDate);
        Assert.Equal(newScheme.DeadlineDate, newParticipant.SchemeDeadlineDate);
        Assert.Equal(newScheme.DeadlineDate, newParticipant.EffectiveDeadlineDate);
        Assert.NotEqual(participant.DeadlineDate, newParticipant.EffectiveDeadlineDate);
    }

    [Fact]
    public async Task RepeatDistributionAsync_NewParticipant_GetsAFreshIdAndBlankDeadlineChangeReason()
    {
        var participant = Participant();
        participant.DeadlineChangeReason = "Old reason, must not carry over";
        var data = new DistributionParticipantsData(Scheme(), [participant], []);
        var (service, repository) = CreateService(data);

        await service.RepeatDistributionAsync(SchemeId, [new ParticipantSelection(ParticipantId, false)], CancellationToken.None);

        var (newScheme, newParticipants) = Assert.Single(repository.RepeatedDistributions);
        var newParticipant = Assert.Single(newParticipants);
        Assert.NotEqual(participant.DistributionParticipantId, newParticipant.DistributionParticipantId);
        Assert.Equal(newScheme.MonthlyDistributionSchemeId, newParticipant.MonthlyDistributionSchemeId);
        Assert.Equal(string.Empty, newParticipant.DeadlineChangeReason);
    }
}
