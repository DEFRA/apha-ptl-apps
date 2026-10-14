using System.Data;
using Dapper;
using PTL.Core.Contract;
using PTL.Core.Customer;
using PTL.Core.Distribution;
using PTL.Core.Lookup;
using PTL.Core.Participant;
using PTL.Core.Scheme;
using PTL.Data.Infrastructure;
using CoreContract = PTL.Core.Contract.Contract;
using CoreCustomer = PTL.Core.Customer.Customer;
using CoreParticipant = PTL.Core.Participant.Participant;
using CoreScheme = PTL.Core.Scheme.Scheme;

namespace PTL.Data.Tests.Infrastructure;

// Exercises DapperColumnMappings against in-memory DataTable readers (no live SQL Server needed) -
// proves the fldXxx (and "Readonly" alias) column-to-property mappings actually round-trip through
// Dapper's materialiser, the same failure mode that silently dropped every property when EF Core's
// column mappings were removed and nothing replaced them (see DapperColumnMappings' own remarks).
public class DapperColumnMappingsTests
{
    public DapperColumnMappingsTests() => DapperColumnMappings.Register();

    [Fact]
    public void Register_MapsCustomer_ColumnsToProperties()
    {
        var customerId = Guid.NewGuid();
        var countryId = Guid.NewGuid();
        var table = new DataTable();
        table.Columns.Add("fldCustomerId", typeof(Guid));
        table.Columns.Add("fldQalNumber", typeof(string));
        table.Columns.Add("fldName", typeof(string));
        table.Columns.Add("fldOrganisation", typeof(string));
        table.Columns.Add("fldCountryId", typeof(Guid));
        table.Columns.Add("fldIsActive", typeof(bool));
        table.Rows.Add(customerId, "QAL0001", "Test Customer", "Test Org", countryId, true);

        var customer = Parse<CoreCustomer>(table).Single();

        Assert.Equal(customerId, customer.CustomerId);
        Assert.Equal("QAL0001", customer.QalNumber);
        Assert.Equal("Test Customer", customer.Name);
        Assert.Equal("Test Org", customer.Organisation);
        Assert.Equal(countryId, customer.CountryId);
        Assert.True(customer.IsActive);
    }

    [Fact]
    public void Register_MapsCustomerSummary_ColumnsToProperties()
    {
        var customerId = Guid.NewGuid();
        var table = new DataTable();
        table.Columns.Add("fldCustomerId", typeof(Guid));
        table.Columns.Add("fldQalNumber", typeof(string));
        table.Columns.Add("fldName", typeof(string));
        table.Columns.Add("fldOrganisation", typeof(string));
        table.Columns.Add("fldIsActive", typeof(bool));
        table.Rows.Add(customerId, "QAL0002", "Summary Customer", "Summary Org", false);

        var summary = Parse<CustomerSummaryEntity>(table).Single();

        Assert.Equal(customerId, summary.CustomerId);
        Assert.Equal("QAL0002", summary.QalNumber);
        Assert.Equal("Summary Customer", summary.Name);
        Assert.Equal("Summary Org", summary.Organisation);
        Assert.False(summary.IsActive);
    }

    [Fact]
    public void Register_MapsParticipantAndSummary_ColumnsToProperties()
    {
        var participantId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var table = new DataTable();
        table.Columns.Add("fldParticipantId", typeof(Guid));
        table.Columns.Add("fldCustomerId", typeof(Guid));
        table.Columns.Add("fldLabCode", typeof(string));
        table.Columns.Add("fldLabName", typeof(string));
        table.Columns.Add("fldContactName", typeof(string));
        table.Columns.Add("fldIsActive", typeof(bool));
        table.Rows.Add(participantId, customerId, "LAB001", "Test Lab", "A Contact", true);

        var participant = Parse<CoreParticipant>(table).Single();
        Assert.Equal(participantId, participant.ParticipantId);
        Assert.Equal(customerId, participant.CustomerId);
        Assert.Equal("LAB001", participant.LabCode);
        Assert.True(participant.IsActive);

        var summary = Parse<ParticipantSummaryEntity>(table).Single();
        Assert.Equal(participantId, summary.ParticipantId);
        Assert.Equal("Test Lab", summary.LabName);
        Assert.Equal("A Contact", summary.ContactName);
    }

    [Fact]
    public void Register_MapsContract_IncludingReadonlyAliasAndJoinedColumns()
    {
        var contractId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var table = new DataTable();
        table.Columns.Add("Readonly", typeof(bool));
        table.Columns.Add("fldContractId", typeof(Guid));
        table.Columns.Add("fldCustomerId", typeof(Guid));
        table.Columns.Add("fldCustomerName", typeof(string));
        table.Columns.Add("fldQALNumber", typeof(string));
        table.Columns.Add("fldYearId", typeof(int));
        table.Columns.Add("fldIsActive", typeof(bool));
        table.Rows.Add(true, contractId, customerId, "Joined Customer", "QAL0003", 2026, true);

        var contract = Parse<CoreContract>(table).Single();

        Assert.True(contract.IsReadOnly);
        Assert.Equal(contractId, contract.ContractId);
        Assert.Equal(customerId, contract.CustomerId);
        Assert.Equal("Joined Customer", contract.CustomerName);
        Assert.Equal("QAL0003", contract.QalNumber);
        Assert.Equal(2026, contract.YearId);
        Assert.True(contract.IsActive);
    }

    [Fact]
    public void Register_MapsContractSummary_ColumnsToProperties()
    {
        var contractId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var table = new DataTable();
        table.Columns.Add("fldContractId", typeof(Guid));
        table.Columns.Add("fldCustomerId", typeof(Guid));
        table.Columns.Add("fldYearId", typeof(int));
        table.Columns.Add("fldIsActive", typeof(bool));
        table.Columns.Add("fldSuffix", typeof(string));
        table.Rows.Add(contractId, customerId, 2026, true, "A");

        var summary = Parse<ContractSummaryEntity>(table).Single();

        Assert.Equal(contractId, summary.ContractId);
        Assert.Equal(2026, summary.YearId);
        Assert.Equal("A", summary.Suffix);
    }

    [Fact]
    public void Register_MapsScheme_IncludingReadonlyAliasAndJoinedSampleNoSequence()
    {
        var schemeId = Guid.NewGuid();
        var table = new DataTable();
        table.Columns.Add("Readonly", typeof(bool));
        table.Columns.Add("fldSchemeId", typeof(Guid));
        table.Columns.Add("fldYearId", typeof(int));
        table.Columns.Add("fldName", typeof(string));
        table.Columns.Add("fldSampleNoSequence", typeof(int));
        table.Columns.Add("fldCustomsDocumentDescription", typeof(string));
        table.Columns.Add("fldCustomsDocumentVolume", typeof(string));
        table.Rows.Add(false, schemeId, 2026, "Test Scheme", 7, "A description", "10kg");

        var scheme = Parse<CoreScheme>(table).Single();

        Assert.False(scheme.IsReadOnly);
        Assert.Equal(schemeId, scheme.SchemeId);
        Assert.Equal("Test Scheme", scheme.Name);
        Assert.Equal(7, scheme.SampleNoSequence);
        Assert.Equal("A description", scheme.CustomsDescription);
        Assert.Equal("10kg", scheme.CustomsVolume);
    }

    [Fact]
    public void Register_MapsSchemeSummaryAndHistory_ColumnsToProperties()
    {
        var sharedId = Guid.NewGuid();
        var currentSchemeId = Guid.NewGuid();
        var table = new DataTable();
        table.Columns.Add("fldSharedId", typeof(Guid));
        table.Columns.Add("fldYearId", typeof(int));
        table.Columns.Add("fldCurrentSchemeId", typeof(Guid));
        table.Columns.Add("fldCurrentIdentifier", typeof(string));
        table.Columns.Add("fldCurrentName", typeof(string));
        table.Rows.Add(sharedId, 2026, currentSchemeId, "PT0001", "Current Scheme");

        var summary = Parse<SchemeSummaryEntity>(table).Single();
        Assert.Equal(sharedId, summary.SharedId);
        Assert.Equal(currentSchemeId, summary.CurrentSchemeId);
        Assert.Equal("Current Scheme", summary.CurrentName);

        var history = Parse<SchemeHistoryEntity>(table).Single();
        Assert.Equal(currentSchemeId, history.SchemeId);
        Assert.Equal(sharedId, history.SharedId);
        Assert.Equal("PT0001", history.Identifier);
        Assert.Equal("Current Scheme", history.Name);
    }

    [Fact]
    public void Register_MapsSchemeCurrencyAndPostagePricingPlan_ColumnsToProperties()
    {
        var schemeCurrencyId = Guid.NewGuid();
        var currencyTable = new DataTable();
        currencyTable.Columns.Add("fldSchemeCurrencyId", typeof(Guid));
        currencyTable.Columns.Add("fldPrice", typeof(decimal));
        currencyTable.Columns.Add("fldCurrencyName", typeof(string));
        currencyTable.Columns.Add("fldCurrencySymbol", typeof(string));
        currencyTable.Rows.Add(schemeCurrencyId, 12.5m, "British Pound", "£");

        var schemeCurrency = Parse<SchemeCurrencyEntity>(currencyTable).Single();
        Assert.Equal(schemeCurrencyId, schemeCurrency.SchemeCurrencyId);
        Assert.Equal(12.5m, schemeCurrency.Price);
        Assert.Equal("£", schemeCurrency.CurrencySymbol);

        var postageId = Guid.NewGuid();
        var postageTable = new DataTable();
        postageTable.Columns.Add("fldPostageId", typeof(Guid));
        postageTable.Columns.Add("fldName", typeof(string));
        postageTable.Columns.Add("fldUkPrice", typeof(decimal));
        postageTable.Columns.Add("fldYearId", typeof(int));
        postageTable.Rows.Add(postageId, "Standard", 5.5m, 2026);

        var postage = Parse<PostagePricingPlanEntity>(postageTable).Single();
        Assert.Equal(postageId, postage.PostageId);
        Assert.Equal("Standard", postage.Name);
        Assert.Equal(2026, postage.YearId);
    }

    [Fact]
    public void Register_MapsLookupEntities_ColumnsToProperties()
    {
        var currencyId = Guid.NewGuid();
        var currencyTable = new DataTable();
        currencyTable.Columns.Add("fldCurrencyId", typeof(Guid));
        currencyTable.Columns.Add("fldName", typeof(string));
        currencyTable.Columns.Add("fldSymbol", typeof(string));
        currencyTable.Rows.Add(currencyId, "British Pound", "£");
        var currency = Parse<CurrencyEntity>(currencyTable).Single();
        Assert.Equal(currencyId, currency.CurrencyId);
        Assert.Equal("£", currency.Symbol);

        var customerTypeTable = new DataTable();
        customerTypeTable.Columns.Add("fldCustomerTypeId", typeof(Guid));
        customerTypeTable.Columns.Add("fldCustomerType", typeof(string));
        customerTypeTable.Rows.Add(Guid.NewGuid(), "Commercial");
        Assert.Equal("Commercial", Parse<CustomerTypeEntity>(customerTypeTable).Single().CustomerType);

        var vatRatingTable = new DataTable();
        vatRatingTable.Columns.Add("fldVatRatingId", typeof(Guid));
        vatRatingTable.Columns.Add("fldVatRating", typeof(string));
        vatRatingTable.Rows.Add(Guid.NewGuid(), "Standard");
        Assert.Equal("Standard", Parse<VatRatingEntity>(vatRatingTable).Single().VatRating);

        var labTypeTable = new DataTable();
        labTypeTable.Columns.Add("fldLabTypeId", typeof(Guid));
        labTypeTable.Columns.Add("fldName", typeof(string));
        labTypeTable.Rows.Add(Guid.NewGuid(), "Reference");
        Assert.Equal("Reference", Parse<LabTypeEntity>(labTypeTable).Single().Name);

        var yearTable = new DataTable();
        yearTable.Columns.Add("fldYearId", typeof(int));
        yearTable.Columns.Add("fldYear", typeof(string));
        yearTable.Rows.Add(2026, "2026");
        Assert.Equal(2026, Parse<YearEntity>(yearTable).Single().YearId);
    }

    [Fact]
    public void Register_IgnoresUnmappedColumns_InsteadOfThrowing()
    {
        // Mirrors spgaCountry's real result shape: fldCountryTypeId/fldCountryType/
        // fldAllocationCount exist in the stored procedure output but have no matching property -
        // ResolveProperty must return null for them so Dapper simply skips the column.
        var countryId = Guid.NewGuid();
        var table = new DataTable();
        table.Columns.Add("fldCountryId", typeof(Guid));
        table.Columns.Add("fldCountry", typeof(string));
        table.Columns.Add("fldCountryTypeId", typeof(Guid));
        table.Columns.Add("fldCountryType", typeof(string));
        table.Columns.Add("fldAllocationCount", typeof(int));
        table.Rows.Add(countryId, "United Kingdom", Guid.NewGuid(), "Domestic", 3);

        var country = Parse<CountryEntity>(table).Single();

        Assert.Equal(countryId, country.CountryId);
        Assert.Equal("United Kingdom", country.Country);
    }

    // The next eight tests use the exact result-set column lists from spgIntendedResults.sql and
    // spgMonthlyDistributionScheme.sql, including the columns those procedures return that the
    // Sample Numbers screen has no property for - they must be skipped, not throw.
    [Fact]
    public void Register_MapsIntendedResultsHeader_ColumnsToProperties()
    {
        var schemeId = Guid.NewGuid();
        var tabulationId = Guid.NewGuid();
        var table = new DataTable();
        table.Columns.Add("fldMonthlyDistributionSchemeId", typeof(Guid));
        table.Columns.Add("fldComments", typeof(string));
        table.Columns.Add("fldHasIntendedResults", typeof(bool));
        table.Columns.Add("fldHasSampleNumbersDefined", typeof(bool));
        table.Columns.Add("fldTestConsultantTabulationId", typeof(Guid));
        table.Columns.Add("fldStoreRatings", typeof(bool));
        table.Columns.Add("fldSchemeVersionDate", typeof(DateTime));
        table.Columns.Add("fldDateOfReceipt", typeof(bool));
        table.Columns.Add("fldStorageConditions", typeof(bool));
        table.Columns.Add("fldConditionOnReceipt", typeof(bool));
        table.Columns.Add("fldInstructions", typeof(string));
        table.Columns.Add("fldRequiresAssessment", typeof(bool));
        table.Columns.Add("fldIsReadyForAssessor", typeof(DateTime));
        table.Rows.Add(
            schemeId, "Some comments", true, true, tabulationId, true,
            new DateTime(2026, 1, 1), true, true, false, "Instructions",
            true, new DateTime(2026, 4, 1));

        var header = Parse<IntendedResultsHeaderEntity>(table).Single();

        Assert.Equal(schemeId, header.MonthlyDistributionSchemeId);
        Assert.Equal("Some comments", header.Comments);
        Assert.True(header.HasIntendedResults);
        Assert.True(header.HasSampleNumbersDefined);
        Assert.Equal(tabulationId, header.TestConsultantTabulationId);
        Assert.True(header.StoreRatings);
        Assert.True(header.RequiresAssessment);
        Assert.Equal(new DateTime(2026, 4, 1), header.IsReadyForAssessor);
        Assert.Equal(new DateTime(2026, 1, 1), header.SchemeVersionDate);
        Assert.True(header.DateOfReceipt);
        Assert.True(header.StorageConditions);
        Assert.False(header.ConditionOnReceipt);
        Assert.Equal("Instructions", header.Instructions);
    }

    [Fact]
    public void Register_MapsIntendedResultsHeader_NullIsReadyForAssessor()
    {
        var table = new DataTable();
        table.Columns.Add("fldMonthlyDistributionSchemeId", typeof(Guid));
        table.Columns.Add("fldIsReadyForAssessor", typeof(DateTime));
        table.Rows.Add(Guid.NewGuid(), DBNull.Value);

        Assert.Null(Parse<IntendedResultsHeaderEntity>(table).Single().IsReadyForAssessor);
    }

    [Fact]
    public void Register_MapsDistributionSample_ColumnsToProperties()
    {
        var sampleId = Guid.NewGuid();
        var schemeId = Guid.NewGuid();
        var table = new DataTable();
        table.Columns.Add("fldDistributionSchemeId", typeof(Guid));
        table.Columns.Add("fldSampleId", typeof(Guid));
        table.Columns.Add("fldSampleNumber", typeof(string));
        table.Columns.Add("fldInternalDescription", typeof(string));
        table.Columns.Add("fldExternalDescription", typeof(string));
        table.Columns.Add("fldTabulationDescription", typeof(string));
        table.Columns.Add("fldTestConsultantsDescription", typeof(string));
        table.Rows.Add(schemeId, sampleId, "S1", "Internal", "External", "Tabulation", "TC");

        var sample = Parse<DistributionSampleEntity>(table).Single();

        Assert.Equal(sampleId, sample.SampleId);
        Assert.Equal(schemeId, sample.DistributionSchemeId);
        Assert.Equal("S1", sample.SampleNumber);
        Assert.Equal("Internal", sample.InternalDescription);
        Assert.Equal("External", sample.ExternalDescription);
        Assert.Equal("Tabulation", sample.TabulationDescription);
        Assert.Equal("TC", sample.TestConsultantsDescription);
    }

    [Fact]
    public void Register_MapsDistributionSampleTest_ColumnsToProperties()
    {
        var sampleTestId = Guid.NewGuid();
        var sampleId = Guid.NewGuid();
        var testId = Guid.NewGuid();
        var table = new DataTable();
        table.Columns.Add("fldDistributionSchemeId", typeof(Guid));
        table.Columns.Add("fldSampleId", typeof(Guid));
        table.Columns.Add("fldSampleTestId", typeof(Guid));
        table.Columns.Add("fldTestId", typeof(Guid));
        table.Columns.Add("fldIsApplicable", typeof(bool));
        table.Rows.Add(Guid.NewGuid(), sampleId, sampleTestId, testId, true);

        var sampleTest = Parse<DistributionSampleTestEntity>(table).Single();

        Assert.Equal(sampleTestId, sampleTest.SampleTestId);
        Assert.Equal(sampleId, sampleTest.SampleId);
        Assert.Equal(testId, sampleTest.TestId);
        Assert.True(sampleTest.IsApplicable);
    }

    [Fact]
    public void Register_MapsDistributionTest_ColumnsToProperties()
    {
        var testId = Guid.NewGuid();
        var testTypeId = Guid.NewGuid();
        var table = new DataTable();
        table.Columns.Add("fldDistributionSchemeId", typeof(Guid));
        table.Columns.Add("fldTestId", typeof(Guid));
        table.Columns.Add("fldTestTypeId", typeof(Guid));
        table.Columns.Add("TestName", typeof(string));
        table.Columns.Add("fldOrder", typeof(int));
        table.Rows.Add(Guid.NewGuid(), testId, testTypeId, "Somatic cell count", 2);

        var test = Parse<DistributionTestEntity>(table).Single();

        Assert.Equal(testId, test.TestId);
        Assert.Equal(testTypeId, test.TestTypeId);
        Assert.Equal("Somatic cell count", test.TestName);
        Assert.Equal(2, test.Order);
    }

    [Fact]
    public void Register_MapsDistributionTestMethodItem_ColumnsToProperties()
    {
        var methodItemId = Guid.NewGuid();
        var testId = Guid.NewGuid();
        var table = new DataTable();
        table.Columns.Add("fldTestMethodItemId", typeof(Guid));
        table.Columns.Add("fldTestId", typeof(Guid));
        table.Columns.Add("fldTestMethodItemTypeId", typeof(Guid));
        table.Columns.Add("fldIntendedResult", typeof(string));
        table.Columns.Add("TestMethodItemTypeName", typeof(string));
        table.Columns.Add("FormatString", typeof(string));
        table.Columns.Add("ReturnValueType", typeof(string));
        table.Columns.Add("fldOrder", typeof(int));
        table.Columns.Add("fldIsApplicable", typeof(bool));
        table.Rows.Add(methodItemId, testId, Guid.NewGuid(), "3.2", "Method A", "{0:N2}", "Numeric", 1, true);

        var methodItem = Parse<DistributionTestMethodItemEntity>(table).Single();

        Assert.Equal(methodItemId, methodItem.TestMethodItemId);
        Assert.Equal(testId, methodItem.TestId);
        Assert.Equal("Method A", methodItem.Name);
        Assert.Equal(1, methodItem.Order);
    }

    [Fact]
    public void Register_MapsDistributionTestResultItem_ColumnsToProperties()
    {
        var resultItemId = Guid.NewGuid();
        var testId = Guid.NewGuid();
        var table = new DataTable();
        table.Columns.Add("fldTestResultItemId", typeof(Guid));
        table.Columns.Add("fldTestId", typeof(Guid));
        table.Columns.Add("fldTestResultItemTypeId", typeof(Guid));
        table.Columns.Add("TestResultItemTypeName", typeof(string));
        table.Columns.Add("FormatString", typeof(string));
        table.Columns.Add("ExpectedLength", typeof(int));
        table.Columns.Add("ReturnValueType", typeof(string));
        table.Columns.Add("fldOrder", typeof(int));
        table.Columns.Add("fldIsRange", typeof(bool));
        table.Rows.Add(resultItemId, testId, Guid.NewGuid(), "Result A", "{0:N2}", 6, "Numeric", 3, false);

        var resultItem = Parse<DistributionTestResultItemEntity>(table).Single();

        Assert.Equal(resultItemId, resultItem.TestResultItemId);
        Assert.Equal(testId, resultItem.TestId);
        Assert.Equal("Result A", resultItem.Name);
        Assert.Equal(3, resultItem.Order);
    }

    [Fact]
    public void Register_MapsDistributionCategoryItem_ColumnsToProperties()
    {
        var categoryItemId = Guid.NewGuid();
        var testId = Guid.NewGuid();
        var table = new DataTable();
        table.Columns.Add("fldCategoryItemId", typeof(Guid));
        table.Columns.Add("fldTestId", typeof(Guid));
        table.Columns.Add("fldCategoryItemTypeId", typeof(Guid));
        table.Columns.Add("fldOrder", typeof(int));
        table.Columns.Add("fldName", typeof(string));
        table.Rows.Add(categoryItemId, testId, Guid.NewGuid(), 4, "Appearance");

        var categoryItem = Parse<DistributionCategoryItemEntity>(table).Single();

        Assert.Equal(categoryItemId, categoryItem.CategoryItemId);
        Assert.Equal(testId, categoryItem.TestId);
        Assert.Equal(4, categoryItem.Order);
        Assert.Equal("Appearance", categoryItem.Name);
    }

    [Fact]
    public void Register_MapsDistributionCriterionItem_ColumnsToProperties()
    {
        var criterionItemId = Guid.NewGuid();
        var categoryItemId = Guid.NewGuid();
        var testId = Guid.NewGuid();
        var table = new DataTable();
        table.Columns.Add("fldCriterionItemId", typeof(Guid));
        table.Columns.Add("fldCategoryItemId", typeof(Guid));
        table.Columns.Add("fldCriterionItemTypeId", typeof(Guid));
        table.Columns.Add("fldOrder", typeof(int));
        table.Columns.Add("fldTestId", typeof(Guid));
        table.Columns.Add("fldName", typeof(string));
        table.Rows.Add(criterionItemId, categoryItemId, Guid.NewGuid(), 5, testId, "Colour");

        var criterionItem = Parse<DistributionCriterionItemEntity>(table).Single();

        Assert.Equal(criterionItemId, criterionItem.CriterionItemId);
        Assert.Equal(categoryItemId, criterionItem.CategoryItemId);
        Assert.Equal(testId, criterionItem.TestId);
        Assert.Equal(5, criterionItem.Order);
        Assert.Equal("Colour", criterionItem.Name);
    }

    // spgPreparationSheet.sql's 3 result sets - header row, scheme rows, sample rows.
    [Fact]
    public void Register_MapsPreparationHeader_ColumnsToProperties()
    {
        var table = new DataTable();
        table.Columns.Add("fldYearId", typeof(int));
        table.Columns.Add("fldMonthId", typeof(int));
        table.Rows.Add(2026, 4);

        var header = Parse<PreparationHeaderEntity>(table).Single();

        Assert.Equal(2026, header.YearId);
        Assert.Equal(4, header.MonthId);
    }

    [Fact]
    public void Register_MapsPreparationScheme_ColumnsToProperties()
    {
        var schemeId = Guid.NewGuid();
        var table = new DataTable();
        table.Columns.Add("fldMonthlyDistributionSchemeId", typeof(Guid));
        table.Columns.Add("fldIdentifier", typeof(string));
        table.Columns.Add("fldName", typeof(string));
        table.Columns.Add("fldDistributionReference", typeof(string));
        table.Columns.Add("fldDistributionReferenceSuffix", typeof(string));
        table.Columns.Add("fldScheduleCode", typeof(string));
        table.Columns.Add("fldNumberOfSetsRequired", typeof(int));
        table.Columns.Add("fldSamplePackingInstructions", typeof(string));
        table.Columns.Add("fldDiaryEntry", typeof(DateTime));
        table.Columns.Add("fldPrepStartDate", typeof(DateTime));
        table.Columns.Add("fldPrepCompleteDate", typeof(DateTime));
        table.Rows.Add(
            schemeId, "PT0060", "Mastitis bovine", "17152", "A", "BA", 9, "Keep chilled",
            DBNull.Value, DBNull.Value, DBNull.Value);

        var scheme = Parse<PreparationSchemeEntity>(table).Single();

        Assert.Equal(schemeId, scheme.MonthlyDistributionSchemeId);
        Assert.Equal("PT0060", scheme.SchemeIdentifier);
        Assert.Equal("Mastitis bovine", scheme.SchemeName);
        Assert.Equal("17152", scheme.DistributionReference);
        Assert.Equal("A", scheme.DistributionReferenceSuffix);
        Assert.Equal("BA", scheme.ScheduleCode);
        Assert.Equal(9, scheme.NumberOfSetsRequired);
        Assert.Equal("Keep chilled", scheme.SamplePackingInstructions);
        Assert.Null(scheme.DiaryEntry);
        Assert.Null(scheme.PrepStartDate);
        Assert.Null(scheme.PrepCompleteDate);
    }

    [Fact]
    public void Register_MapsPreparationScheme_SetDates_ColumnsToProperties()
    {
        var diaryEntry = new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc);
        var prepStart = new DateTime(2026, 1, 2, 9, 0, 0, DateTimeKind.Utc);
        var prepComplete = new DateTime(2026, 1, 3, 9, 0, 0, DateTimeKind.Utc);
        var table = new DataTable();
        table.Columns.Add("fldMonthlyDistributionSchemeId", typeof(Guid));
        table.Columns.Add("fldDiaryEntry", typeof(DateTime));
        table.Columns.Add("fldPrepStartDate", typeof(DateTime));
        table.Columns.Add("fldPrepCompleteDate", typeof(DateTime));
        table.Rows.Add(Guid.NewGuid(), diaryEntry, prepStart, prepComplete);

        var scheme = Parse<PreparationSchemeEntity>(table).Single();

        Assert.Equal(diaryEntry, scheme.DiaryEntry);
        Assert.Equal(prepStart, scheme.PrepStartDate);
        Assert.Equal(prepComplete, scheme.PrepCompleteDate);
    }

    [Fact]
    public void Register_MapsPreparationSample_ColumnsToProperties()
    {
        var schemeId = Guid.NewGuid();
        var sampleId = Guid.NewGuid();
        var table = new DataTable();
        table.Columns.Add("fldMonthlyDistributionSchemeId", typeof(Guid));
        table.Columns.Add("fldSampleId", typeof(Guid));
        table.Columns.Add("fldSampleNumber", typeof(string));
        table.Columns.Add("fldInternalDescription", typeof(string));
        table.Rows.Add(schemeId, sampleId, "S1", "Internal 1");

        var sample = Parse<PreparationSampleEntity>(table).Single();

        Assert.Equal(schemeId, sample.MonthlyDistributionSchemeId);
        Assert.Equal(sampleId, sample.SampleId);
        Assert.Equal("S1", sample.SampleNumber);
        Assert.Equal("Internal 1", sample.InternalDescription);
    }

    [Fact]
    public void Register_MapsPackagingGroup_ColumnsToProperties()
    {
        var groupAddressId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var distributionDate = new DateTime(2026, 5, 13);
        var table = new DataTable();
        table.Columns.Add("fldGroupAddressId", typeof(Guid));
        table.Columns.Add("fldParticipantId", typeof(Guid));
        table.Columns.Add("fldIdentifier", typeof(string));
        table.Columns.Add("fldLabCode", typeof(string));
        table.Columns.Add("fldLabName", typeof(string));
        table.Columns.Add("fldPackingInfo", typeof(string));
        table.Columns.Add("fldDistributionDate", typeof(DateTime));
        table.Columns.Add("fldContactName", typeof(string));
        table.Columns.Add("fldOrganisation", typeof(string));
        table.Columns.Add("fldAddress1", typeof(string));
        table.Columns.Add("fldAddress2", typeof(string));
        table.Columns.Add("fldAddress3", typeof(string));
        table.Columns.Add("fldAddress4", typeof(string));
        table.Columns.Add("fldAddress5", typeof(string));
        table.Columns.Add("fldTelephone", typeof(string));
        table.Columns.Add("fldCountry", typeof(string));
        table.Rows.Add(
            groupAddressId, participantId, "G1", "L001", "Lab One", "Keep cool", distributionDate,
            "A Contact", "An Org", "1 High St", "Town", "County", "Four", "Five", "0123", "UK");

        var group = Parse<PackagingGroupEntity>(table).Single();

        Assert.Equal(groupAddressId, group.GroupAddressId);
        Assert.Equal(participantId, group.ParticipantId);
        Assert.Equal("G1", group.Identifier);
        Assert.Equal("L001", group.LabCode);
        Assert.Equal("Lab One", group.LabName);
        Assert.Equal("Keep cool", group.PackingInfo);
        Assert.Equal(distributionDate, group.DistributionDate);
        Assert.Equal("A Contact", group.ContactName);
        Assert.Equal("An Org", group.Organisation);
        Assert.Equal("1 High St", group.Address1);
        Assert.Equal("Town", group.Address2);
        Assert.Equal("County", group.Address3);
        Assert.Equal("Four", group.Address4);
        Assert.Equal("Five", group.Address5);
        Assert.Equal("0123", group.Telephone);
        Assert.Equal("UK", group.Country);
    }

    // Result set 2 of spgWeeklyPackagingSheet returns only the date; everything else keeps its default.
    [Fact]
    public void Register_MapsPackagingGroup_DayRowWithOnlyADateKeepsDefaults()
    {
        var table = new DataTable();
        table.Columns.Add("fldDistributionDate", typeof(DateTime));
        table.Columns.Add("fldPackingInfo", typeof(string));
        table.Columns.Add("fldLabName", typeof(string));
        table.Rows.Add(new DateTime(2026, 5, 14), DBNull.Value, DBNull.Value);

        var group = Parse<PackagingGroupEntity>(table).Single();

        Assert.Equal(new DateTime(2026, 5, 14), group.DistributionDate);
        Assert.Equal(string.Empty, group.PackingInfo);
        Assert.Equal(string.Empty, group.LabName);
        Assert.Equal(Guid.Empty, group.GroupAddressId);
    }

    [Fact]
    public void Register_MapsPackagingItem_UsingFldNameAndFldIdentifierForTheScheme()
    {
        var schemeId = Guid.NewGuid();
        var groupAddressId = Guid.NewGuid();
        var table = new DataTable();
        table.Columns.Add("fldMonthlyDistributionSchemeId", typeof(Guid));
        table.Columns.Add("fldGroupAddressId", typeof(Guid));
        table.Columns.Add("fldPreparationComplete", typeof(bool));
        table.Columns.Add("fldName", typeof(string));
        table.Columns.Add("fldIdentifier", typeof(string));
        table.Columns.Add("fldDistributionReference", typeof(string));
        table.Columns.Add("fldDistributionReferenceSuffix", typeof(string));
        table.Columns.Add("fldScheduleCode", typeof(string));
        table.Columns.Add("fldPackingInfo", typeof(string));
        table.Columns.Add("fldNumberOfSetsRequired", typeof(int));
        table.Columns.Add("fldDistributionDate", typeof(DateTime));
        table.Columns.Add("fldCustomsDocumentDescription", typeof(string));
        table.Columns.Add("fldCustomsDocumentVolume", typeof(string));
        table.Columns.Add("fldNumberOfSamples", typeof(int));
        table.Rows.Add(
            schemeId, groupAddressId, true, "Mastitis bovine", "PT0060", "17152", "A", "BA", "Scheme pack", 3,
            new DateTime(2026, 5, 13), "Blood samples", "5ml", 4);

        var item = Parse<PackagingItemEntity>(table).Single();

        Assert.Equal(schemeId, item.MonthlyDistributionSchemeId);
        Assert.Equal(groupAddressId, item.GroupAddressId);
        Assert.True(item.PreparationComplete);
        Assert.Equal("Mastitis bovine", item.SchemeName);
        Assert.Equal("PT0060", item.SchemeIdentifier);
        Assert.Equal("17152", item.DistributionReference);
        Assert.Equal("A", item.DistributionReferenceSuffix);
        Assert.Equal("BA", item.ScheduleCode);
        Assert.Equal("Scheme pack", item.PackingInfo);
        Assert.Equal(3, item.NumberOfSetsRequired);
        Assert.Equal(new DateTime(2026, 5, 13), item.DistributionDate);
        Assert.Equal("Blood samples", item.CustomsDocumentDescription);
        Assert.Equal("5ml", item.CustomsDocumentVolume);
        Assert.Equal(4, item.NumberOfSamples);
    }

    // The Participant Combined result set names the scheme columns fldSchemeName/fldSchemeIdentifier;
    // PackagingItemEntity deliberately does not read those aliases.
    [Fact]
    public void Register_MapsPackagingItem_IgnoresTheSchemePrefixedAliases()
    {
        var table = new DataTable();
        table.Columns.Add("fldSchemeName", typeof(string));
        table.Columns.Add("fldSchemeIdentifier", typeof(string));
        table.Rows.Add("Ignored name", "Ignored id");

        var item = Parse<PackagingItemEntity>(table).Single();

        Assert.Equal(string.Empty, item.SchemeName);
        Assert.Equal(string.Empty, item.SchemeIdentifier);
    }

    [Fact]
    public void Register_MapsPackagingItem_NullColumnsKeepPropertyDefaults()
    {
        var table = new DataTable();
        table.Columns.Add("fldPreparationComplete", typeof(bool));
        table.Columns.Add("fldPackingInfo", typeof(string));
        table.Columns.Add("fldNumberOfSetsRequired", typeof(int));
        table.Columns.Add("fldNumberOfSamples", typeof(int));
        table.Rows.Add(DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value);

        var item = Parse<PackagingItemEntity>(table).Single();

        Assert.False(item.PreparationComplete);
        Assert.Equal(string.Empty, item.PackingInfo);
        Assert.Equal(0, item.NumberOfSetsRequired);
        Assert.Equal(0, item.NumberOfSamples);
    }

    [Fact]
    public void Register_MapsPackagingParticipant_ColumnsToProperties()
    {
        var groupAddressId = Guid.NewGuid();
        var schemeId = Guid.NewGuid();
        var distributionParticipantId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var packaged = new DateTime(2026, 5, 12, 9, 0, 0);
        var posted = new DateTime(2026, 5, 13, 9, 0, 0);
        var table = new DataTable();
        table.Columns.Add("fldGroupAddressId", typeof(Guid));
        table.Columns.Add("fldMonthlyDistributionSchemeId", typeof(Guid));
        table.Columns.Add("fldDistributionParticipantId", typeof(Guid));
        table.Columns.Add("fldParticipantId", typeof(Guid));
        table.Columns.Add("fldPackagedDate", typeof(DateTime));
        table.Columns.Add("fldPostedDate", typeof(DateTime));
        table.Columns.Add("fldLabCode", typeof(string));
        table.Columns.Add("fldLabName", typeof(string));
        table.Columns.Add("fldPackingInfo", typeof(string));
        table.Columns.Add("fldCustomerPackingInfo", typeof(string));
        table.Columns.Add("fldPSLevelPackingInfo", typeof(string));
        table.Columns.Add("fldNumberOfSetsRequired", typeof(int));
        table.Columns.Add("fldImportExportLicenceRequired", typeof(bool));
        table.Columns.Add("fldImportPermitReceived", typeof(bool));
        table.Columns.Add("fldCustomsCertificateRequired", typeof(bool));
        table.Columns.Add("fldContactName", typeof(string));
        table.Columns.Add("fldOrganisation", typeof(string));
        table.Columns.Add("fldAddress1", typeof(string));
        table.Columns.Add("fldAddress2", typeof(string));
        table.Columns.Add("fldAddress3", typeof(string));
        table.Columns.Add("fldAddress4", typeof(string));
        table.Columns.Add("fldAddress5", typeof(string));
        table.Columns.Add("fldTelephone", typeof(string));
        table.Columns.Add("fldCountry", typeof(string));
        table.Columns.Add("fldDistributionDate", typeof(DateTime));
        table.Rows.Add(
            groupAddressId, schemeId, distributionParticipantId, participantId, packaged, posted, "L001", "Lab One",
            "Participant pack", "Customer pack", "PS pack", 2, true, true, true, "A Contact", "An Org", "1 High St",
            "Town", "County", "Four", "Five", "0123", "UK", new DateTime(2026, 5, 13));

        var participant = Parse<PackagingParticipantEntity>(table).Single();

        Assert.Equal(groupAddressId, participant.GroupAddressId);
        Assert.Equal(schemeId, participant.MonthlyDistributionSchemeId);
        Assert.Equal(distributionParticipantId, participant.DistributionParticipantId);
        Assert.Equal(participantId, participant.ParticipantId);
        Assert.Equal(packaged, participant.PackagedDate);
        Assert.Equal(posted, participant.PostedDate);
        Assert.Equal("L001", participant.LabCode);
        Assert.Equal("Lab One", participant.LabName);
        Assert.Equal("Participant pack", participant.PackingInfo);
        Assert.Equal("Customer pack", participant.CustomerPackingInfo);
        Assert.Equal("PS pack", participant.PSLevelPackingInfo);
        Assert.Equal(2, participant.NumberOfSetsRequired);
        Assert.True(participant.ImportExportLicenceRequired);
        Assert.True(participant.ImportPermitReceived);
        Assert.True(participant.CustomsCertificateRequired);
        Assert.Equal("A Contact", participant.ContactName);
        Assert.Equal("An Org", participant.Organisation);
        Assert.Equal("1 High St", participant.Address1);
        Assert.Equal("Town", participant.Address2);
        Assert.Equal("County", participant.Address3);
        Assert.Equal("Four", participant.Address4);
        Assert.Equal("Five", participant.Address5);
        Assert.Equal("0123", participant.Telephone);
        Assert.Equal("UK", participant.Country);
        Assert.Equal(new DateTime(2026, 5, 13), participant.DistributionDate);
    }

    [Fact]
    public void Register_MapsPackagingParticipant_NullDatesAndTextKeepDefaults()
    {
        var table = new DataTable();
        table.Columns.Add("fldPackagedDate", typeof(DateTime));
        table.Columns.Add("fldPostedDate", typeof(DateTime));
        table.Columns.Add("fldPackingInfo", typeof(string));
        table.Columns.Add("fldCustomerPackingInfo", typeof(string));
        table.Columns.Add("fldImportExportLicenceRequired", typeof(bool));
        table.Rows.Add(DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value);

        var participant = Parse<PackagingParticipantEntity>(table).Single();

        Assert.Null(participant.PackagedDate);
        Assert.Null(participant.PostedDate);
        Assert.Equal(string.Empty, participant.PackingInfo);
        Assert.Equal(string.Empty, participant.CustomerPackingInfo);
        Assert.False(participant.ImportExportLicenceRequired);
    }

    [Fact]
    public void Register_MapsPackagingSchemeItem_UsingTheSchemePrefixedColumns()
    {
        var participantId = Guid.NewGuid();
        var distributionParticipantId = Guid.NewGuid();
        var schemeId = Guid.NewGuid();
        var packaged = new DateTime(2026, 5, 12, 9, 0, 0);
        var posted = new DateTime(2026, 5, 13, 9, 0, 0);
        var table = new DataTable();
        table.Columns.Add("fldParticipantId", typeof(Guid));
        table.Columns.Add("fldDistributionParticipantId", typeof(Guid));
        table.Columns.Add("fldMonthlyDistributionSchemeId", typeof(Guid));
        table.Columns.Add("fldPreparationComplete", typeof(bool));
        table.Columns.Add("fldDistributionReference", typeof(string));
        table.Columns.Add("fldDistributionReferenceSuffix", typeof(string));
        table.Columns.Add("fldScheduleCode", typeof(string));
        table.Columns.Add("fldPackagedDate", typeof(DateTime));
        table.Columns.Add("fldPostedDate", typeof(DateTime));
        table.Columns.Add("fldPackingInfo", typeof(string));
        table.Columns.Add("fldSchemeName", typeof(string));
        table.Columns.Add("fldSchemeIdentifier", typeof(string));
        table.Columns.Add("fldCustomsDocumentDescription", typeof(string));
        table.Columns.Add("fldCustomsDocumentVolume", typeof(string));
        table.Columns.Add("fldNumberOfSamples", typeof(int));
        table.Columns.Add("fldNumberOfSetsRequired", typeof(int));
        table.Columns.Add("fldPSLevelPackingInfo", typeof(string));
        table.Columns.Add("fldImportExportLicenceRequired", typeof(bool));
        table.Columns.Add("fldImportPermitReceived", typeof(bool));
        table.Columns.Add("fldCustomsCertificateRequired", typeof(bool));
        table.Columns.Add("fldCustomerPackingInfo", typeof(string));
        table.Columns.Add("fldDistributionDate", typeof(DateTime));
        table.Rows.Add(
            participantId, distributionParticipantId, schemeId, true, "17152", "A", "BA", packaged, posted,
            "Scheme pack", "Mastitis bovine", "PT0060", "Blood samples", "5ml", 4, 2, "PS pack", true, false, true,
            "Customer pack", new DateTime(2026, 5, 13));

        var item = Parse<PackagingSchemeItemEntity>(table).Single();

        Assert.Equal(participantId, item.ParticipantId);
        Assert.Equal(distributionParticipantId, item.DistributionParticipantId);
        Assert.Equal(schemeId, item.MonthlyDistributionSchemeId);
        Assert.True(item.PreparationComplete);
        Assert.Equal("17152", item.DistributionReference);
        Assert.Equal("A", item.DistributionReferenceSuffix);
        Assert.Equal("BA", item.ScheduleCode);
        Assert.Equal(packaged, item.PackagedDate);
        Assert.Equal(posted, item.PostedDate);
        Assert.Equal("Scheme pack", item.PackingInfo);
        Assert.Equal("Mastitis bovine", item.SchemeName);
        Assert.Equal("PT0060", item.SchemeIdentifier);
        Assert.Equal("Blood samples", item.CustomsDocumentDescription);
        Assert.Equal("5ml", item.CustomsDocumentVolume);
        Assert.Equal(4, item.NumberOfSamples);
        Assert.Equal(2, item.NumberOfSetsRequired);
        Assert.Equal("PS pack", item.PSLevelPackingInfo);
        Assert.True(item.ImportExportLicenceRequired);
        Assert.False(item.ImportPermitReceived);
        Assert.True(item.CustomsCertificateRequired);
        Assert.Equal("Customer pack", item.CustomerPackingInfo);
        Assert.Equal(new DateTime(2026, 5, 13), item.DistributionDate);
    }

    // fldName/fldIdentifier belong to the Group Address/Daily item shape, not this one.
    [Fact]
    public void Register_MapsPackagingSchemeItem_IgnoresFldNameAndFldIdentifier()
    {
        var table = new DataTable();
        table.Columns.Add("fldName", typeof(string));
        table.Columns.Add("fldIdentifier", typeof(string));
        table.Rows.Add("Ignored name", "Ignored id");

        var item = Parse<PackagingSchemeItemEntity>(table).Single();

        Assert.Equal(string.Empty, item.SchemeName);
        Assert.Equal(string.Empty, item.SchemeIdentifier);
    }

    [Fact]
    public void Register_MapsPackagingSchemeItem_NullDatesAndTextKeepDefaults()
    {
        var table = new DataTable();
        table.Columns.Add("fldPackagedDate", typeof(DateTime));
        table.Columns.Add("fldPostedDate", typeof(DateTime));
        table.Columns.Add("fldPackingInfo", typeof(string));
        table.Columns.Add("fldPSLevelPackingInfo", typeof(string));
        table.Columns.Add("fldPreparationComplete", typeof(bool));
        table.Rows.Add(DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value);

        var item = Parse<PackagingSchemeItemEntity>(table).Single();

        Assert.Null(item.PackagedDate);
        Assert.Null(item.PostedDate);
        Assert.Equal(string.Empty, item.PackingInfo);
        Assert.Equal(string.Empty, item.PSLevelPackingInfo);
        Assert.False(item.PreparationComplete);
    }

    [Fact]
    public void Register_MapsDistributionPostedEmail_ColumnsToProperties()
    {
        var emailId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var schemeId = Guid.NewGuid();
        var table = new DataTable();
        table.Columns.Add("fldDistributionPostedEmailId", typeof(Guid));
        table.Columns.Add("fldParticipantId", typeof(Guid));
        table.Columns.Add("fldMonthlyDistributionSchemeId", typeof(Guid));
        table.Columns.Add("fldEmail", typeof(string));
        table.Columns.Add("fldDistribution", typeof(string));
        table.Columns.Add("fldSchemeName", typeof(string));
        table.Rows.Add(emailId, participantId, schemeId, "lab@example.org", "17152A/BA", "PT0060: Mastitis bovine");

        var email = Parse<DistributionPostedEmailEntity>(table).Single();

        Assert.Equal(emailId, email.DistributionPostedEmailId);
        Assert.Equal(participantId, email.ParticipantId);
        Assert.Equal(schemeId, email.MonthlyDistributionSchemeId);
        Assert.Equal("lab@example.org", email.Email);
        Assert.Equal("17152A/BA", email.Distribution);
        Assert.Equal("PT0060: Mastitis bovine", email.SchemeName);
    }

    [Fact]
    public void Register_MapsDistributionPostedEmail_NullTextKeepsDefaults()
    {
        var table = new DataTable();
        table.Columns.Add("fldEmail", typeof(string));
        table.Columns.Add("fldDistribution", typeof(string));
        table.Rows.Add(DBNull.Value, DBNull.Value);

        var email = Parse<DistributionPostedEmailEntity>(table).Single();

        Assert.Equal(string.Empty, email.Email);
        Assert.Equal(string.Empty, email.Distribution);
    }

    [Fact]
    public void Register_MapsPackagingLabelAddress_ColumnsToProperties()
    {
        var table = new DataTable();
        table.Columns.Add("fldContactName", typeof(string));
        table.Columns.Add("fldOrganisation", typeof(string));
        table.Columns.Add("fldAddress1", typeof(string));
        table.Columns.Add("fldAddress2", typeof(string));
        table.Columns.Add("fldAddress3", typeof(string));
        table.Columns.Add("fldAddress4", typeof(string));
        table.Columns.Add("fldAddress5", typeof(string));
        table.Columns.Add("fldCountry", typeof(string));
        table.Columns.Add("fldTelephone", typeof(string));
        table.Rows.Add("A Contact", "An Org", "1 High St", "Town", "County", "Four", "Five", "UK", "0123");

        var label = Parse<PackagingLabelAddressEntity>(table).Single();

        Assert.Equal("A Contact", label.ContactName);
        Assert.Equal("An Org", label.Organisation);
        Assert.Equal("1 High St", label.Address1);
        Assert.Equal("Town", label.Address2);
        Assert.Equal("County", label.Address3);
        Assert.Equal("Four", label.Address4);
        Assert.Equal("Five", label.Address5);
        Assert.Equal("UK", label.Country);
        Assert.Equal("0123", label.Telephone);
    }

    [Fact]
    public void Register_MapsPackagingLabelAddress_NullColumnsKeepDefaults()
    {
        var table = new DataTable();
        table.Columns.Add("fldContactName", typeof(string));
        table.Columns.Add("fldAddress2", typeof(string));
        table.Rows.Add(DBNull.Value, DBNull.Value);

        var label = Parse<PackagingLabelAddressEntity>(table).Single();

        Assert.Equal(string.Empty, label.ContactName);
        Assert.Equal(string.Empty, label.Address2);
    }

    private static List<T> Parse<T>(DataTable table)
    {
        using var reader = table.CreateDataReader();
        return SqlMapper.Parse<T>(reader).ToList();
    }
}
