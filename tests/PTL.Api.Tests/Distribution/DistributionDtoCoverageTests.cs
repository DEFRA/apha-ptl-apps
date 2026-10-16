using PTL.Contracts.Distribution;
using PTL.Core.Distribution;

namespace PTL.Api.Tests.Distribution;

// Reads every property getter on the Distribution wire DTOs and Dapper entities. Coverlet only
// counts a property as covered when its getter actually runs, so constructing one is not enough -
// same pattern as Contracts/SharedDtoCoverageTests.
public class DistributionDtoCoverageTests
{
    [Fact]
    public void MonthlyDistributionResponse_ExposesEveryProperty()
    {
        var monthlyDistributionId = Guid.NewGuid();
        var schemeId = Guid.NewGuid();
        var monthlyDistributionSchemeId = Guid.NewGuid();

        var scheme = new MonthlyDistributionSchemeResponse(
            MonthlyDistributionSchemeId: monthlyDistributionSchemeId,
            SchemeId: schemeId,
            SchemeIdentifier: "PT0060",
            SchemeName: "Mastitis bovine",
            DistributionReferenceFull: "17152/BA",
            DistributionDate: new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            OverseasPostingDate: new DateTime(2026, 4, 2, 0, 0, 0, DateTimeKind.Utc),
            DeadlineDate: new DateTime(2026, 4, 5, 0, 0, 0, DateTimeKind.Utc),
            ResultsIssueTargetDate: new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc),
            ParticipantCount: 4,
            TotalSetsOfSamplesRequired: 9,
            HasSampleNumbersDefined: true,
            HasIntendedResults: true,
            IsCancelled: false,
            IsAsAvailable: true);

        var response = new MonthlyDistributionResponse(monthlyDistributionId, 2026, 4, [scheme]);

        Assert.Equal(monthlyDistributionId, response.MonthlyDistributionId);
        Assert.Equal(2026, response.YearId);
        Assert.Equal(4, response.MonthId);
        Assert.Equal(monthlyDistributionSchemeId, Assert.Single(response.Schemes).MonthlyDistributionSchemeId);
        Assert.Equal(schemeId, scheme.SchemeId);
        Assert.Equal("PT0060", scheme.SchemeIdentifier);
        Assert.Equal("Mastitis bovine", scheme.SchemeName);
        Assert.Equal("17152/BA", scheme.DistributionReferenceFull);
        Assert.Equal(new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc), scheme.DistributionDate);
        Assert.Equal(new DateTime(2026, 4, 2, 0, 0, 0, DateTimeKind.Utc), scheme.OverseasPostingDate);
        Assert.Equal(new DateTime(2026, 4, 5, 0, 0, 0, DateTimeKind.Utc), scheme.DeadlineDate);
        Assert.Equal(new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc), scheme.ResultsIssueTargetDate);
        Assert.Equal(4, scheme.ParticipantCount);
        Assert.Equal(9, scheme.TotalSetsOfSamplesRequired);
        Assert.True(scheme.HasSampleNumbersDefined);
        Assert.True(scheme.HasIntendedResults);
        Assert.False(scheme.IsCancelled);
        Assert.True(scheme.IsAsAvailable);
    }

    [Fact]
    public void MonthlyDistributionScheduleRequestAndResult_ExposeEveryProperty()
    {
        var schemeId = Guid.NewGuid();

        var row = new MonthlyDistributionScheduleRowRequest(
            MonthlyDistributionSchemeId: schemeId,
            DistributionDate: new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            OverseasPostingDate: new DateTime(2026, 4, 2, 0, 0, 0, DateTimeKind.Utc),
            DeadlineDate: new DateTime(2026, 4, 5, 0, 0, 0, DateTimeKind.Utc),
            ResultsIssueTargetDate: new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc),
            IsCancelled: true);

        Assert.Equal(schemeId, row.MonthlyDistributionSchemeId);
        Assert.Equal(new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc), row.DistributionDate);
        Assert.Equal(new DateTime(2026, 4, 2, 0, 0, 0, DateTimeKind.Utc), row.OverseasPostingDate);
        Assert.Equal(new DateTime(2026, 4, 5, 0, 0, 0, DateTimeKind.Utc), row.DeadlineDate);
        Assert.Equal(new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc), row.ResultsIssueTargetDate);
        Assert.True(row.IsCancelled);

        var result = new MonthlyDistributionScheduleSaveResult(
            false, new Dictionary<Guid, string[]> { [schemeId] = ["Deadline Date needs to be after the UK Posting date."] });

        Assert.False(result.Success);
        Assert.Equal("Deadline Date needs to be after the UK Posting date.", Assert.Single(result.FieldErrorsBySchemeId[schemeId]));
    }

    [Fact]
    public void DistributionDashboardResponses_ExposeEveryProperty()
    {
        var monthlyDistributionId = Guid.NewGuid();
        var month = new DistributionDashboardMonthResponse(
            YearId: 2026,
            MonthId: 4,
            MonthDescription: "Apr",
            MonthlyDistributionId: monthlyDistributionId,
            IsInitialisable: false,
            SchedulingCompletePercentage: 50m,
            PreparationCompletePercentage: 25m,
            PackagingCompletePercentage: 10m,
            ResultsCompletePercentage: 5m,
            TabulationsCompletePercentage: 1m);

        Assert.Equal(2026, month.YearId);
        Assert.Equal(4, month.MonthId);
        Assert.Equal("Apr", month.MonthDescription);
        Assert.Equal(monthlyDistributionId, month.MonthlyDistributionId);
        Assert.False(month.IsInitialisable);
        Assert.Equal(50m, month.SchedulingCompletePercentage);
        Assert.Equal(25m, month.PreparationCompletePercentage);
        Assert.Equal(10m, month.PackagingCompletePercentage);
        Assert.Equal(5m, month.ResultsCompletePercentage);
        Assert.Equal(1m, month.TabulationsCompletePercentage);

        var year = new DistributionYearOptionResponse(2026, "2026/27");
        Assert.Equal(2026, year.YearId);
        Assert.Equal("2026/27", year.Label);
    }

    [Fact]
    public void DistributionEntities_ExposeEveryProperty()
    {
        var monthlyDistributionId = Guid.NewGuid();
        var summary = new DistributionMonthSummaryEntity
        {
            MonthlyDistributionId = monthlyDistributionId,
            YearId = 2026,
            MonthId = 4,
            NumberOfSchemes = 10,
            NumberOfSampleNumbersDefined = 9,
            NumberOfPrepComplete = 8,
            NumberOfParticipants = 7,
            NumberOfPackagingComplete = 6,
            NumberOfResultsEntered = 5,
            NumberOfTabulations = 4,
            NumberOfCompleteTabulations = 3,
        };

        Assert.Equal(monthlyDistributionId, summary.MonthlyDistributionId);
        Assert.Equal(2026, summary.YearId);
        Assert.Equal(4, summary.MonthId);
        Assert.Equal(10, summary.NumberOfSchemes);
        Assert.Equal(9, summary.NumberOfSampleNumbersDefined);
        Assert.Equal(8, summary.NumberOfPrepComplete);
        Assert.Equal(7, summary.NumberOfParticipants);
        Assert.Equal(6, summary.NumberOfPackagingComplete);
        Assert.Equal(5, summary.NumberOfResultsEntered);
        Assert.Equal(4, summary.NumberOfTabulations);
        Assert.Equal(3, summary.NumberOfCompleteTabulations);

        var monthYear = new DistributionMonthYearEntity { YearId = 2026, MonthId = 4 };
        Assert.Equal(2026, monthYear.YearId);
        Assert.Equal(4, monthYear.MonthId);
    }

    [Fact]
    public void MonthlyDistributionSchemeEntity_ExposesEveryProperty()
    {
        var monthlyDistributionSchemeId = Guid.NewGuid();
        var monthlyDistributionId = Guid.NewGuid();
        var schemeId = Guid.NewGuid();

        var entity = new MonthlyDistributionSchemeEntity
        {
            MonthlyDistributionSchemeId = monthlyDistributionSchemeId,
            MonthlyDistributionId = monthlyDistributionId,
            SchemeId = schemeId,
            SchemeIdentifier = "PT0060",
            SchemeName = "Mastitis bovine",
            DistributionReference = "17152",
            DistributionReferenceSuffix = "A",
            ScheduleCode = "BA",
            DistributionDate = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            OverseasPostingDate = new DateTime(2026, 4, 2, 0, 0, 0, DateTimeKind.Utc),
            DeadlineDate = new DateTime(2026, 4, 5, 0, 0, 0, DateTimeKind.Utc),
            ResultsIssueTargetDate = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc),
            Comments = "Some comments",
            HasIntendedResults = true,
            HasSampleNumbersDefined = true,
            IsCancelled = true,
            IsAsAvailable = true,
            StoreRatings = true,
            SchemeVersionDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ParticipantCount = 4,
            TotalSetsOfSamplesRequired = 9,
        };

        Assert.Equal(monthlyDistributionSchemeId, entity.MonthlyDistributionSchemeId);
        Assert.Equal(monthlyDistributionId, entity.MonthlyDistributionId);
        Assert.Equal(schemeId, entity.SchemeId);
        Assert.Equal("PT0060", entity.SchemeIdentifier);
        Assert.Equal("Mastitis bovine", entity.SchemeName);
        Assert.Equal("17152", entity.DistributionReference);
        Assert.Equal("A", entity.DistributionReferenceSuffix);
        Assert.Equal("BA", entity.ScheduleCode);
        Assert.Equal(new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc), entity.DistributionDate);
        Assert.Equal(new DateTime(2026, 4, 2, 0, 0, 0, DateTimeKind.Utc), entity.OverseasPostingDate);
        Assert.Equal(new DateTime(2026, 4, 5, 0, 0, 0, DateTimeKind.Utc), entity.DeadlineDate);
        Assert.Equal(new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc), entity.ResultsIssueTargetDate);
        Assert.Equal("Some comments", entity.Comments);
        Assert.True(entity.HasIntendedResults);
        Assert.True(entity.HasSampleNumbersDefined);
        Assert.True(entity.IsCancelled);
        Assert.True(entity.IsAsAvailable);
        Assert.True(entity.StoreRatings);
        Assert.Equal(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), entity.SchemeVersionDate);
        Assert.Equal(4, entity.ParticipantCount);
        Assert.Equal(9, entity.TotalSetsOfSamplesRequired);
        Assert.Equal("17152A/BA", entity.DistributionReferenceFull);

        var participant = new MonthlyDistributionParticipantAggregateEntity
        {
            DistributionSchemeId = monthlyDistributionSchemeId,
            NumberOfSetsRequired = 3,
        };

        Assert.Equal(monthlyDistributionSchemeId, participant.DistributionSchemeId);
        Assert.Equal(3, participant.NumberOfSetsRequired);
    }

    [Fact]
    public void SampleNumbersResponses_ExposeEveryProperty()
    {
        var schemeId = Guid.NewGuid();
        var sampleId = Guid.NewGuid();
        var sampleTestId = Guid.NewGuid();
        var testId = Guid.NewGuid();

        var allocation = new SampleAllocationResponse(sampleTestId, testId, true);
        var sample = new SampleNumberRowResponse(sampleId, "S1", "Internal", "External", "Tabulation", "TC", [allocation]);
        var test = new SampleNumbersTestResponse(testId, "Somatic cell count", ["Result A"], ["Method A"], ["Appearance (Colour)"]);
        var response = new SampleNumbersResponse(schemeId, "17152A/BA", "PT0060", "Mastitis bovine", 9, "Comments", true, true, [sample], [test]);

        Assert.Equal(schemeId, response.MonthlyDistributionSchemeId);
        Assert.Equal("17152A/BA", response.DistributionReferenceFull);
        Assert.Equal("PT0060", response.SchemeIdentifier);
        Assert.Equal("Mastitis bovine", response.SchemeName);
        Assert.Equal(9, response.NumberOfSetsRequired);
        Assert.Equal("Comments", response.Comments);
        Assert.True(response.RequiresAssessment);
        Assert.True(response.IsReadyForAssessment);
        Assert.Same(sample, Assert.Single(response.Samples));
        Assert.Same(test, Assert.Single(response.Tests));

        Assert.Equal(sampleId, sample.SampleId);
        Assert.Equal("S1", sample.SampleNumber);
        Assert.Equal("Internal", sample.InternalDescription);
        Assert.Equal("External", sample.ExternalDescription);
        Assert.Equal("Tabulation", sample.TabulationDescription);
        Assert.Equal("TC", sample.TestConsultantsDescription);
        Assert.Same(allocation, Assert.Single(sample.Allocations));

        Assert.Equal(sampleTestId, allocation.SampleTestId);
        Assert.Equal(testId, allocation.TestId);
        Assert.True(allocation.IsApplicable);

        Assert.Equal(testId, test.TestId);
        Assert.Equal("Somatic cell count", test.TestName);
        Assert.Equal(["Result A"], test.TestResultItemNames);
        Assert.Equal(["Method A"], test.TestMethodItemNames);
        Assert.Equal(["Appearance (Colour)"], test.CategoryItemNames);
    }

    [Fact]
    public void SampleNumbersSaveContracts_ExposeEveryProperty()
    {
        var sampleId = Guid.NewGuid();
        var sampleTestId = Guid.NewGuid();

        var allocation = new SampleAllocationRequest(sampleTestId, true);
        var row = new SampleNumberRowRequest(sampleId, "S1", "Internal", "External", "Tabulation", "TC", [allocation]);
        var request = new SampleNumbersSaveRequest("Comments", [row], "Because");

        Assert.Equal("Comments", request.Comments);
        Assert.Same(row, Assert.Single(request.Samples));
        Assert.Equal("Because", request.AuditWhy);
        Assert.Null(new SampleNumbersSaveRequest("Comments", [row]).AuditWhy);
        Assert.Equal(sampleId, row.SampleId);
        Assert.Equal("S1", row.SampleNumber);
        Assert.Equal("Internal", row.InternalDescription);
        Assert.Equal("External", row.ExternalDescription);
        Assert.Equal("Tabulation", row.TabulationDescription);
        Assert.Equal("TC", row.TestConsultantsDescription);
        Assert.Equal(sampleTestId, allocation.SampleTestId);
        Assert.True(allocation.IsApplicable);

        var fieldError = new SampleNumbersFieldError("SampleNumber", "Sample Number Required");
        var result = new SampleNumbersSaveResult(false, ["Comments must not exceed 8000 characters"], new Dictionary<Guid, SampleNumbersFieldError[]> { [sampleId] = [fieldError] });

        Assert.False(result.Success);
        Assert.Equal("Comments must not exceed 8000 characters", Assert.Single(result.PageErrors));
        Assert.Same(fieldError, Assert.Single(result.FieldErrorsBySampleId[sampleId]));
        Assert.Equal("SampleNumber", fieldError.Field);
        Assert.Equal("Sample Number Required", fieldError.Message);
        Assert.False(result.AuditReasonRequired);
        Assert.Null(result.AuditObjectName);

        var auditResult = new SampleNumbersSaveResult(
            false, [], new Dictionary<Guid, SampleNumbersFieldError[]>(), AuditReasonRequired: true, AuditObjectName: "Intended Results Set");
        Assert.True(auditResult.AuditReasonRequired);
        Assert.Equal("Intended Results Set", auditResult.AuditObjectName);
    }

    [Fact]
    public void SampleNumbersPageModels_ExposeEveryProperty()
    {
        var schemeId = Guid.NewGuid();
        var sampleId = Guid.NewGuid();
        var sampleTestId = Guid.NewGuid();
        var testId = Guid.NewGuid();

        var allocation = new SampleAllocation(sampleTestId, testId, true);
        var sample = new SampleNumberRow(sampleId, "S1", "Internal", "External", "Tabulation", "TC", [allocation]);
        var test = new SampleNumbersTest(testId, "Somatic cell count", ["Result A"], ["Method A"], ["Appearance ()"]);
        var page = new SampleNumbersPage(schemeId, "17152A/BA", "PT0060", "Mastitis bovine", 9, "Comments", true, false, [sample], [test]);

        Assert.Equal(schemeId, page.MonthlyDistributionSchemeId);
        Assert.Equal("17152A/BA", page.DistributionReferenceFull);
        Assert.Equal("PT0060", page.SchemeIdentifier);
        Assert.Equal("Mastitis bovine", page.SchemeName);
        Assert.Equal(9, page.NumberOfSetsRequired);
        Assert.Equal("Comments", page.Comments);
        Assert.True(page.RequiresAssessment);
        Assert.False(page.IsReadyForAssessment);
        Assert.Same(sample, Assert.Single(page.Samples));
        Assert.Same(test, Assert.Single(page.Tests));

        Assert.Equal(sampleId, sample.SampleId);
        Assert.Equal("S1", sample.SampleNumber);
        Assert.Equal("Internal", sample.InternalDescription);
        Assert.Equal("External", sample.ExternalDescription);
        Assert.Equal("Tabulation", sample.TabulationDescription);
        Assert.Equal("TC", sample.TestConsultantsDescription);
        Assert.Same(allocation, Assert.Single(sample.Allocations));
        Assert.Equal(sampleTestId, allocation.SampleTestId);
        Assert.Equal(testId, allocation.TestId);
        Assert.True(allocation.IsApplicable);

        Assert.Equal(testId, test.TestId);
        Assert.Equal("Somatic cell count", test.TestName);
        Assert.Equal(["Result A"], test.TestResultItemNames);
        Assert.Equal(["Method A"], test.TestMethodItemNames);
        Assert.Equal(["Appearance ()"], test.CategoryItemNames);

        var update = new SampleNumbersUpdate("Comments", [new SampleNumberRowUpdate(sampleId, "S1", "Internal", "External", "Tabulation", "TC", [new SampleAllocationUpdate(sampleTestId, false)])]);
        Assert.Equal("Comments", update.Comments);
        Assert.Equal(string.Empty, update.AuditWho);
        Assert.Equal(string.Empty, update.AuditWhy);
        var audited = update with { AuditWho = "tester", AuditWhy = "Because" };
        Assert.Equal("tester", audited.AuditWho);
        Assert.Equal("Because", audited.AuditWhy);
        var rowUpdate = Assert.Single(update.Samples);
        Assert.Equal(sampleId, rowUpdate.SampleId);
        Assert.Equal("S1", rowUpdate.SampleNumber);
        Assert.Equal("Internal", rowUpdate.InternalDescription);
        Assert.Equal("External", rowUpdate.ExternalDescription);
        Assert.Equal("Tabulation", rowUpdate.TabulationDescription);
        Assert.Equal("TC", rowUpdate.TestConsultantsDescription);
        var allocationUpdate = Assert.Single(rowUpdate.Allocations);
        Assert.Equal(sampleTestId, allocationUpdate.SampleTestId);
        Assert.False(allocationUpdate.IsApplicable);

        var error = new SampleNumbersError("Comments", "Too long");
        var outcome = new SampleNumbersSaveOutcome(false, [error], new Dictionary<Guid, IReadOnlyList<SampleNumbersError>> { [sampleId] = [error] });
        Assert.False(outcome.Success);
        Assert.Same(error, Assert.Single(outcome.PageErrors));
        Assert.Same(error, Assert.Single(outcome.FieldErrorsBySampleId[sampleId]));
        Assert.Equal("Comments", error.Field);
        Assert.Equal("Too long", error.Message);

        var succeeded = SampleNumbersSaveOutcome.Succeeded();
        Assert.True(succeeded.Success);
        Assert.Empty(succeeded.PageErrors);
        Assert.Empty(succeeded.FieldErrorsBySampleId);
        Assert.False(succeeded.AuditReasonRequired);
        Assert.Null(succeeded.AuditObjectName);

        var failed = SampleNumbersSaveOutcome.Failed([error], new Dictionary<Guid, IReadOnlyList<SampleNumbersError>> { [sampleId] = [error] });
        Assert.False(failed.Success);
        Assert.Same(error, Assert.Single(failed.PageErrors));
        Assert.False(failed.AuditReasonRequired);

        var needsReason = SampleNumbersSaveOutcome.NeedsAuditReason("Intended Results Set");
        Assert.False(needsReason.Success);
        Assert.True(needsReason.AuditReasonRequired);
        Assert.Equal("Intended Results Set", needsReason.AuditObjectName);
        Assert.Empty(needsReason.PageErrors);
        Assert.Empty(needsReason.FieldErrorsBySampleId);

        var header = new IntendedResultsHeaderEntity { MonthlyDistributionSchemeId = schemeId };
        var write = new SampleNumbersWrite(
            header,
            [new DistributionSampleEntity { SampleId = sampleId }],
            [new DistributionSampleTestEntity { SampleTestId = sampleTestId }],
            [sampleId],
            "tester",
            "Because",
            SetupSampleReceipt: true);
        Assert.Same(header, write.Header);
        Assert.Equal(sampleId, Assert.Single(write.Samples).SampleId);
        Assert.Equal(sampleTestId, Assert.Single(write.SampleTests).SampleTestId);
        Assert.Equal(sampleId, Assert.Single(write.AuditedSampleIds));
        Assert.Equal("tester", write.AuditWho);
        Assert.Equal("Because", write.AuditWhy);
        Assert.True(write.SetupSampleReceipt);
    }

    [Fact]
    public void IntendedResultsEntities_ExposeEveryProperty()
    {
        var schemeId = Guid.NewGuid();
        var sampleId = Guid.NewGuid();
        var sampleTestId = Guid.NewGuid();
        var testId = Guid.NewGuid();
        var testTypeId = Guid.NewGuid();
        var tabulationId = Guid.NewGuid();
        var categoryItemId = Guid.NewGuid();

        var header = new IntendedResultsHeaderEntity
        {
            MonthlyDistributionSchemeId = schemeId,
            Comments = "Comments",
            HasIntendedResults = true,
            HasSampleNumbersDefined = true,
            TestConsultantTabulationId = tabulationId,
            StoreRatings = true,
            RequiresAssessment = true,
            IsReadyForAssessor = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
        };

        Assert.Equal(schemeId, header.MonthlyDistributionSchemeId);
        Assert.Equal("Comments", header.Comments);
        Assert.True(header.HasIntendedResults);
        Assert.True(header.HasSampleNumbersDefined);
        Assert.Equal(tabulationId, header.TestConsultantTabulationId);
        Assert.True(header.StoreRatings);
        Assert.True(header.RequiresAssessment);
        Assert.Equal(new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc), header.IsReadyForAssessor);

        var sample = new DistributionSampleEntity
        {
            SampleId = sampleId,
            DistributionSchemeId = schemeId,
            SampleNumber = "S1",
            InternalDescription = "Internal",
            ExternalDescription = "External",
            TabulationDescription = "Tabulation",
            TestConsultantsDescription = "TC",
        };

        Assert.Equal(sampleId, sample.SampleId);
        Assert.Equal(schemeId, sample.DistributionSchemeId);
        Assert.Equal("S1", sample.SampleNumber);
        Assert.Equal("Internal", sample.InternalDescription);
        Assert.Equal("External", sample.ExternalDescription);
        Assert.Equal("Tabulation", sample.TabulationDescription);
        Assert.Equal("TC", sample.TestConsultantsDescription);

        var sampleTest = new DistributionSampleTestEntity { SampleTestId = sampleTestId, SampleId = sampleId, TestId = testId, IsApplicable = true };
        Assert.Equal(sampleTestId, sampleTest.SampleTestId);
        Assert.Equal(sampleId, sampleTest.SampleId);
        Assert.Equal(testId, sampleTest.TestId);
        Assert.True(sampleTest.IsApplicable);

        var test = new DistributionTestEntity { TestId = testId, TestTypeId = testTypeId, TestName = "Somatic cell count", Order = 1 };
        Assert.Equal(testId, test.TestId);
        Assert.Equal(testTypeId, test.TestTypeId);
        Assert.Equal("Somatic cell count", test.TestName);
        Assert.Equal(1, test.Order);

        var methodItemId = Guid.NewGuid();
        var methodItem = new DistributionTestMethodItemEntity { TestMethodItemId = methodItemId, TestId = testId, Name = "Method A", Order = 2 };
        Assert.Equal(methodItemId, methodItem.TestMethodItemId);
        Assert.Equal(testId, methodItem.TestId);
        Assert.Equal("Method A", methodItem.Name);
        Assert.Equal(2, methodItem.Order);

        var resultItemId = Guid.NewGuid();
        var resultItem = new DistributionTestResultItemEntity { TestResultItemId = resultItemId, TestId = testId, Name = "Result A", Order = 3 };
        Assert.Equal(resultItemId, resultItem.TestResultItemId);
        Assert.Equal(testId, resultItem.TestId);
        Assert.Equal("Result A", resultItem.Name);
        Assert.Equal(3, resultItem.Order);

        var categoryItem = new DistributionCategoryItemEntity { CategoryItemId = categoryItemId, TestId = testId, Name = "Appearance", Order = 4 };
        Assert.Equal(categoryItemId, categoryItem.CategoryItemId);
        Assert.Equal(testId, categoryItem.TestId);
        Assert.Equal("Appearance", categoryItem.Name);
        Assert.Equal(4, categoryItem.Order);

        var criterionItemId = Guid.NewGuid();
        var criterionItem = new DistributionCriterionItemEntity { CriterionItemId = criterionItemId, CategoryItemId = categoryItemId, TestId = testId, Name = "Colour", Order = 5 };
        Assert.Equal(criterionItemId, criterionItem.CriterionItemId);
        Assert.Equal(categoryItemId, criterionItem.CategoryItemId);
        Assert.Equal(testId, criterionItem.TestId);
        Assert.Equal("Colour", criterionItem.Name);
        Assert.Equal(5, criterionItem.Order);

        var data = new IntendedResultsData(header, [sample], [sampleTest], [test], [methodItem], [resultItem], [categoryItem], [criterionItem]);
        Assert.Same(header, data.Header);
        Assert.Same(sample, Assert.Single(data.Samples));
        Assert.Same(sampleTest, Assert.Single(data.SampleTests));
        Assert.Same(test, Assert.Single(data.Tests));
        Assert.Same(methodItem, Assert.Single(data.TestMethodItems));
        Assert.Same(resultItem, Assert.Single(data.TestResultItems));
        Assert.Same(categoryItem, Assert.Single(data.CategoryItems));
        Assert.Same(criterionItem, Assert.Single(data.CriterionItems));
    }

    [Fact]
    public void PreparationContracts_ExposeEveryProperty()
    {
        var monthlyDistributionId = Guid.NewGuid();
        var schemeId = Guid.NewGuid();
        var sampleId = Guid.NewGuid();
        var diaryEntry = new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc);
        var prepStart = new DateTime(2026, 1, 2, 9, 0, 0, DateTimeKind.Utc);
        var prepComplete = new DateTime(2026, 1, 3, 9, 0, 0, DateTimeKind.Utc);

        var sampleResponse = new PreparationSampleResponse(sampleId, "S1", "Internal 1");
        Assert.Equal(sampleId, sampleResponse.SampleId);
        Assert.Equal("S1", sampleResponse.SampleNumber);
        Assert.Equal("Internal 1", sampleResponse.InternalDescription);

        var schemeResponse = new PreparationSchemeResponse(
            schemeId, "PT0060", "Mastitis bovine", "17152A/BA", 9, "Keep chilled",
            diaryEntry, prepStart, prepComplete, [sampleResponse]);

        Assert.Equal(schemeId, schemeResponse.MonthlyDistributionSchemeId);
        Assert.Equal("PT0060", schemeResponse.SchemeIdentifier);
        Assert.Equal("Mastitis bovine", schemeResponse.SchemeName);
        Assert.Equal("17152A/BA", schemeResponse.DistributionReferenceFull);
        Assert.Equal(9, schemeResponse.NumberOfSetsRequired);
        Assert.Equal("Keep chilled", schemeResponse.SamplePackingInstructions);
        Assert.Equal(diaryEntry, schemeResponse.DiaryEntry);
        Assert.Equal(prepStart, schemeResponse.PrepStartDate);
        Assert.Equal(prepComplete, schemeResponse.PrepCompleteDate);
        Assert.Same(sampleResponse, Assert.Single(schemeResponse.Samples));

        var sheetResponse = new PreparationSheetResponse(monthlyDistributionId, 2026, 4, [schemeResponse]);
        Assert.Equal(monthlyDistributionId, sheetResponse.MonthlyDistributionId);
        Assert.Equal(2026, sheetResponse.YearId);
        Assert.Equal(4, sheetResponse.MonthId);
        Assert.Same(schemeResponse, Assert.Single(sheetResponse.Schemes));

        var stageUpdateRequest = new PreparationStageUpdateRequest(schemeId, true, false, true);
        Assert.Equal(schemeId, stageUpdateRequest.MonthlyDistributionSchemeId);
        Assert.True(stageUpdateRequest.DiaryEntryChecked);
        Assert.False(stageUpdateRequest.PrepStartChecked);
        Assert.True(stageUpdateRequest.PrepCompleteChecked);

        var saveRequest = new PreparationSaveRequest([stageUpdateRequest]);
        Assert.Same(stageUpdateRequest, Assert.Single(saveRequest.Updates));

        var saveResult = new PreparationSaveResult(true);
        Assert.True(saveResult.Success);
    }

    [Fact]
    public void PreparationEntities_ExposeEveryProperty()
    {
        var monthlyDistributionId = Guid.NewGuid();
        var schemeId = Guid.NewGuid();
        var sampleId = Guid.NewGuid();
        var diaryEntry = new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc);
        var prepStart = new DateTime(2026, 1, 2, 9, 0, 0, DateTimeKind.Utc);
        var prepComplete = new DateTime(2026, 1, 3, 9, 0, 0, DateTimeKind.Utc);

        var sample = new PreparationSampleEntity
        {
            MonthlyDistributionSchemeId = schemeId,
            SampleId = sampleId,
            SampleNumber = "S1",
            InternalDescription = "Internal 1",
        };
        Assert.Equal(schemeId, sample.MonthlyDistributionSchemeId);
        Assert.Equal(sampleId, sample.SampleId);
        Assert.Equal("S1", sample.SampleNumber);
        Assert.Equal("Internal 1", sample.InternalDescription);

        var scheme = new PreparationSchemeEntity
        {
            MonthlyDistributionSchemeId = schemeId,
            SchemeIdentifier = "PT0060",
            SchemeName = "Mastitis bovine",
            DistributionReference = "17152",
            DistributionReferenceSuffix = "A",
            ScheduleCode = "BA",
            NumberOfSetsRequired = 9,
            SamplePackingInstructions = "Keep chilled",
            DiaryEntry = diaryEntry,
            PrepStartDate = prepStart,
            PrepCompleteDate = prepComplete,
            Samples = [sample],
        };

        Assert.Equal(schemeId, scheme.MonthlyDistributionSchemeId);
        Assert.Equal("PT0060", scheme.SchemeIdentifier);
        Assert.Equal("Mastitis bovine", scheme.SchemeName);
        Assert.Equal("17152", scheme.DistributionReference);
        Assert.Equal("A", scheme.DistributionReferenceSuffix);
        Assert.Equal("BA", scheme.ScheduleCode);
        Assert.Equal(9, scheme.NumberOfSetsRequired);
        Assert.Equal("Keep chilled", scheme.SamplePackingInstructions);
        Assert.Equal(diaryEntry, scheme.DiaryEntry);
        Assert.Equal(prepStart, scheme.PrepStartDate);
        Assert.Equal(prepComplete, scheme.PrepCompleteDate);
        Assert.Same(sample, Assert.Single(scheme.Samples));
        Assert.Equal("17152A/BA", scheme.DistributionReferenceFull);

        var header = new PreparationHeaderEntity { YearId = 2026, MonthId = 4 };
        Assert.Equal(2026, header.YearId);
        Assert.Equal(4, header.MonthId);

        var sheet = new PreparationSheetData(monthlyDistributionId, 2026, 4, [scheme]);
        Assert.Equal(monthlyDistributionId, sheet.MonthlyDistributionId);
        Assert.Equal(2026, sheet.YearId);
        Assert.Equal(4, sheet.MonthId);
        Assert.Same(scheme, Assert.Single(sheet.Schemes));

        var stageUpdate = new PreparationStageUpdate(schemeId, true, false, true);
        Assert.Equal(schemeId, stageUpdate.MonthlyDistributionSchemeId);
        Assert.True(stageUpdate.DiaryEntryChecked);
        Assert.False(stageUpdate.PrepStartChecked);
        Assert.True(stageUpdate.PrepCompleteChecked);

        var outcome = new PreparationSaveOutcome(true);
        Assert.True(outcome.Success);
    }

    [Fact]
    public void PackagingCustomsResult_ExposesEveryProperty()
    {
        var document = new PackagingCustomsResult([1, 2], "Customs.docx", null);
        var failure = new PackagingCustomsResult(null, string.Empty, "Please select schemes to print");

        Assert.Equal([1, 2], document.Content);
        Assert.Equal("Customs.docx", document.FileName);
        Assert.Null(document.Error);
        Assert.Null(failure.Content);
        Assert.Equal(string.Empty, failure.FileName);
        Assert.Equal("Please select schemes to print", failure.Error);
    }
}
