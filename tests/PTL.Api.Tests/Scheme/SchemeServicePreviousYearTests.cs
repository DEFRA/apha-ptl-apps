using Microsoft.Extensions.Logging.Abstractions;
using PTL.Api.Tests.Lookup;
using PTL.Api.Tests.Participant;
using PTL.Core.Scheme;
using PTL.Core.Viewer;
using static PTL.Api.Tests.Scheme.SchemeAuditFixture;
using CoreScheme = PTL.Core.Scheme.Scheme;

namespace PTL.Api.Tests.Scheme;

// UpdateSchemeAsync for a closed-year scheme; SchemePreviousYearEditTests covers the merge itself.
public class SchemeServicePreviousYearTests
{
    private static readonly int ClosedYear = DateTime.UtcNow.Year - 1;

    private sealed record Harness(SchemeService Service, FakeSchemeRepository Repository, FakeViewerRepository Viewers);

    // The fixture scheme stored for a closed year, with a viewer link for each of its viewers.
    private static async Task<Harness> StoredAsync(Action<CoreScheme>? edit = null)
    {
        var stored = Build();
        stored.YearId = ClosedYear;
        edit?.Invoke(stored);

        var repository = new FakeSchemeRepository();
        await repository.CreateAsync(stored);
        var viewers = new FakeViewerRepository();
        viewers.SchemeViewers.AddRange(stored.ViewerIds.Select(viewerId => new SchemeViewerEntity { ViewerSchemeId = Guid.NewGuid(), ViewerId = viewerId, SchemeId = SchemeId }));
        var service = new SchemeService(repository, new FakeLookupRepository(), viewers, new FakeParticipantRepository(), NullLogger<SchemeService>.Instance);
        return new Harness(service, repository, viewers);
    }

    // The request the form would post back for that scheme, with the edit applied.
    private static CoreScheme Posted(Action<CoreScheme>? edit = null)
    {
        var posted = Build();
        posted.YearId = ClosedYear;
        edit?.Invoke(posted);
        return posted;
    }

    // The fixture as a non-assessment scheme: a Primary Test Consultant in place of the assessors, and no categories.
    private static void MakeNonAssessment(CoreScheme scheme)
    {
        scheme.RequiresAssessment = false;
        scheme.Assessor1 = null;
        scheme.Assessor2 = null;
        scheme.TestConsultant1 = Guid.NewGuid();
        scheme.TestConsultantTabulationId = Tabulation2;

        foreach (var test in scheme.Tests)
        {
            test.Categories.Clear();
        }
    }

    private static Task<SchemeUpdateResult> UpdateAsync(Harness harness, CoreScheme posted, string? why = null) =>
        harness.Service.UpdateSchemeAsync(SchemeId, posted, "tester", why);

    private static async Task<CoreScheme> SavedAsync(Harness harness) => (await harness.Repository.GetByIdAsync(SchemeId))!;

    private static void Set(CoreScheme scheme, string field, object? value) => typeof(CoreScheme).GetProperty(field)!.SetValue(scheme, value);

    private static object? Get(CoreScheme scheme, string field) => typeof(CoreScheme).GetProperty(field)!.GetValue(scheme);

    // Fields legacy leaves editable

    [Fact]
    public async Task UpdateSchemeAsync_ClosedYearScheme_SavesEveryFieldLegacyLeavesEditable()
    {
        var harness = await StoredAsync();
        var posted = Posted(s =>
        {
            s.CustomsVolume = "2kg";
            s.CustomsDescription = "Revised customs description";
            s.DataConsentDeclarationActive = true;
            s.DataConsentDeclarationText = "I consent";
            s.Instructions = "Revised instructions.";
            s.DateOfReceipt = true;
            s.StorageConditions = true;
            s.ConditionOnReceipt = true;
            s.StandardTabulationText = "Standard tabulation text";
            s.TestConsultantTabulationId = Tabulation2;
            s.UseExternalReference = true;
            s.Tabulations[0].AvailableToViewers = false;
            s.Tabulations[1].AvailableToParticipants = true;
        });

        var result = await UpdateAsync(harness, posted);

        Assert.Equal(SchemeUpdateStatus.Updated, result.Status);
        var saved = await SavedAsync(harness);
        Assert.Equal("2kg", saved.CustomsVolume);
        Assert.Equal("Revised customs description", saved.CustomsDescription);
        Assert.True(saved.DataConsentDeclarationActive);
        Assert.Equal("I consent", saved.DataConsentDeclarationText);
        Assert.Equal("Revised instructions.", saved.Instructions);
        Assert.True(saved.DateOfReceipt);
        Assert.True(saved.StorageConditions);
        Assert.True(saved.ConditionOnReceipt);
        Assert.Equal("Standard tabulation text", saved.StandardTabulationText);
        Assert.Equal(Tabulation2, saved.TestConsultantTabulationId);
        Assert.True(saved.UseExternalReference);
        Assert.False(saved.Tabulations[0].AvailableToViewers);
        Assert.True(saved.Tabulations[1].AvailableToParticipants);
    }

    // Fields legacy locks

    [Fact]
    public async Task UpdateSchemeAsync_ClosedYearScheme_IgnoresEveryFieldLegacyLocks()
    {
        var harness = await StoredAsync();
        var before = await SavedAsync(harness);
        var posted = Posted(s =>
        {
            s.Name = "Renamed Scheme";
            s.Identifier = "PT9999";
            s.YearId = ClosedYear + 1;
            s.ScheduleId = Guid.NewGuid();
            s.DistributionMonthApr = false;
            s.DistributionMonthMay = true;
            s.NumberOfSamples = 50;
            s.Deadline = 99;
            s.SampleOrigin = "Elsewhere";
            s.Subcontractor = "Someone Else";
            s.RequiresAssessment = false;
            s.Assessor1 = Guid.NewGuid();
            s.TestConsultant1 = Guid.NewGuid();
            s.StoreRatings = true;
            s.Prices[0].Price = 99m;
            s.CustomsDescription = "Revised customs description";
        });

        var result = await UpdateAsync(harness, posted);

        Assert.Equal(SchemeUpdateStatus.Updated, result.Status);
        var saved = await SavedAsync(harness);
        Assert.Equal("Revised customs description", saved.CustomsDescription);
        Assert.Equal(before.Name, saved.Name);
        Assert.Equal(before.Identifier, saved.Identifier);
        Assert.Equal(ClosedYear, saved.YearId);
        Assert.Equal(before.ScheduleId, saved.ScheduleId);
        Assert.True(saved.DistributionMonthApr);
        Assert.False(saved.DistributionMonthMay);
        Assert.Equal(before.NumberOfSamples, saved.NumberOfSamples);
        Assert.Equal(before.Deadline, saved.Deadline);
        Assert.Equal(before.SampleOrigin, saved.SampleOrigin);
        Assert.Equal(before.Subcontractor, saved.Subcontractor);
        Assert.True(saved.RequiresAssessment);
        Assert.Equal(before.Assessor1, saved.Assessor1);
        Assert.Equal(before.TestConsultant1, saved.TestConsultant1);
        Assert.False(saved.StoreRatings);
        Assert.Equal(before.Prices[0].Price, saved.Prices[0].Price);
    }

    [Fact]
    public async Task UpdateSchemeAsync_ClosedYearScheme_InvalidValuesInLockedFieldsAreIgnoredNotReported()
    {
        var harness = await StoredAsync();
        var before = await SavedAsync(harness);
        var posted = Posted(s =>
        {
            s.Name = string.Empty;
            s.Identifier = "BADID";
            s.Deadline = 0;
            s.NumberOfSamples = 0;
            s.SampleOrigin = string.Empty;
            s.DistributionAsAvailable = true;
            s.Assessor1 = null;
            s.Assessor2 = null;
        });

        var result = await UpdateAsync(harness, posted);

        Assert.Equal(SchemeUpdateStatus.Updated, result.Status);
        var saved = await SavedAsync(harness);
        Assert.Equal(before.Name, saved.Name);
        Assert.Equal(before.Identifier, saved.Identifier);
        Assert.Equal(before.Deadline, saved.Deadline);
        Assert.Equal(before.NumberOfSamples, saved.NumberOfSamples);
        Assert.Equal(before.SampleOrigin, saved.SampleOrigin);
        Assert.False(saved.DistributionAsAvailable);
        Assert.Equal(before.Assessor1, saved.Assessor1);
        Assert.Equal(before.Assessor2, saved.Assessor2);
    }

    // Start Date

    [Fact]
    public async Task UpdateSchemeAsync_ClosedYearScheme_KeepsTheStoredStartDate()
    {
        var storedStart = new DateTime(ClosedYear, 6, 15);
        var harness = await StoredAsync(s => s.StartDate = storedStart);
        var posted = Posted(s =>
        {
            s.StartDate = new DateTime(ClosedYear, 9, 1);
            s.DistributionMonthMay = true;
            s.CustomsDescription = "Revised customs description";
        });

        var result = await UpdateAsync(harness, posted);

        Assert.Equal(storedStart, result.Scheme!.StartDate);
        Assert.Equal(storedStart, (await SavedAsync(harness)).StartDate);
    }

    [Fact]
    public async Task UpdateSchemeAsync_ClosedYearSchemeWithNoStoredStartDate_UsesTheDerivedOne()
    {
        var harness = await StoredAsync();

        var result = await UpdateAsync(harness, Posted());

        Assert.Equal(new DateTime(ClosedYear, 4, 1), result.Scheme!.StartDate);
    }

    // Validation still runs on what will be saved

    [Theory]
    [InlineData("CustomsDescription", "")]
    [InlineData("CustomsDescription", "   ")]
    [InlineData("CustomsDescription", null)]
    [InlineData("CustomsVolume", "")]
    [InlineData("CustomsVolume", "   ")]
    [InlineData("CustomsVolume", null)]
    [InlineData("Instructions", "")]
    [InlineData("Instructions", "   ")]
    public async Task UpdateSchemeAsync_ClosedYearScheme_BlankRequiredField_ThrowsAndSavesNothing(string field, string? blank)
    {
        var harness = await StoredAsync();
        var storedValue = Get(await SavedAsync(harness), field);

        var exception = await Assert.ThrowsAsync<SchemeValidationException>(() => UpdateAsync(harness, Posted(s => Set(s, field, blank))));

        Assert.Contains(exception.Errors, e => e.Field == field);
        Assert.Empty(harness.Repository.AuditWrites);
        Assert.Equal(storedValue, Get(await SavedAsync(harness), field));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateSchemeAsync_ClosedYearScheme_ActiveDataConsentWithoutText_Throws(string? text)
    {
        var harness = await StoredAsync();
        var posted = Posted(s =>
        {
            s.DataConsentDeclarationActive = true;
            s.DataConsentDeclarationText = text;
        });

        var exception = await Assert.ThrowsAsync<SchemeValidationException>(() => UpdateAsync(harness, posted));

        Assert.Contains(exception.Errors, e => e.Field == "DataConsentDeclarationText");
        Assert.Empty(harness.Repository.AuditWrites);
    }

    [Fact]
    public async Task UpdateSchemeAsync_ClosedYearScheme_DataConsentSwitchedOffWithoutText_IsSaved()
    {
        var harness = await StoredAsync(s =>
        {
            s.DataConsentDeclarationActive = true;
            s.DataConsentDeclarationText = "I consent";
        });

        var result = await UpdateAsync(harness, Posted(s =>
        {
            s.DataConsentDeclarationActive = false;
            s.DataConsentDeclarationText = null;
        }));

        Assert.Equal(SchemeUpdateStatus.Updated, result.Status);
        var saved = await SavedAsync(harness);
        Assert.False(saved.DataConsentDeclarationActive);
        Assert.Null(saved.DataConsentDeclarationText);
    }

    [Fact]
    public async Task UpdateSchemeAsync_ClosedYearNonAssessmentScheme_TestConsultantTabulationChange_IsSaved()
    {
        var harness = await StoredAsync(MakeNonAssessment);
        var posted = Posted(s =>
        {
            MakeNonAssessment(s);
            s.TestConsultantTabulationId = Tabulation1;
        });

        var result = await UpdateAsync(harness, posted);

        Assert.Equal(SchemeUpdateStatus.Updated, result.Status);
        Assert.Equal(Tabulation1, (await SavedAsync(harness)).TestConsultantTabulationId);
    }

    [Fact]
    public async Task UpdateSchemeAsync_ClosedYearNonAssessmentScheme_TestConsultantTabulationNotAmongTheTabulations_Throws()
    {
        var harness = await StoredAsync(MakeNonAssessment);
        var posted = Posted(s =>
        {
            MakeNonAssessment(s);
            s.TestConsultantTabulationId = Guid.NewGuid();
        });

        var exception = await Assert.ThrowsAsync<SchemeValidationException>(() => UpdateAsync(harness, posted));

        Assert.Contains(exception.Errors, e => e.Field == "TestConsultantTabulationId");
        Assert.Empty(harness.Repository.AuditWrites);
    }

    // Derived Score Samples

    [Fact]
    public async Task UpdateSchemeAsync_ClosedYearScheme_TabulationShowingRatings_TurnsOnScoreSamplesEvenThoughItIsLocked()
    {
        var harness = await StoredAsync();
        Assert.False((await SavedAsync(harness)).StoreRatings);
        var posted = Posted(s => s.Tabulations[0].ShowRatings = true);

        var result = await UpdateAsync(harness, posted);

        Assert.Equal(SchemeUpdateStatus.Updated, result.Status);
        Assert.True(result.Scheme!.StoreRatings);
        Assert.True((await SavedAsync(harness)).StoreRatings);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UpdateSchemeAsync_ClosedYearScheme_NoTabulationShowingRatings_KeepsTheStoredScoreSamples(bool stored)
    {
        var harness = await StoredAsync(s => s.StoreRatings = stored);

        var result = await UpdateAsync(harness, Posted(s => s.StoreRatings = !stored));

        Assert.Equal(SchemeUpdateStatus.Updated, result.Status);
        Assert.Equal(stored, (await SavedAsync(harness)).StoreRatings);
    }

    // Viewers

    [Fact]
    public async Task UpdateSchemeAsync_ClosedYearScheme_ViewerSelectionChanged_AddsAndRemovesOnlyTheDifference()
    {
        var kept = Guid.NewGuid();
        var removed = Guid.NewGuid();
        var added = Guid.NewGuid();
        var harness = await StoredAsync(s => s.ViewerIds = [kept, removed]);
        var keptLinkId = harness.Viewers.SchemeViewers.Single(link => link.ViewerId == kept).ViewerSchemeId;

        var result = await UpdateAsync(harness, Posted(s => s.ViewerIds = [kept, added]));

        Assert.Equal(SchemeUpdateStatus.Updated, result.Status);
        Assert.Equal(new[] { kept, added }, result.Scheme!.ViewerIds);
        var links = await harness.Viewers.GetSchemeViewersAsync(SchemeId);
        Assert.Equal(2, links.Count);
        Assert.DoesNotContain(links, link => link.ViewerId == removed);
        Assert.Contains(links, link => link.ViewerId == added);
        Assert.Equal(keptLinkId, links.Single(link => link.ViewerId == kept).ViewerSchemeId);
    }

    [Fact]
    public async Task UpdateSchemeAsync_ClosedYearScheme_AllViewersRemoved_RemovesEveryLink()
    {
        var harness = await StoredAsync(s => s.ViewerIds = [Guid.NewGuid(), Guid.NewGuid()]);

        var result = await UpdateAsync(harness, Posted(s => s.ViewerIds.Clear()));

        Assert.Equal(SchemeUpdateStatus.Updated, result.Status);
        Assert.Empty(result.Scheme!.ViewerIds);
        Assert.Empty(await harness.Viewers.GetSchemeViewersAsync(SchemeId));
    }

    // Not found

    [Fact]
    public async Task UpdateSchemeAsync_UnknownScheme_IsStillNotFound()
    {
        var harness = await StoredAsync();

        var result = await harness.Service.UpdateSchemeAsync(Guid.NewGuid(), Posted(), "tester", null);

        Assert.Equal(SchemeUpdateStatus.NotFound, result.Status);
        Assert.Null(result.Scheme);
        Assert.Empty(harness.Repository.AuditWrites);
    }

    [Fact]
    public async Task UpdateSchemeAsync_ClosedYearScheme_DeletedBeforeTheSave_IsNotFound()
    {
        var harness = await StoredAsync();
        harness.Repository.ForceUpdateReturnsNull = true;

        var result = await UpdateAsync(harness, Posted(s => s.CustomsDescription = "Revised customs description"));

        Assert.Equal(SchemeUpdateStatus.NotFound, result.Status);
        Assert.Null(result.Scheme);
    }

    // Schemes that are not from a closed year are unaffected

    [Theory]
    [InlineData(-1, "Test Scheme")]
    [InlineData(0, "Renamed Scheme")]
    [InlineData(1, "Renamed Scheme")]
    public async Task UpdateSchemeAsync_NameChange_IsTakenUnlessTheSchemeIsFromAClosedYear(int yearOffset, string expectedName)
    {
        var year = DateTime.UtcNow.Year + yearOffset;
        var harness = await StoredAsync(s => s.YearId = year);

        var result = await UpdateAsync(harness, Posted(s =>
        {
            s.YearId = year;
            s.Name = "Renamed Scheme";
        }));

        Assert.Equal(SchemeUpdateStatus.Updated, result.Status);
        Assert.Equal(expectedName, (await SavedAsync(harness)).Name);
    }

    [Fact]
    public async Task UpdateSchemeAsync_SchemeNotFromAClosedYear_StillTakesEveryPostedField()
    {
        var year = DateTime.UtcNow.Year;
        var harness = await StoredAsync(s => s.YearId = year);
        Assert.False((await SavedAsync(harness)).IsReadOnly);
        var posted = Posted(s =>
        {
            s.YearId = year;
            s.Name = "Renamed Scheme";
            s.NumberOfSamples = 8;
            s.Deadline = 20;
            s.DistributionMonthApr = false;
            s.DistributionMonthMay = true;
            s.Prices[0].Price = 25m;
            s.Tests.RemoveAt(1);
        });

        var result = await UpdateAsync(harness, posted, "Test 2 was withdrawn");

        Assert.Equal(SchemeUpdateStatus.Updated, result.Status);
        var saved = await SavedAsync(harness);
        Assert.Equal("Renamed Scheme", saved.Name);
        Assert.Equal(8, saved.NumberOfSamples);
        Assert.Equal(20, saved.Deadline);
        Assert.False(saved.DistributionMonthApr);
        Assert.True(saved.DistributionMonthMay);
        Assert.Equal(new DateTime(year, 5, 1), saved.StartDate);
        Assert.Equal(25m, saved.Prices[0].Price);
        Assert.Equal(new[] { Test1 }, saved.Tests.Select(t => t.TestId));
        Assert.Equal(new SchemeItemAudit(Test2, SchemeAuditOperation.Delete, TypeB, false, 2, false), Assert.Single(harness.Repository.LastAudit!.Plan.Tests));
    }
}
