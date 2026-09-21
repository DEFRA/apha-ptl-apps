using System.Reflection;
using Dapper;
using PTL.Core.Contract;
using PTL.Core.Customer;
using PTL.Core.Lookup;
using PTL.Core.Participant;
using PTL.Core.Scheme;
using CoreContract = PTL.Core.Contract.Contract;
using CoreCustomer = PTL.Core.Customer.Customer;
using CoreParticipant = PTL.Core.Participant.Participant;
using CoreScheme = PTL.Core.Scheme.Scheme;

namespace PTL.Data.Infrastructure;

/// <summary>
/// Stored procedures return raw fldXxx (and a few other non-conventional, e.g. "Readonly") column
/// names - Dapper's default mapper only matches a column to a property by exact (case-insensitive)
/// name, so without an explicit map every fldXxx column silently fails to populate its property.
/// This reproduces, column-for-column, the mappings that used to live in EF Core's
/// PtlDbContext.OnModelCreating before the migration to Dapper, so it must be kept in sync with the
/// stored procedures' result columns exactly as that file was. Call <see cref="Register"/> once at
/// startup, before any repository executes a query.
/// </summary>
public static class DapperColumnMappings
{
    private const string ColCustomerId = "fldCustomerId";
    private const string ColName = "fldName";
    private const string ColIsActive = "fldIsActive";
    private const string ColYearId = "fldYearId";

    public static void Register()
    {
        Map<CoreCustomer>(new(StringComparer.OrdinalIgnoreCase)
        {
            [ColCustomerId] = nameof(CoreCustomer.CustomerId),
            ["fldQalNumber"] = nameof(CoreCustomer.QalNumber),
            ["fldRegisteredFileNumber"] = nameof(CoreCustomer.RegisteredFileNumber),
            [ColName] = nameof(CoreCustomer.Name),
            ["fldPreviousName"] = nameof(CoreCustomer.PreviousName),
            ["fldCustomerTypeID"] = nameof(CoreCustomer.CustomerTypeId),
            ["fldVatNumber"] = nameof(CoreCustomer.VatNumber),
            ["fldVatRatingId"] = nameof(CoreCustomer.VatRatingId),
            ["fldAccountNumber"] = nameof(CoreCustomer.AccountNumber),
            ["fldCustomerFinanceId"] = nameof(CoreCustomer.CustomerFinanceId),
            ["fldContactName"] = nameof(CoreCustomer.ContactName),
            ["fldOrganisation"] = nameof(CoreCustomer.Organisation),
            ["fldAddress1"] = nameof(CoreCustomer.Address1),
            ["fldAddress2"] = nameof(CoreCustomer.Address2),
            ["fldAddress3"] = nameof(CoreCustomer.Address3),
            ["fldAddress4"] = nameof(CoreCustomer.Address4),
            ["fldAddress5"] = nameof(CoreCustomer.Address5),
            ["fldCountryId"] = nameof(CoreCustomer.CountryId),
            ["fldTelephone"] = nameof(CoreCustomer.Telephone),
            ["fldTelephone2"] = nameof(CoreCustomer.Telephone2),
            ["fldFax"] = nameof(CoreCustomer.Fax),
            ["fldEmail"] = nameof(CoreCustomer.Email),
            ["fldCurrencyId"] = nameof(CoreCustomer.CurrencyId),
            ["fldComments"] = nameof(CoreCustomer.Comments),
            ["fldInitialStartDate"] = nameof(CoreCustomer.InitialStartDate),
            ["fldPostageArrangements"] = nameof(CoreCustomer.PostageArrangements),
            ["fldPaymentNonUK"] = nameof(CoreCustomer.PaymentNonUK),
            ["fldInvoiceName"] = nameof(CoreCustomer.InvoiceName),
            ["fldInvoiceOrganisation"] = nameof(CoreCustomer.InvoiceOrganisation),
            ["fldInvoiceAddress1"] = nameof(CoreCustomer.InvoiceAddress1),
            ["fldInvoiceAddress2"] = nameof(CoreCustomer.InvoiceAddress2),
            ["fldInvoiceAddress3"] = nameof(CoreCustomer.InvoiceAddress3),
            ["fldInvoiceAddress4"] = nameof(CoreCustomer.InvoiceAddress4),
            ["fldInvoiceAddress5"] = nameof(CoreCustomer.InvoiceAddress5),
            ["fldInvoiceCountryId"] = nameof(CoreCustomer.InvoiceCountryId),
            ["fldInvoiceTelephone"] = nameof(CoreCustomer.InvoiceTelephone),
            ["fldInvoiceTelephone2"] = nameof(CoreCustomer.InvoiceTelephone2),
            ["fldInvoiceFax"] = nameof(CoreCustomer.InvoiceFax),
            ["fldInvoiceEmail"] = nameof(CoreCustomer.InvoiceEmail),
            [ColIsActive] = nameof(CoreCustomer.IsActive),
            ["fldCanOrderOnline"] = nameof(CoreCustomer.CanOrderOnline),
            ["fldInactiveDate"] = nameof(CoreCustomer.InactiveDate),
            ["fldCustomerStatusId"] = nameof(CoreCustomer.CustomerStatusId),
        });

        Map<CustomerSummaryEntity>(new(StringComparer.OrdinalIgnoreCase)
        {
            [ColCustomerId] = nameof(CustomerSummaryEntity.CustomerId),
            ["fldQalNumber"] = nameof(CustomerSummaryEntity.QalNumber),
            [ColName] = nameof(CustomerSummaryEntity.Name),
            ["fldOrganisation"] = nameof(CustomerSummaryEntity.Organisation),
            [ColIsActive] = nameof(CustomerSummaryEntity.IsActive),
        });

        Map<CoreParticipant>(new(StringComparer.OrdinalIgnoreCase)
        {
            ["fldParticipantId"] = nameof(CoreParticipant.ParticipantId),
            ["fldSsoId"] = nameof(CoreParticipant.SsoId),
            [ColCustomerId] = nameof(CoreParticipant.CustomerId),
            ["fldLabCode"] = nameof(CoreParticipant.LabCode),
            ["fldLabName"] = nameof(CoreParticipant.LabName),
            ["fldLabTypeId"] = nameof(CoreParticipant.LabTypeId),
            ["fldContactName"] = nameof(CoreParticipant.ContactName),
            ["fldOrganisation"] = nameof(CoreParticipant.Organisation),
            ["fldAddress1"] = nameof(CoreParticipant.Address1),
            ["fldAddress2"] = nameof(CoreParticipant.Address2),
            ["fldAddress3"] = nameof(CoreParticipant.Address3),
            ["fldAddress4"] = nameof(CoreParticipant.Address4),
            ["fldAddress5"] = nameof(CoreParticipant.Address5),
            ["fldCountryId"] = nameof(CoreParticipant.CountryId),
            ["fldTelephone"] = nameof(CoreParticipant.Telephone),
            ["fldFax"] = nameof(CoreParticipant.Fax),
            ["fldEmail"] = nameof(CoreParticipant.Email),
            ["fldEmail2"] = nameof(CoreParticipant.Email2),
            ["fldComments"] = nameof(CoreParticipant.Comments),
            [ColIsActive] = nameof(CoreParticipant.IsActive),
            ["fldInactiveDate"] = nameof(CoreParticipant.InactiveDate),
            ["fldInactiveError"] = nameof(CoreParticipant.InactiveError),
            ["fldInactiveErrorDate"] = nameof(CoreParticipant.InactiveErrorDate),
        });

        Map<ParticipantSummaryEntity>(new(StringComparer.OrdinalIgnoreCase)
        {
            ["fldParticipantId"] = nameof(ParticipantSummaryEntity.ParticipantId),
            [ColCustomerId] = nameof(ParticipantSummaryEntity.CustomerId),
            ["fldLabCode"] = nameof(ParticipantSummaryEntity.LabCode),
            ["fldLabName"] = nameof(ParticipantSummaryEntity.LabName),
            ["fldContactName"] = nameof(ParticipantSummaryEntity.ContactName),
            [ColIsActive] = nameof(ParticipantSummaryEntity.IsActive),
        });

        Map<CoreContract>(new(StringComparer.OrdinalIgnoreCase)
        {
            ["fldContractId"] = nameof(CoreContract.ContractId),
            [ColCustomerId] = nameof(CoreContract.CustomerId),
            ["fldCustomerName"] = nameof(CoreContract.CustomerName),
            ["fldQALNumber"] = nameof(CoreContract.QalNumber),
            [ColYearId] = nameof(CoreContract.YearId),
            ["fldUTNumber"] = nameof(CoreContract.UTNumber),
            ["fldFTNumber"] = nameof(CoreContract.FTNumber),
            ["fldContractSignatory"] = nameof(CoreContract.ContractSignatory),
            ["fldActionsRequired"] = nameof(CoreContract.ActionsRequired),
            ["fldRenewalInformation"] = nameof(CoreContract.RenewalInformation),
            ["fldDiscountRate"] = nameof(CoreContract.DiscountRate),
            ["fldAdministrationCharge"] = nameof(CoreContract.AdministrationCharge),
            ["fldNumberCourier"] = nameof(CoreContract.NumberCourier),
            ["fldCourierPrice"] = nameof(CoreContract.CourierPrice),
            ["fldNumberPostage"] = nameof(CoreContract.NumberPostage),
            ["fldPostagePrice"] = nameof(CoreContract.PostagePrice),
            ["fldNumberSpecialDelivery"] = nameof(CoreContract.NumberSpecialDelivery),
            ["fldSpecialDeliveryPrice"] = nameof(CoreContract.SpecialDeliveryPrice),
            ["fldAcknowledgementPostedDate"] = nameof(CoreContract.AcknowledgementPostedDate),
            ["fldAcknowledgementReturnedDate"] = nameof(CoreContract.AcknowledgementReturnedDate),
            ["fldJobSheetPostedDate"] = nameof(CoreContract.JobSheetPostedDate),
            ["fldReasonForClosure"] = nameof(CoreContract.ReasonForClosure),
            ["fldDateOfLeaving"] = nameof(CoreContract.DateOfLeaving),
            [ColIsActive] = nameof(CoreContract.IsActive),
            ["Readonly"] = nameof(CoreContract.IsReadOnly),
            ["fldSuffix"] = nameof(CoreContract.Suffix),
            ["fldCommencementDate"] = nameof(CoreContract.CommencementDate),
            ["fldPurchaseOrderNumber"] = nameof(CoreContract.PurchaseOrderNumber),
            ["fldOptOutOfInvoiceGeneration"] = nameof(CoreContract.OptOutOfInvoiceGeneration),
            ["fldIsInvoiceSent"] = nameof(CoreContract.IsInvoiceSent),
            ["fldIsOnlineOrder"] = nameof(CoreContract.IsOnlineOrder),
            ["fldApprovedBy"] = nameof(CoreContract.ApprovedBy),
            ["fldApprovedDate"] = nameof(CoreContract.ApprovedDate),
        });

        Map<ContractSummaryEntity>(new(StringComparer.OrdinalIgnoreCase)
        {
            ["fldContractId"] = nameof(ContractSummaryEntity.ContractId),
            [ColCustomerId] = nameof(ContractSummaryEntity.CustomerId),
            [ColYearId] = nameof(ContractSummaryEntity.YearId),
            [ColIsActive] = nameof(ContractSummaryEntity.IsActive),
            ["fldSuffix"] = nameof(ContractSummaryEntity.Suffix),
        });

        Map<CoreScheme>(new(StringComparer.OrdinalIgnoreCase)
        {
            ["fldSchemeId"] = nameof(CoreScheme.SchemeId),
            ["fldSharedId"] = nameof(CoreScheme.SharedId),
            [ColYearId] = nameof(CoreScheme.YearId),
            ["fldIdentifier"] = nameof(CoreScheme.Identifier),
            [ColName] = nameof(CoreScheme.Name),
            ["fldScheduleId"] = nameof(CoreScheme.ScheduleId),
            ["fldScheduleCodeId"] = nameof(CoreScheme.ScheduleCodeId),
            ["fldStartDate"] = nameof(CoreScheme.StartDate),
            ["fldDistributionMonthJan"] = nameof(CoreScheme.DistributionMonthJan),
            ["fldDistributionMonthFeb"] = nameof(CoreScheme.DistributionMonthFeb),
            ["fldDistributionMonthMar"] = nameof(CoreScheme.DistributionMonthMar),
            ["fldDistributionMonthApr"] = nameof(CoreScheme.DistributionMonthApr),
            ["fldDistributionMonthMay"] = nameof(CoreScheme.DistributionMonthMay),
            ["fldDistributionMonthJun"] = nameof(CoreScheme.DistributionMonthJun),
            ["fldDistributionMonthJul"] = nameof(CoreScheme.DistributionMonthJul),
            ["fldDistributionMonthAug"] = nameof(CoreScheme.DistributionMonthAug),
            ["fldDistributionMonthSep"] = nameof(CoreScheme.DistributionMonthSep),
            ["fldDistributionMonthOct"] = nameof(CoreScheme.DistributionMonthOct),
            ["fldDistributionMonthNov"] = nameof(CoreScheme.DistributionMonthNov),
            ["fldDistributionMonthDec"] = nameof(CoreScheme.DistributionMonthDec),
            ["fldDistributionAsAvailable"] = nameof(CoreScheme.DistributionAsAvailable),
            ["fldWeekNumber"] = nameof(CoreScheme.WeekNumber),
            ["fldDayOfWeekId"] = nameof(CoreScheme.DayOfWeekId),
            ["fldDeadline"] = nameof(CoreScheme.Deadline),
            ["fldPilot"] = nameof(CoreScheme.Pilot),
            ["fldAccredited"] = nameof(CoreScheme.Accredited),
            ["fldComerciallyAvailable"] = nameof(CoreScheme.ComerciallyAvailable),
            ["fldLimitedSampleAvailability"] = nameof(CoreScheme.LimitedSampleAvailability),
            ["fldNoVLALabs"] = nameof(CoreScheme.NoVLALabs),
            ["fldCombinedPackaging"] = nameof(CoreScheme.CombinedPackaging),
            ["fldSampleOrigin"] = nameof(CoreScheme.SampleOrigin),
            ["fldSubcontractor"] = nameof(CoreScheme.Subcontractor),
            ["fldNumberOfSamples"] = nameof(CoreScheme.NumberOfSamples),
            ["fldSamplePackingInstructions"] = nameof(CoreScheme.SamplePackingInstructions),
            ["fldTestConsultant1"] = nameof(CoreScheme.TestConsultant1),
            ["fldTestConsultant2"] = nameof(CoreScheme.TestConsultant2),
            ["fldTestConsultant3"] = nameof(CoreScheme.TestConsultant3),
            ["fldCommentsRequired"] = nameof(CoreScheme.CommentsRequired),
            ["fldDateOfReceipt"] = nameof(CoreScheme.DateOfReceipt),
            ["fldStorageConditions"] = nameof(CoreScheme.StorageConditions),
            ["fldConditionOnReceipt"] = nameof(CoreScheme.ConditionOnReceipt),
            ["fldInstructions"] = nameof(CoreScheme.Instructions),
            ["fldSampleNoSequence"] = nameof(CoreScheme.SampleNoSequence),
            ["fldTestConsultantTabulationId"] = nameof(CoreScheme.TestConsultantTabulationId),
            ["fldUseExternalReference"] = nameof(CoreScheme.UseExternalReference),
            ["fldLastModified"] = nameof(CoreScheme.LastModified),
            ["fldStoreRatings"] = nameof(CoreScheme.StoreRatings),
            ["fldAssessor1"] = nameof(CoreScheme.Assessor1),
            ["fldAssessor2"] = nameof(CoreScheme.Assessor2),
            ["fldAssessor3"] = nameof(CoreScheme.Assessor3),
            ["fldAssessor4"] = nameof(CoreScheme.Assessor4),
            ["fldRequiresAssessment"] = nameof(CoreScheme.RequiresAssessment),
            ["fldStandardTabulationText"] = nameof(CoreScheme.StandardTabulationText),
            ["fldPostage"] = nameof(CoreScheme.Postage),
            ["fldCustomsDocumentDescription"] = nameof(CoreScheme.CustomsDescription),
            ["fldCustomsDocumentVolume"] = nameof(CoreScheme.CustomsVolume),
            ["fldDataConsentDeclarationActive"] = nameof(CoreScheme.DataConsentDeclarationActive),
            ["fldDataConsentDeclarationText"] = nameof(CoreScheme.DataConsentDeclarationText),
            ["Readonly"] = nameof(CoreScheme.IsReadOnly),
        });

        Map<SchemeSummaryEntity>(new(StringComparer.OrdinalIgnoreCase)
        {
            ["fldSharedId"] = nameof(SchemeSummaryEntity.SharedId),
            [ColYearId] = nameof(SchemeSummaryEntity.YearId),
            ["fldCurrentSchemeId"] = nameof(SchemeSummaryEntity.CurrentSchemeId),
            ["fldCurrentIdentifier"] = nameof(SchemeSummaryEntity.CurrentIdentifier),
            ["fldCurrentName"] = nameof(SchemeSummaryEntity.CurrentName),
            ["fldNextSchemeId"] = nameof(SchemeSummaryEntity.NextSchemeId),
            ["fldNextIdentifier"] = nameof(SchemeSummaryEntity.NextIdentifier),
            ["fldNextName"] = nameof(SchemeSummaryEntity.NextName),
            ["fldRecentSchemeId"] = nameof(SchemeSummaryEntity.RecentSchemeId),
            ["fldRecentIdentifier"] = nameof(SchemeSummaryEntity.RecentIdentifier),
            ["fldRecentName"] = nameof(SchemeSummaryEntity.RecentName),
        });

        Map<SchemeHistoryEntity>(new(StringComparer.OrdinalIgnoreCase)
        {
            ["fldCurrentSchemeId"] = nameof(SchemeHistoryEntity.SchemeId),
            ["fldSharedId"] = nameof(SchemeHistoryEntity.SharedId),
            [ColYearId] = nameof(SchemeHistoryEntity.YearId),
            ["fldCurrentIdentifier"] = nameof(SchemeHistoryEntity.Identifier),
            ["fldCurrentName"] = nameof(SchemeHistoryEntity.Name),
        });

        Map<SchemeCurrencyEntity>(new(StringComparer.OrdinalIgnoreCase)
        {
            ["fldSchemeCurrencyId"] = nameof(SchemeCurrencyEntity.SchemeCurrencyId),
            ["fldSchemeId"] = nameof(SchemeCurrencyEntity.SchemeId),
            ["fldCurrencyId"] = nameof(SchemeCurrencyEntity.CurrencyId),
            ["fldPrice"] = nameof(SchemeCurrencyEntity.Price),
            ["fldCurrencyName"] = nameof(SchemeCurrencyEntity.CurrencyName),
            ["fldCurrencySymbol"] = nameof(SchemeCurrencyEntity.CurrencySymbol),
        });

        Map<PostagePricingPlanEntity>(new(StringComparer.OrdinalIgnoreCase)
        {
            ["fldPostageId"] = nameof(PostagePricingPlanEntity.PostageId),
            [ColName] = nameof(PostagePricingPlanEntity.Name),
            ["fldUkPrice"] = nameof(PostagePricingPlanEntity.UKPrice),
            ["fldEuPrice"] = nameof(PostagePricingPlanEntity.EUPrice),
            ["fldNonEuPrice"] = nameof(PostagePricingPlanEntity.NonEUPrice),
            [ColYearId] = nameof(PostagePricingPlanEntity.YearId),
        });

        Map<CountryEntity>(new(StringComparer.OrdinalIgnoreCase)
        {
            ["fldCountryId"] = nameof(CountryEntity.CountryId),
            ["fldCountry"] = nameof(CountryEntity.Country),
        });

        Map<CurrencyEntity>(new(StringComparer.OrdinalIgnoreCase)
        {
            ["fldCurrencyId"] = nameof(CurrencyEntity.CurrencyId),
            [ColName] = nameof(CurrencyEntity.Name),
            ["fldSymbol"] = nameof(CurrencyEntity.Symbol),
        });

        Map<CustomerTypeEntity>(new(StringComparer.OrdinalIgnoreCase)
        {
            ["fldCustomerTypeId"] = nameof(CustomerTypeEntity.CustomerTypeId),
            ["fldCustomerType"] = nameof(CustomerTypeEntity.CustomerType),
        });

        Map<VatRatingEntity>(new(StringComparer.OrdinalIgnoreCase)
        {
            ["fldVatRatingId"] = nameof(VatRatingEntity.VatRatingId),
            ["fldVatRating"] = nameof(VatRatingEntity.VatRating),
        });

        Map<LabTypeEntity>(new(StringComparer.OrdinalIgnoreCase)
        {
            ["fldLabTypeId"] = nameof(LabTypeEntity.LabTypeId),
            [ColName] = nameof(LabTypeEntity.Name),
        });

        Map<YearEntity>(new(StringComparer.OrdinalIgnoreCase)
        {
            [ColYearId] = nameof(YearEntity.YearId),
            ["fldYear"] = nameof(YearEntity.Year),
        });
    }

    private static void Map<T>(Dictionary<string, string> columnNameToPropertyName)
    {
        // CustomPropertyTypeMap's Func<Type,string,PropertyInfo> is declared non-nullable, but
        // Dapper explicitly supports returning null to mean "skip this column" (see ResolveProperty).
#pragma warning disable CS8603
        SqlMapper.SetTypeMap(typeof(T), new CustomPropertyTypeMap(typeof(T), (type, columnName) =>
            ResolveProperty(type, columnNameToPropertyName, columnName)));
#pragma warning restore CS8603
    }

    // Dapper skips a result column entirely when this returns null (e.g. spgaCountry's unused
    // fldCountryTypeId/fldCountryType/fldAllocationCount columns).
    private static PropertyInfo? ResolveProperty(Type type, Dictionary<string, string> columnNameToPropertyName, string columnName) =>
        columnNameToPropertyName.TryGetValue(columnName, out var propertyName)
            ? type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)
            : null;
}
