using System.Reflection;
using Dapper;
using PTL.Core.AdministrationCharge;
using PTL.Core.Contract;
using PTL.Core.Contract.ImportPermit;
using PTL.Core.Contract.Renew;
using PTL.Core.Contract.Renewal;
using PTL.Core.Contract.SampleAddress;
using PTL.Core.Customer;
using PTL.Core.GroupAddress;
using PTL.Core.Lookup;
using PTL.Core.Participant;
using PTL.Core.Scheme;
using PTL.Core.WeightedPricingPlan;
using CoreContract = PTL.Core.Contract.Contract;
using CoreCustomer = PTL.Core.Customer.Customer;
using CoreGroupAddress = PTL.Core.GroupAddress.GroupAddress;
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
    private const string ColAddress1 = "fldAddress1";
    private const string ColAddress2 = "fldAddress2";
    private const string ColAddress3 = "fldAddress3";
    private const string ColAddress4 = "fldAddress4";
    private const string ColAddress5 = "fldAddress5";
    private const string ColCountryId = "fldCountryId";
    private const string ColTelephone = "fldTelephone";
    private const string ColContactName = "fldContactName";
    private const string ColOrganisation = "fldOrganisation";
    private const string ColParticipantId = "fldParticipantId";
    private const string ColLabCode = "fldLabCode";
    private const string ColIdentifier = "fldIdentifier";
    private const string ColContractId = "fldContractId";
    private const string ColCurrencyId = "fldCurrencyId";

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
            [ColContactName] = nameof(CoreCustomer.ContactName),
            [ColOrganisation] = nameof(CoreCustomer.Organisation),
            [ColAddress1] = nameof(CoreCustomer.Address1),
            [ColAddress2] = nameof(CoreCustomer.Address2),
            [ColAddress3] = nameof(CoreCustomer.Address3),
            [ColAddress4] = nameof(CoreCustomer.Address4),
            [ColAddress5] = nameof(CoreCustomer.Address5),
            [ColCountryId] = nameof(CoreCustomer.CountryId),
            [ColTelephone] = nameof(CoreCustomer.Telephone),
            ["fldTelephone2"] = nameof(CoreCustomer.Telephone2),
            ["fldFax"] = nameof(CoreCustomer.Fax),
            ["fldEmail"] = nameof(CoreCustomer.Email),
            [ColCurrencyId] = nameof(CoreCustomer.CurrencyId),
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
            [ColOrganisation] = nameof(CustomerSummaryEntity.Organisation),
            [ColIsActive] = nameof(CustomerSummaryEntity.IsActive),
        });

        Map<PendingCustomerUpdateSummaryEntity>(new(StringComparer.OrdinalIgnoreCase)
        {
            [ColCustomerId] = nameof(PendingCustomerUpdateSummaryEntity.CustomerId),
            ["fldPendingCustomerDetailsEditId"] = nameof(PendingCustomerUpdateSummaryEntity.PendingCustomerUpdateId),
            ["fldQalNumber"] = nameof(PendingCustomerUpdateSummaryEntity.QalNumber),
            [ColName] = nameof(PendingCustomerUpdateSummaryEntity.Name),
        });

        Map<PendingCustomerUpdate>(new(StringComparer.OrdinalIgnoreCase)
        {
            ["fldPendingCustomerDetailsEditId"] = nameof(PendingCustomerUpdate.PendingCustomerUpdateId),
            [ColCustomerId] = nameof(PendingCustomerUpdate.CustomerId),
            [ColContactName] = nameof(PendingCustomerUpdate.ContactName),
            [ColOrganisation] = nameof(PendingCustomerUpdate.Organisation),
            [ColAddress1] = nameof(PendingCustomerUpdate.Address1),
            [ColAddress2] = nameof(PendingCustomerUpdate.Address2),
            [ColAddress3] = nameof(PendingCustomerUpdate.Address3),
            [ColAddress4] = nameof(PendingCustomerUpdate.Address4),
            [ColAddress5] = nameof(PendingCustomerUpdate.Address5),
            [ColCountryId] = nameof(PendingCustomerUpdate.CountryId),
            [ColTelephone] = nameof(PendingCustomerUpdate.Telephone),
            ["fldTelephone2"] = nameof(PendingCustomerUpdate.Telephone2),
            ["fldFax"] = nameof(PendingCustomerUpdate.Fax),
            ["fldEmail"] = nameof(PendingCustomerUpdate.Email),
            ["fldInvoiceName"] = nameof(PendingCustomerUpdate.InvoiceName),
            ["fldInvoiceOrganisation"] = nameof(PendingCustomerUpdate.InvoiceOrganisation),
            ["fldInvoiceAddress1"] = nameof(PendingCustomerUpdate.InvoiceAddress1),
            ["fldInvoiceAddress2"] = nameof(PendingCustomerUpdate.InvoiceAddress2),
            ["fldInvoiceAddress3"] = nameof(PendingCustomerUpdate.InvoiceAddress3),
            ["fldInvoiceAddress4"] = nameof(PendingCustomerUpdate.InvoiceAddress4),
            ["fldInvoiceAddress5"] = nameof(PendingCustomerUpdate.InvoiceAddress5),
            ["fldInvoiceCountryId"] = nameof(PendingCustomerUpdate.InvoiceCountryId),
            ["fldInvoiceTelephone"] = nameof(PendingCustomerUpdate.InvoiceTelephone),
            ["fldInvoiceTelephone2"] = nameof(PendingCustomerUpdate.InvoiceTelephone2),
            ["fldInvoiceFax"] = nameof(PendingCustomerUpdate.InvoiceFax),
            ["fldInvoiceEmail"] = nameof(PendingCustomerUpdate.InvoiceEmail),
            ["fldIsSubmitted"] = nameof(PendingCustomerUpdate.IsSubmitted),
            ["fldIsDeleted"] = nameof(PendingCustomerUpdate.IsDeleted),
        });

        Map<CoreParticipant>(new(StringComparer.OrdinalIgnoreCase)
        {
            [ColParticipantId] = nameof(CoreParticipant.ParticipantId),
            ["fldSsoId"] = nameof(CoreParticipant.SsoId),
            [ColCustomerId] = nameof(CoreParticipant.CustomerId),
            [ColLabCode] = nameof(CoreParticipant.LabCode),
            ["fldLabName"] = nameof(CoreParticipant.LabName),
            ["fldLabTypeId"] = nameof(CoreParticipant.LabTypeId),
            [ColContactName] = nameof(CoreParticipant.ContactName),
            [ColOrganisation] = nameof(CoreParticipant.Organisation),
            [ColAddress1] = nameof(CoreParticipant.Address1),
            [ColAddress2] = nameof(CoreParticipant.Address2),
            [ColAddress3] = nameof(CoreParticipant.Address3),
            [ColAddress4] = nameof(CoreParticipant.Address4),
            [ColAddress5] = nameof(CoreParticipant.Address5),
            [ColCountryId] = nameof(CoreParticipant.CountryId),
            [ColTelephone] = nameof(CoreParticipant.Telephone),
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
            [ColParticipantId] = nameof(ParticipantSummaryEntity.ParticipantId),
            [ColCustomerId] = nameof(ParticipantSummaryEntity.CustomerId),
            [ColLabCode] = nameof(ParticipantSummaryEntity.LabCode),
            ["fldLabName"] = nameof(ParticipantSummaryEntity.LabName),
            [ColContactName] = nameof(ParticipantSummaryEntity.ContactName),
            [ColIsActive] = nameof(ParticipantSummaryEntity.IsActive),
        });

        Map<PendingParticipantUpdateSummaryEntity>(new(StringComparer.OrdinalIgnoreCase)
        {
            [ColParticipantId] = nameof(PendingParticipantUpdateSummaryEntity.ParticipantId),
            ["fldPendingParticipantDetailsEditId"] = nameof(PendingParticipantUpdateSummaryEntity.PendingParticipantUpdateId),
            [ColLabCode] = nameof(PendingParticipantUpdateSummaryEntity.LabCode),
            ["fldLabName"] = nameof(PendingParticipantUpdateSummaryEntity.LabName),
        });

        Map<PendingParticipantUpdate>(new(StringComparer.OrdinalIgnoreCase)
        {
            ["fldPendingParticipantDetailsEditId"] = nameof(PendingParticipantUpdate.PendingParticipantUpdateId),
            [ColParticipantId] = nameof(PendingParticipantUpdate.ParticipantId),
            [ColCustomerId] = nameof(PendingParticipantUpdate.CustomerId),
            [ColLabCode] = nameof(PendingParticipantUpdate.LabCode),
            [ColContactName] = nameof(PendingParticipantUpdate.ContactName),
            [ColOrganisation] = nameof(PendingParticipantUpdate.Organisation),
            [ColAddress1] = nameof(PendingParticipantUpdate.Address1),
            [ColAddress2] = nameof(PendingParticipantUpdate.Address2),
            [ColAddress3] = nameof(PendingParticipantUpdate.Address3),
            [ColAddress4] = nameof(PendingParticipantUpdate.Address4),
            [ColAddress5] = nameof(PendingParticipantUpdate.Address5),
            [ColCountryId] = nameof(PendingParticipantUpdate.CountryId),
            [ColTelephone] = nameof(PendingParticipantUpdate.Telephone),
            ["fldFax"] = nameof(PendingParticipantUpdate.Fax),
            ["fldEmail"] = nameof(PendingParticipantUpdate.Email),
            ["fldEmail2"] = nameof(PendingParticipantUpdate.Email2),
            ["fldIsSubmitted"] = nameof(PendingParticipantUpdate.IsSubmitted),
            ["fldIsDeleted"] = nameof(PendingParticipantUpdate.IsDeleted),
        });

        Map<CoreContract>(new(StringComparer.OrdinalIgnoreCase)
        {
            [ColContractId] = nameof(CoreContract.ContractId),
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

        Map<SystemSettingsEntity>(new(StringComparer.OrdinalIgnoreCase)
        {
            ["fldUTNumber"] = nameof(SystemSettingsEntity.UTNumber),
            ["NextYearWithDelayId"] = nameof(SystemSettingsEntity.NextYearWithDelayId),
        });

        // spgContractMerge result set 1 (legacy ContractMergeInfo).
        Map<RenewableContractEntity>(new(StringComparer.OrdinalIgnoreCase)
        {
            [ColContractId] = nameof(RenewableContractEntity.ContractId),
            ["fldSuffix"] = nameof(RenewableContractEntity.Suffix),
            ["fldContractSignatory"] = nameof(RenewableContractEntity.ContractSignatory),
            ["fldRenewalInformation"] = nameof(RenewableContractEntity.RenewalInformation),
            ["fldActionsRequired"] = nameof(RenewableContractEntity.ActionsRequired),
            [ColIsActive] = nameof(RenewableContractEntity.IsActive),
            ["fldNoOfItems"] = nameof(RenewableContractEntity.NoOfItems),
        });

        // spgContractMerge result set 2 (legacy ParticipantSchemeMergeInfo). Suffix is not a result
        // column - it is copied from the parent contract by ContractMergeRepository.
        Map<RenewableContractItemEntity>(new(StringComparer.OrdinalIgnoreCase)
        {
            [ColContractId] = nameof(RenewableContractItemEntity.ContractId),
            ["fldParticipantSchemeId"] = nameof(RenewableContractItemEntity.ParticipantSchemeId),
            [ColLabCode] = nameof(RenewableContractItemEntity.LabCode),
            ["fldLabName"] = nameof(RenewableContractItemEntity.LabName),
            ["fldOldSchemeIdentifier"] = nameof(RenewableContractItemEntity.OldSchemeIdentifier),
            ["fldOldSchemeName"] = nameof(RenewableContractItemEntity.OldSchemeName),
            ["fldNewSchemeIdentifier"] = nameof(RenewableContractItemEntity.NewSchemeIdentifier),
            ["fldNewSchemeName"] = nameof(RenewableContractItemEntity.NewSchemeName),
        });

        Map<ContractSummaryEntity>(new(StringComparer.OrdinalIgnoreCase)
        {
            [ColContractId] = nameof(ContractSummaryEntity.ContractId),
            [ColCustomerId] = nameof(ContractSummaryEntity.CustomerId),
            [ColYearId] = nameof(ContractSummaryEntity.YearId),
            [ColIsActive] = nameof(ContractSummaryEntity.IsActive),
            ["fldSuffix"] = nameof(ContractSummaryEntity.Suffix),
        });

        Map<ImportPermitEntity>(new(StringComparer.OrdinalIgnoreCase)
        {
            ["ParticipantSchemeId"] = nameof(ImportPermitEntity.ParticipantSchemeId),
            ["SchemeNumber"] = nameof(ImportPermitEntity.SchemeNumber),
            ["SchemeName"] = nameof(ImportPermitEntity.SchemeName),
            ["LabID"] = nameof(ImportPermitEntity.LabId),
            ["fldImportExportLicenceRequired"] = nameof(ImportPermitEntity.ImportPermitRequired),
            ["fldImportPermitReceived"] = nameof(ImportPermitEntity.ImportPermitReceived),
            ["fldImportPermitExpiry"] = nameof(ImportPermitEntity.ImportPermitExpiry),
        });

        Map<CoreScheme>(new(StringComparer.OrdinalIgnoreCase)
        {
            ["fldSchemeId"] = nameof(CoreScheme.SchemeId),
            ["fldSharedId"] = nameof(CoreScheme.SharedId),
            [ColYearId] = nameof(CoreScheme.YearId),
            [ColIdentifier] = nameof(CoreScheme.Identifier),
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
            [ColCurrencyId] = nameof(SchemeCurrencyEntity.CurrencyId),
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
            [ColCountryId] = nameof(CountryEntity.CountryId),
            ["fldCountry"] = nameof(CountryEntity.Country),
        });

        Map<CurrencyEntity>(new(StringComparer.OrdinalIgnoreCase)
        {
            [ColCurrencyId] = nameof(CurrencyEntity.CurrencyId),
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

        Map<GroupAddressEntity>(new(StringComparer.OrdinalIgnoreCase)
        {
            ["fldGroupAddressId"] = nameof(GroupAddressEntity.GroupAddressId),
            [ColIdentifier] = nameof(GroupAddressEntity.Identifier),
            [ColAddress1] = nameof(GroupAddressEntity.Address1),
            [ColCountryId] = nameof(GroupAddressEntity.CountryId),
        });

        Map<CoreGroupAddress>(new(StringComparer.OrdinalIgnoreCase)
        {
            ["fldGroupAddressId"] = nameof(CoreGroupAddress.GroupAddressId),
            [ColIdentifier] = nameof(CoreGroupAddress.Identifier),
            [ColAddress1] = nameof(CoreGroupAddress.Address1),
            [ColAddress2] = nameof(CoreGroupAddress.Address2),
            [ColAddress3] = nameof(CoreGroupAddress.Address3),
            [ColAddress4] = nameof(CoreGroupAddress.Address4),
            [ColAddress5] = nameof(CoreGroupAddress.Address5),
            [ColCountryId] = nameof(CoreGroupAddress.CountryId),
            [ColTelephone] = nameof(CoreGroupAddress.Telephone),
            ["fldPackingInstructions"] = nameof(CoreGroupAddress.PackingInstructions),
        });

        Map<SampleAddressEntity>(new(StringComparer.OrdinalIgnoreCase)
        {
            [ColContractId] = nameof(SampleAddressEntity.ContractId),
            [ColParticipantId] = nameof(SampleAddressEntity.ParticipantId),
            ["fldQalNumber"] = nameof(SampleAddressEntity.QalNumber),
            [ColLabCode] = nameof(SampleAddressEntity.LabCode),
            [ColContactName] = nameof(SampleAddressEntity.ContactName),
            [ColOrganisation] = nameof(SampleAddressEntity.Organisation),
            [ColAddress1] = nameof(SampleAddressEntity.Address1),
            [ColAddress2] = nameof(SampleAddressEntity.Address2),
            [ColAddress3] = nameof(SampleAddressEntity.Address3),
            [ColAddress4] = nameof(SampleAddressEntity.Address4),
            [ColAddress5] = nameof(SampleAddressEntity.Address5),
            ["fldCountry"] = nameof(SampleAddressEntity.Country),
            [ColTelephone] = nameof(SampleAddressEntity.Telephone),
            ["fldFax"] = nameof(SampleAddressEntity.Fax),
            ["fldEmail"] = nameof(SampleAddressEntity.Email),
            ["fldVatNumber"] = nameof(SampleAddressEntity.VatNumber),
            ["fldAccountNumber"] = nameof(SampleAddressEntity.AccountNumber),
            ["fldVatRating"] = nameof(SampleAddressEntity.VatRating),
            ["fldPurchaseOrderNumber"] = nameof(SampleAddressEntity.PurchaseOrderNumber),
        });

        Map<SampleAddressSchemeEntity>(new(StringComparer.OrdinalIgnoreCase)
        {
            [ColContractId] = nameof(SampleAddressSchemeEntity.ContractId),
            [ColParticipantId] = nameof(SampleAddressSchemeEntity.ParticipantId),
            ["fldParticipantSchemeId"] = nameof(SampleAddressSchemeEntity.ParticipantSchemeId),
            [ColName] = nameof(SampleAddressSchemeEntity.SchemeName),
            [ColIdentifier] = nameof(SampleAddressSchemeEntity.SchemeIdentifier),
            ["fldMonthsActive"] = nameof(SampleAddressSchemeEntity.MonthsActive),
            ["fldWeekNumber"] = nameof(SampleAddressSchemeEntity.WeekNumber),
        });

        // ContractStartDate/ContractEndDate/RenewalInformation are computed aliases in
        // spgaExportContractRenewal and carry no fld prefix.
        Map<ContractRenewalEntity>(new(StringComparer.OrdinalIgnoreCase)
        {
            [ColContractId] = nameof(ContractRenewalEntity.ContractId),
            [ColCustomerId] = nameof(ContractRenewalEntity.CustomerId),
            ["fldQALNumber"] = nameof(ContractRenewalEntity.QalNumber),
            [ColOrganisation] = nameof(ContractRenewalEntity.OrganisationName),
            [ColContactName] = nameof(ContractRenewalEntity.ContactName),
            [ColAddress1] = nameof(ContractRenewalEntity.Address1),
            [ColAddress2] = nameof(ContractRenewalEntity.Address2),
            [ColAddress3] = nameof(ContractRenewalEntity.Address3),
            [ColAddress4] = nameof(ContractRenewalEntity.Address4),
            [ColAddress5] = nameof(ContractRenewalEntity.Address5),
            ["fldCountry"] = nameof(ContractRenewalEntity.Country),
            ["ContractStartDate"] = nameof(ContractRenewalEntity.ContractStartDate),
            ["ContractEndDate"] = nameof(ContractRenewalEntity.ContractEndDate),
            ["RenewalInformation"] = nameof(ContractRenewalEntity.RenewalInformation),
        });

        Map<AdministrationChargeEntity>(new(StringComparer.OrdinalIgnoreCase)
        {
            ["fldAdministrationChargeId"] = nameof(AdministrationChargeEntity.AdministrationChargeId),
            ["fldAdministrationCharge"] = nameof(AdministrationChargeEntity.Name),
        });

        Map<AdministrationChargeCurrencyEntity>(new(StringComparer.OrdinalIgnoreCase)
        {
            ["fldAdministrationChargeCurrencyId"] = nameof(AdministrationChargeCurrencyEntity.AdministrationChargeCurrencyId),
            ["fldAdministrationChargeId"] = nameof(AdministrationChargeCurrencyEntity.AdministrationChargeId),
            [ColCurrencyId] = nameof(AdministrationChargeCurrencyEntity.CurrencyId),
            ["fldPrice"] = nameof(AdministrationChargeCurrencyEntity.Price),
        });

        Map<PricingPercentageEntity>(new(StringComparer.OrdinalIgnoreCase)
        {
            ["fldPricingPercentageId"] = nameof(PricingPercentageEntity.PricingPercentageId),
            [ColYearId] = nameof(PricingPercentageEntity.YearId),
            ["fldNumberOfDistributionsOnScheme"] = nameof(PricingPercentageEntity.NumberOfDistributionsOnScheme),
            ["fldNumberOfDistributionsChosen"] = nameof(PricingPercentageEntity.NumberOfDistributionsChosen),
            ["fldWeight"] = nameof(PricingPercentageEntity.Weight),
        });
    }

    private static void Map<T>(Dictionary<string, string> columnNameToPropertyName)
    {
        // CustomPropertyTypeMap's Func<Type,string,PropertyInfo> is declared non-nullable, but
        // Dapper explicitly supports returning null to mean "skip this column" (see ResolveProperty).
#pragma warning disable CS8603
        SqlMapper.SetTypeMap(typeof(T), new CustomPropertyTypeMap(typeof(T), (type, columnName) =>
            ResolveProperty(type, columnNameToPropertyName, columnName)!));
    }

    // Dapper skips a result column entirely when this returns null (e.g. spgaCountry's unused
    // fldCountryTypeId/fldCountryType/fldAllocationCount columns).
    private static PropertyInfo? ResolveProperty(Type type, Dictionary<string, string> columnNameToPropertyName, string columnName) =>
        columnNameToPropertyName.TryGetValue(columnName, out var propertyName)
            ? type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)
            : null;
}
