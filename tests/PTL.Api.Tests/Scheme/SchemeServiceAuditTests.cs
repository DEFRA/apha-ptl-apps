using Microsoft.Extensions.Logging.Abstractions;
using PTL.Api.Tests.Lookup;
using PTL.Api.Tests.Participant;
using PTL.Core.Scheme;
using static PTL.Api.Tests.Scheme.SchemeAuditFixture;
using CoreScheme = PTL.Core.Scheme.Scheme;

namespace PTL.Api.Tests.Scheme;

public class SchemeServiceAuditTests
{
    private static async Task<(SchemeService Service, FakeSchemeRepository Repository)> StoredSchemeAsync(CoreScheme? stored = null)
    {
        var repository = new FakeSchemeRepository();
        await repository.CreateAsync(stored ?? Build());
        var service = new SchemeService(repository, new FakeLookupRepository(), new FakeViewerRepository(), new FakeParticipantRepository(), NullLogger<SchemeService>.Instance);
        return (service, repository);
    }

    private static Task<SchemeUpdateResult> UpdateAsync(SchemeService service, CoreScheme updated, string? why = null, string who = "tester") =>
        service.UpdateSchemeAsync(SchemeId, updated, who, why);

    private static CoreScheme AuditableEdit()
    {
        var updated = Build();
        updated.Tests.RemoveAt(1);
        return updated;
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateSchemeAsync_AuditableChangeWithoutAReason_AsksForOneAndSavesNothing(string? reason)
    {
        var (service, repository) = await StoredSchemeAsync();

        var result = await UpdateAsync(service, AuditableEdit(), reason);

        Assert.Equal(SchemeUpdateStatus.AuditReasonRequired, result.Status);
        Assert.Equal("Scheme PT1234: Test Scheme", result.AuditObjectName);
        Assert.Null(result.Scheme);
        Assert.Empty(repository.AuditWrites);
        Assert.Equal(2, (await repository.GetByIdAsync(SchemeId))!.Tests.Count);
    }

    [Fact]
    public async Task UpdateSchemeAsync_AuditObjectName_UsesThePostedIdentifierAndName()
    {
        var (service, _) = await StoredSchemeAsync();
        var updated = AuditableEdit();
        updated.Name = "Renamed Scheme";

        var result = await UpdateAsync(service, updated);

        Assert.Equal("Scheme PT1234: Renamed Scheme", result.AuditObjectName);
    }

    [Fact]
    public async Task UpdateSchemeAsync_AuditableChangeWithAReason_PassesTheAuditToTheRepository()
    {
        var (service, repository) = await StoredSchemeAsync();

        var result = await UpdateAsync(service, AuditableEdit(), "Test 2 was withdrawn");

        Assert.Equal(SchemeUpdateStatus.Updated, result.Status);
        Assert.Single(result.Scheme!.Tests);
        var audit = Assert.Single(repository.AuditWrites);
        Assert.NotNull(audit);
        Assert.Equal("tester", audit.AuditWho);
        Assert.Equal("Test 2 was withdrawn", audit.AuditWhy);
        Assert.Equal(new SchemeItemAudit(Test2, SchemeAuditOperation.Delete, TypeB, false, 2, false), Assert.Single(audit.Plan.Tests));
        Assert.Single((await repository.GetByIdAsync(SchemeId))!.Tests);
    }

    [Fact]
    public async Task UpdateSchemeAsync_ReasonOfExactlyTheMaximumLength_IsAccepted()
    {
        var (service, repository) = await StoredSchemeAsync();
        var reason = new string('x', 2000);

        var result = await UpdateAsync(service, AuditableEdit(), reason);

        Assert.Equal(SchemeUpdateStatus.Updated, result.Status);
        Assert.Equal(reason, repository.LastAudit!.AuditWhy);
    }

    [Fact]
    public async Task UpdateSchemeAsync_ReasonLongerThanTheMaximum_ThrowsAValidationErrorOnAuditWhy()
    {
        var (service, repository) = await StoredSchemeAsync();

        var exception = await Assert.ThrowsAsync<SchemeValidationException>(() => UpdateAsync(service, AuditableEdit(), new string('x', 2001)));

        var error = Assert.Single(exception.Errors);
        Assert.Equal("AuditWhy", error.Field);
        Assert.Equal(SchemeService.AuditWhyTooLongMessage, error.Message);
        Assert.Empty(repository.AuditWrites);
    }

    [Fact]
    public async Task UpdateSchemeAsync_AuditWhoLongerThanTheColumn_IsTruncatedToFiftyCharacters()
    {
        var (service, repository) = await StoredSchemeAsync();
        var who = new string('a', 30) + new string('b', 50);

        await UpdateAsync(service, AuditableEdit(), "Reason", who);

        Assert.Equal(who[..50], repository.LastAudit!.AuditWho);
    }

    [Fact]
    public async Task UpdateSchemeAsync_AuditWhoOfExactlyFiftyCharacters_IsKeptWhole()
    {
        var (service, repository) = await StoredSchemeAsync();
        var who = new string('a', 50);

        await UpdateAsync(service, AuditableEdit(), "Reason", who);

        Assert.Equal(who, repository.LastAudit!.AuditWho);
    }

    [Fact]
    public async Task UpdateSchemeAsync_OnlyNonAuditedFieldsChanged_SavesWithoutAReasonAndWithoutAnAudit()
    {
        var (service, repository) = await StoredSchemeAsync();
        var updated = Build();
        updated.Name = "Renamed";
        updated.Instructions = "Different instructions.";
        updated.Prices[0].Price = 99m;
        updated.ViewerIds.Add(Guid.NewGuid());
        updated.Tabulations[0].ShowRatings = true;
        updated.Tabulations[0].AvailableToViewers = false;
        updated.Assessor3 = Guid.NewGuid();

        var result = await UpdateAsync(service, updated);

        Assert.Equal(SchemeUpdateStatus.Updated, result.Status);
        Assert.Equal("Renamed", result.Scheme!.Name);
        Assert.Null(Assert.Single(repository.AuditWrites));
    }

    [Fact]
    public async Task UpdateSchemeAsync_OnlyNonAuditedFieldsChangedButAReasonSupplied_StillWritesNoAudit()
    {
        var (service, repository) = await StoredSchemeAsync();
        var updated = Build();
        updated.Name = "Renamed";

        var result = await UpdateAsync(service, updated, "Not needed");

        Assert.Equal(SchemeUpdateStatus.Updated, result.Status);
        Assert.Null(Assert.Single(repository.AuditWrites));
    }

    [Fact]
    public async Task UpdateSchemeAsync_NothingChanged_SavesWithoutAnAudit()
    {
        var (service, repository) = await StoredSchemeAsync();

        var result = await UpdateAsync(service, Build());

        Assert.Equal(SchemeUpdateStatus.Updated, result.Status);
        Assert.Null(Assert.Single(repository.AuditWrites));
    }

    [Fact]
    public async Task UpdateSchemeAsync_UnknownScheme_IsNotFoundAndNothingIsWritten()
    {
        var (service, repository) = await StoredSchemeAsync();

        var result = await service.UpdateSchemeAsync(Guid.NewGuid(), AuditableEdit(), "tester", "Reason");

        Assert.Equal(SchemeUpdateStatus.NotFound, result.Status);
        Assert.Empty(repository.AuditWrites);
    }

    [Fact]
    public async Task UpdateSchemeAsync_RepositoryReturnsNullForAnAuditedSave_IsNotFound()
    {
        var (service, repository) = await StoredSchemeAsync();
        repository.ForceUpdateReturnsNull = true;

        var result = await UpdateAsync(service, AuditableEdit(), "Reason");

        Assert.Equal(SchemeUpdateStatus.NotFound, result.Status);
        Assert.Null(result.Scheme);
        Assert.NotNull(Assert.Single(repository.AuditWrites));
    }

    [Fact]
    public async Task UpdateSchemeAsync_ReadOnlyScheme_StillThrowsBeforeAnyAuditDecision()
    {
        var stored = Build();
        stored.YearId = DateTime.UtcNow.Year - 1;
        var (service, repository) = await StoredSchemeAsync(stored);
        var updated = AuditableEdit();
        updated.YearId = stored.YearId;

        var exception = await Assert.ThrowsAsync<SchemeValidationException>(() => UpdateAsync(service, updated, "Reason"));

        Assert.Equal("This scheme belongs to a closed year and cannot be edited.", Assert.Single(exception.Errors).Message);
        Assert.Empty(repository.AuditWrites);
    }

    [Fact]
    public async Task UpdateSchemeAsync_ValidationErrorsWinOverTheAuditPrompt()
    {
        var (service, repository) = await StoredSchemeAsync();
        var updated = AuditableEdit();
        updated.Name = string.Empty;

        var exception = await Assert.ThrowsAsync<SchemeValidationException>(() => UpdateAsync(service, updated));

        Assert.Contains(exception.Errors, e => e.Field == "Name");
        Assert.Empty(repository.AuditWrites);
    }

    [Fact]
    public async Task UpdateSchemeAsync_StagedItemsWithEmptyIds_GetTheirIdsBeforeDetectionSoTheAuditMatchesTheSave()
    {
        var (service, repository) = await StoredSchemeAsync();
        var updated = Build();
        updated.Tests.Add(new SchemeTest
        {
            TestTypeId = TypeA,
            MethodItems = [new SchemeTestMethodItem { TestMethodItemTypeId = TypeA }],
            ResultItems = [new SchemeTestResultItem { TestResultItemTypeId = TypeA }],
            Categories =
            [
                new SchemeCategoryItem
                {
                    CategoryItemTypeId = TypeA,
                    Criteria = [new SchemeCriterionItem { CriterionItemTypeId = TypeA }],
                },
            ],
        });
        updated.Tabulations.Add(new SchemeTabulation { Name = "Extra" });

        var result = await UpdateAsync(service, updated, "Added a test");

        Assert.Equal(SchemeUpdateStatus.Updated, result.Status);
        var plan = repository.LastAudit!.Plan;
        var saved = (await repository.GetByIdAsync(SchemeId))!;
        var savedTest = saved.Tests[2];
        Assert.NotEqual(Guid.Empty, savedTest.TestId);
        Assert.Equal(savedTest.TestId, Assert.Single(plan.Tests).Id);
        Assert.Equal(savedTest.MethodItems[0].TestMethodItemId, Assert.Single(plan.TestMethodItems).Id);
        Assert.Equal(savedTest.ResultItems[0].TestResultItemId, Assert.Single(plan.TestResultItems).Id);
        Assert.Equal(savedTest.Categories[0].CategoryItemId, Assert.Single(plan.CategoryItems).Id);
        Assert.Equal(savedTest.Categories[0].Criteria[0].CriterionItemId, Assert.Single(plan.CriterionItems).Id);
        Assert.Equal(saved.Tabulations[2].TabulationId, Assert.Single(plan.Tabulations).Id);
        Assert.NotEqual(Guid.Empty, saved.Tabulations[2].TabulationId);
    }

    [Fact]
    public async Task CreateSchemeAsync_NeverAudits()
    {
        var repository = new FakeSchemeRepository();
        var service = new SchemeService(repository, new FakeLookupRepository(), new FakeViewerRepository(), new FakeParticipantRepository(), NullLogger<SchemeService>.Instance);

        var created = await service.CreateSchemeAsync(Build());

        Assert.Equal(2, created.Tests.Count);
        Assert.Empty(repository.AuditWrites);
    }

    [Fact]
    public async Task RenewSchemeAsync_ThenCreatingTheDraft_NeverAudits()
    {
        var (service, repository) = await StoredSchemeAsync();

        var draft = await service.RenewSchemeAsync(SchemeId);
        var created = await service.CreateSchemeAsync(draft!);

        Assert.Equal(2, created.Tests.Count);
        Assert.Empty(repository.AuditWrites);
    }
}
