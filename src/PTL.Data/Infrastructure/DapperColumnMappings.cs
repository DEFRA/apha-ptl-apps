using System.Reflection;
using Dapper;
using PTL.Core.AdministrationCharge;
using PTL.Core.Contract;
using PTL.Core.Contract.Export.Bulk;
using PTL.Core.Contract.Export.Templates;
using PTL.Core.Contract.ImportPermit;
using PTL.Core.Contract.PendingOrder;
using PTL.Core.Contract.Renew;
using PTL.Core.Contract.Renewal;
using PTL.Core.Contract.SampleAddress;
using PTL.Core.Customer;
using PTL.Core.GroupAddress;
using PTL.Core.Invoice;
using PTL.Core.Lookup;
using PTL.Core.Participant;
using PTL.Core.Scheme;
using PTL.Core.Viewer;
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
    private const string ColQalNumber = "fldQalNumber";
    private const string ColFax = "fldFax";
    private const string ColEmail = "fldEmail";
    private const string ColIsSubmitted = "fldIsSubmitted";
    private const string ColIsDeleted = "fldIsDeleted";
    private const string ColLabName = "fldLabName";
    private const string ColSuffix = "fldSuffix";
    private const string ColPurchaseOrderNumber = "fldPurchaseOrderNumber";
    private const string ColSchemeId = "fldSchemeId";
    private const string ColPrice = "fldPrice";
    private const string ColCountry = "fldCountry";
    private const string ColAccountNumber = "fldAccountNumber";
    private const string ColVatNumber = "fldVatNumber";
    private const string ColInvoiceOrganisation = "fldInvoiceOrganisation";
    private const string ColInvoiceAddress1 = "fldInvoiceAddress1";
    private const string ColInvoiceAddress2 = "fldInvoiceAddress2";
    private const string ColInvoiceAddress3 = "fldInvoiceAddress3";
    private const string ColInvoiceAddress4 = "fldInvoiceAddress4";
    private const string ColInvoiceAddress5 = "fldInvoiceAddress5";
    private const string ColAdministrationCharge = "fldAdministrationCharge";
    private const string ColParticipantSchemeId = "fldParticipantSchemeId";
    private const string ColVatRating = "fldVatRating";

    public static void Register()
    {
        Map<CoreCustomer>(new(StringComparer.OrdinalIgnoreCase)
        {
            [ColCustomerId] = nameof(CoreCustomer.CustomerId),
            [ColQalNumber] = nameof(CoreCustomer.QalNumber),
            ["fldRegisteredFileNumber"] = nameof(CoreCustomer.RegisteredFileNumber),
            [ColName] = nameof(CoreCustomer.Name),
            ["fldPreviousName"] = nameof(CoreCustomer.PreviousName),
            ["fldCustomerTypeID"] = nameof(CoreCustomer.CustomerTypeId),
            [ColVatNumber] = nameof(CoreCustomer.VatNumber),
            ["fldVatRatingId"] = nameof(CoreCustomer.VatRatingId),
            [ColAccountNumber] = nameof(CoreCustomer.AccountNumber),
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
            [ColFax] = nameof(CoreCustomer.Fax),
            [ColEmail] = nameof(CoreCustomer.Email),
            [ColCurrencyId] = nameof(CoreCustomer.CurrencyId),
            ["fldComments"] = nameof(CoreCustomer.Comments),
            ["fldInitialStartDate"] = nameof(CoreCustomer.InitialStartDate),
            ["fldPostageArrangements"] = nameof(CoreCustomer.PostageArrangements),
            ["fldPaymentNonUK"] = nameof(CoreCustomer.PaymentNonUK),
            ["fldInvoiceName"] = nameof(CoreCustomer.InvoiceName),
            [ColInvoiceOrganisation] = nameof(CoreCustomer.InvoiceOrganisation),
            [ColInvoiceAddress1] = nameof(CoreCustomer.InvoiceAddress1),
            [ColInvoiceAddress2] = nameof(CoreCustomer.InvoiceAddress2),
            [ColInvoiceAddress3] = nameof(CoreCustomer.InvoiceAddress3),
            [ColInvoiceAddress4] = nameof(CoreCustomer.InvoiceAddress4),
            [ColInvoiceAddress5] = nameof(CoreCustomer.InvoiceAddress5),
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
            [ColQalNumber] = nameof(CustomerSummaryEntity.QalNumber),
            [ColName] = nameof(CustomerSummaryEntity.Name),
            [ColOrganisation] = nameof(CustomerSummaryEntity.Organisation),
            [ColContactName] = nameof(CustomerSummaryEntity.ContactName),
            [ColAccountNumber] = nameof(CustomerSummaryEntity.AccountNumber),
            [ColCountry] = nameof(CustomerSummaryEntity.Country),
            [ColIsActive] = nameof(CustomerSummaryEntity.IsActive),
        });

        Map<PendingCustomerUpdateSummaryEntity>(new(StringComparer.OrdinalIgnoreCase)
        {
            [ColCustomerId] = nameof(PendingCustomerUpdateSummaryEntity.CustomerId),
            ["fldPendingCustomerDetailsEditId"] = nameof(PendingCustomerUpdateSummaryEntity.PendingCustomerUpdateId),
            [ColQalNumber] = nameof(PendingCustomerUpdateSummaryEntity.QalNumber),
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
            [ColFax] = nameof(PendingCustomerUpdate.Fax),
            [ColEmail] = nameof(PendingCustomerUpdate.Email),
            ["fldInvoiceName"] = nameof(PendingCustomerUpdate.InvoiceName),
            [ColInvoiceOrganisation] = nameof(PendingCustomerUpdate.InvoiceOrganisation),
            [ColInvoiceAddress1] = nameof(PendingCustomerUpdate.InvoiceAddress1),
            [ColInvoiceAddress2] = nameof(PendingCustomerUpdate.InvoiceAddress2),
            [ColInvoiceAddress3] = nameof(PendingCustomerUpdate.InvoiceAddress3),
            [ColInvoiceAddress4] = nameof(PendingCustomerUpdate.InvoiceAddress4),
            [ColInvoiceAddress5] = nameof(PendingCustomerUpdate.InvoiceAddress5),
            ["fldInvoiceCountryId"] = nameof(PendingCustomerUpdate.InvoiceCountryId),
            ["fldInvoiceTelephone"] = nameof(PendingCustomerUpdate.InvoiceTelephone),
            ["fldInvoiceTelephone2"] = nameof(PendingCustomerUpdate.InvoiceTelephone2),
            ["fldInvoiceFax"] = nameof(PendingCustomerUpdate.InvoiceFax),
            ["fldInvoiceEmail"] = nameof(PendingCustomerUpdate.InvoiceEmail),
            [ColIsSubmitted] = nameof(PendingCustomerUpdate.IsSubmitted),
            [ColIsDeleted] = nameof(PendingCustomerUpdate.IsDeleted),
        });

        Map<CoreParticipant>(new(StringComparer.OrdinalIgnoreCase)
        {
            [ColParticipantId] = nameof(CoreParticipant.ParticipantId),
            ["fldSsoId"] = nameof(CoreParticipant.SsoId),
            [ColCustomerId] = nameof(CoreParticipant.CustomerId),
            [ColLabCode] = nameof(CoreParticipant.LabCode),
            [ColLabName] = nameof(CoreParticipant.LabName),
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
            [ColFax] = nameof(CoreParticipant.Fax),
            [ColEmail] = nameof(CoreParticipant.Email),
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
            [ColLabName] = nameof(ParticipantSummaryEntity.LabName),
            [ColContactName] = nameof(ParticipantSummaryEntity.ContactName),
            [ColIsActive] = nameof(ParticipantSummaryEntity.IsActive),
        });

        Map<ViewerEntity>(new(StringComparer.OrdinalIgnoreCase)
        {
            ["fldViewerId"] = nameof(ViewerEntity.ViewerId),
            [ColName] = nameof(ViewerEntity.Name),
            [ColEmail] = nameof(ViewerEntity.Email),
            ["fldSsoId"] = nameof(ViewerEntity.SsoId),
        });

        Map<ParticipantViewerEntity>(new(StringComparer.OrdinalIgnoreCase)
        {
            ["fldViewerParticipantId"] = nameof(ParticipantViewerEntity.ViewerParticipantId),
            ["fldViewerId"] = nameof(ParticipantViewerEntity.ViewerId),
            [ColParticipantId] = nameof(ParticipantViewerEntity.ParticipantId),
            [ColName] = nameof(ParticipantViewerEntity.Name),
        });

        Map<PendingParticipantUpdateSummaryEntity>(new(StringComparer.OrdinalIgnoreCase)
        {
            [ColParticipantId] = nameof(PendingParticipantUpdateSummaryEntity.ParticipantId),
            ["fldPendingParticipantDetailsEditId"] = nameof(PendingParticipantUpdateSummaryEntity.PendingParticipantUpdateId),
            [ColLabCode] = nameof(PendingParticipantUpdateSummaryEntity.LabCode),
            [ColLabName] = nameof(PendingParticipantUpdateSummaryEntity.LabName),
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
            [ColFax] = nameof(PendingParticipantUpdate.Fax),
            [ColEmail] = nameof(PendingParticipantUpdate.Email),
            ["fldEmail2"] = nameof(PendingParticipantUpdate.Email2),
            [ColIsSubmitted] = nameof(PendingParticipantUpdate.IsSubmitted),
            [ColIsDeleted] = nameof(PendingParticipantUpdate.IsDeleted),
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
            [ColAdministrationCharge] = nameof(CoreContract.AdministrationCharge),
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
            [ColSuffix] = nameof(CoreContract.Suffix),
            ["fldCommencementDate"] = nameof(CoreContract.CommencementDate),
            [ColPurchaseOrderNumber] = nameof(CoreContract.PurchaseOrderNumber),
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
            ["CurrentYearId"] = nameof(SystemSettingsEntity.CurrentYearId),
            ["NextYearId"] = nameof(SystemSettingsEntity.NextYearId),
        });

        // spgContractMerge result set 1 (legacy ContractMergeInfo).
        Map<RenewableContractEntity>(new(StringComparer.OrdinalIgnoreCase)
        {
            [ColContractId] = nameof(RenewableContractEntity.ContractId),
            [ColSuffix] = nameof(RenewableContractEntity.Suffix),
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
            [ColParticipantSchemeId] = nameof(RenewableContractItemEntity.ParticipantSchemeId),
            [ColLabCode] = nameof(RenewableContractItemEntity.LabCode),
            [ColLabName] = nameof(RenewableContractItemEntity.LabName),
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
            [ColSuffix] = nameof(ContractSummaryEntity.Suffix),
        });

        Map<PendingOrderSummaryEntity>(new(StringComparer.OrdinalIgnoreCase)
        {
            ["fldPendingContractId"] = nameof(PendingOrderSummaryEntity.PendingContractId),
            [ColCustomerId] = nameof(PendingOrderSummaryEntity.CustomerId),
            [ColYearId] = nameof(PendingOrderSummaryEntity.YearId),
            [ColIsSubmitted] = nameof(PendingOrderSummaryEntity.IsSubmitted),
            [ColIsDeleted] = nameof(PendingOrderSummaryEntity.IsDeleted),
            ["fldOrderSubmitDate"] = nameof(PendingOrderSummaryEntity.OrderSubmitDate),
            [ColName] = nameof(PendingOrderSummaryEntity.CustomerName),
            [ColQalNumber] = nameof(PendingOrderSummaryEntity.QalNumber),
        });

        Map<PendingOrderEntity>(new(StringComparer.OrdinalIgnoreCase)
        {
            ["fldPendingContractId"] = nameof(PendingOrderEntity.PendingContractId),
            [ColCustomerId] = nameof(PendingOrderEntity.CustomerId),
            [ColYearId] = nameof(PendingOrderEntity.YearId),
            [ColIsSubmitted] = nameof(PendingOrderEntity.IsSubmitted),
            [ColIsDeleted] = nameof(PendingOrderEntity.IsDeleted),
            [ColPurchaseOrderNumber] = nameof(PendingOrderEntity.PurchaseOrderNumber),
            ["fldOrderSubmitDate"] = nameof(PendingOrderEntity.OrderSubmitDate),
            [ColName] = nameof(PendingOrderEntity.CustomerName),
            ["fldSymbol"] = nameof(PendingOrderEntity.CurrencySymbol),
        });

        Map<PendingOrderSchemeEntity>(new(StringComparer.OrdinalIgnoreCase)
        {
            ["fldPendingParticipantSchemeId"] = nameof(PendingOrderSchemeEntity.PendingParticipantSchemeId),
            ["fldPendingContractId"] = nameof(PendingOrderSchemeEntity.PendingContractId),
            [ColParticipantId] = nameof(PendingOrderSchemeEntity.ParticipantId),
            ["fldParticipantName"] = nameof(PendingOrderSchemeEntity.ParticipantName),
            [ColSchemeId] = nameof(PendingOrderSchemeEntity.SchemeId),
            ["fldSchemeName"] = nameof(PendingOrderSchemeEntity.SchemeName),
            [ColIdentifier] = nameof(PendingOrderSchemeEntity.SchemeIdentifier),
            ["fldJan"] = nameof(PendingOrderSchemeEntity.DistributionMonthJan),
            ["fldFeb"] = nameof(PendingOrderSchemeEntity.DistributionMonthFeb),
            ["fldMar"] = nameof(PendingOrderSchemeEntity.DistributionMonthMar),
            ["fldApr"] = nameof(PendingOrderSchemeEntity.DistributionMonthApr),
            ["fldMay"] = nameof(PendingOrderSchemeEntity.DistributionMonthMay),
            ["fldJun"] = nameof(PendingOrderSchemeEntity.DistributionMonthJun),
            ["fldJul"] = nameof(PendingOrderSchemeEntity.DistributionMonthJul),
            ["fldAug"] = nameof(PendingOrderSchemeEntity.DistributionMonthAug),
            ["fldSep"] = nameof(PendingOrderSchemeEntity.DistributionMonthSep),
            ["fldOct"] = nameof(PendingOrderSchemeEntity.DistributionMonthOct),
            ["fldNov"] = nameof(PendingOrderSchemeEntity.DistributionMonthNov),
            ["fldDec"] = nameof(PendingOrderSchemeEntity.DistributionMonthDec),
            ["fldImportExportLicenceRequired"] = nameof(PendingOrderSchemeEntity.ImportExportLicenceRequired),
            ["fldIsSelected"] = nameof(PendingOrderSchemeEntity.IsSelected),
            ["fldIsRemoved"] = nameof(PendingOrderSchemeEntity.IsRemoved),
            ["fldDataConsentDeclarationGiven"] = nameof(PendingOrderSchemeEntity.DataConsentDeclarationGiven),
            [ColPrice] = nameof(PendingOrderSchemeEntity.Price),
            ["fldCanEditJan"] = nameof(PendingOrderSchemeEntity.CanEditJan),
            ["fldCanEditFeb"] = nameof(PendingOrderSchemeEntity.CanEditFeb),
            ["fldCanEditMar"] = nameof(PendingOrderSchemeEntity.CanEditMar),
            ["fldCanEditApr"] = nameof(PendingOrderSchemeEntity.CanEditApr),
            ["fldCanEditMay"] = nameof(PendingOrderSchemeEntity.CanEditMay),
            ["fldCanEditJun"] = nameof(PendingOrderSchemeEntity.CanEditJun),
            ["fldCanEditJul"] = nameof(PendingOrderSchemeEntity.CanEditJul),
            ["fldCanEditAug"] = nameof(PendingOrderSchemeEntity.CanEditAug),
            ["fldCanEditSep"] = nameof(PendingOrderSchemeEntity.CanEditSep),
            ["fldCanEditOct"] = nameof(PendingOrderSchemeEntity.CanEditOct),
            ["fldCanEditNov"] = nameof(PendingOrderSchemeEntity.CanEditNov),
            ["fldCanEditDec"] = nameof(PendingOrderSchemeEntity.CanEditDec),
            ["fldDistributionMonthJanIsContracted"] = nameof(PendingOrderSchemeEntity.IsContractedJan),
            ["fldDistributionMonthFebIsContracted"] = nameof(PendingOrderSchemeEntity.IsContractedFeb),
            ["fldDistributionMonthMarIsContracted"] = nameof(PendingOrderSchemeEntity.IsContractedMar),
            ["fldDistributionMonthAprIsContracted"] = nameof(PendingOrderSchemeEntity.IsContractedApr),
            ["fldDistributionMonthMayIsContracted"] = nameof(PendingOrderSchemeEntity.IsContractedMay),
            ["fldDistributionMonthJunIsContracted"] = nameof(PendingOrderSchemeEntity.IsContractedJun),
            ["fldDistributionMonthJulIsContracted"] = nameof(PendingOrderSchemeEntity.IsContractedJul),
            ["fldDistributionMonthAugIsContracted"] = nameof(PendingOrderSchemeEntity.IsContractedAug),
            ["fldDistributionMonthSepIsContracted"] = nameof(PendingOrderSchemeEntity.IsContractedSep),
            ["fldDistributionMonthOctIsContracted"] = nameof(PendingOrderSchemeEntity.IsContractedOct),
            ["fldDistributionMonthNovIsContracted"] = nameof(PendingOrderSchemeEntity.IsContractedNov),
            ["fldDistributionMonthDecIsContracted"] = nameof(PendingOrderSchemeEntity.IsContractedDec),
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

        Map<UploadedTemplate>(new(StringComparer.OrdinalIgnoreCase)
        {
            ["fldFileId"] = nameof(UploadedTemplate.FileId),
            ["fldFilename"] = nameof(UploadedTemplate.Filename),
            ["fldUploadedDate"] = nameof(UploadedTemplate.UploadedDate),
            ["fldDocumentType"] = nameof(UploadedTemplate.DocumentType),
            ["fldSelectedTemplate"] = nameof(UploadedTemplate.Selected),
        });

        Map<BulkContractEntity>(new(StringComparer.OrdinalIgnoreCase)
        {
            ["fldContractId"] = nameof(BulkContractEntity.ContractId),
            [ColCustomerId] = nameof(BulkContractEntity.CustomerId),
            ["fldContractNumber"] = nameof(BulkContractEntity.ContractNumber),
            [ColSuffix] = nameof(BulkContractEntity.Suffix),
            [ColYearId] = nameof(BulkContractEntity.YearId),
            ["fldSymbol"] = nameof(BulkContractEntity.Symbol),
            [ColAdministrationCharge] = nameof(BulkContractEntity.AdministrationCharge),
            ["fldDiscountRate"] = nameof(BulkContractEntity.DiscountRate),
            ["fldNumberPostage"] = nameof(BulkContractEntity.NumberPostage),
            ["fldNumberCourier"] = nameof(BulkContractEntity.NumberCourier),
            ["fldNumberSpecialDelivery"] = nameof(BulkContractEntity.NumberSpecialDelivery),
            ["fldPostagePrice"] = nameof(BulkContractEntity.PostagePrice),
            ["fldCourierPrice"] = nameof(BulkContractEntity.CourierPrice),
            ["fldSpecialDeliveryPrice"] = nameof(BulkContractEntity.SpecialDeliveryPrice),
            ["fldCommencementDate"] = nameof(BulkContractEntity.CommencementDate),
            [ColQalNumber] = nameof(BulkContractEntity.QalNumber),
            ["fldContactName"] = nameof(BulkContractEntity.ContactName),
            ["fldOrganisation"] = nameof(BulkContractEntity.Organisation),
            [ColAddress1] = nameof(BulkContractEntity.Address1),
            [ColAddress2] = nameof(BulkContractEntity.Address2),
            ["fldAddress3"] = nameof(BulkContractEntity.Address3),
            ["fldAddress4"] = nameof(BulkContractEntity.Address4),
            ["fldAddress5"] = nameof(BulkContractEntity.Address5),
            [ColCountry] = nameof(BulkContractEntity.Country),
            ["fldTelephone"] = nameof(BulkContractEntity.Telephone),
            [ColFax] = nameof(BulkContractEntity.Fax),
            [ColEmail] = nameof(BulkContractEntity.Email),
            ["fldInvoiceName"] = nameof(BulkContractEntity.InvoiceName),
            [ColInvoiceOrganisation] = nameof(BulkContractEntity.InvoiceOrganisation),
            [ColInvoiceAddress1] = nameof(BulkContractEntity.InvoiceAddress1),
            [ColInvoiceAddress2] = nameof(BulkContractEntity.InvoiceAddress2),
            [ColInvoiceAddress3] = nameof(BulkContractEntity.InvoiceAddress3),
            [ColInvoiceAddress4] = nameof(BulkContractEntity.InvoiceAddress4),
            [ColInvoiceAddress5] = nameof(BulkContractEntity.InvoiceAddress5),
            ["fldInvoiceCountry"] = nameof(BulkContractEntity.InvoiceCountry),
            ["fldInvoiceTelephone"] = nameof(BulkContractEntity.InvoiceTelephone),
            ["fldInvoiceFax"] = nameof(BulkContractEntity.InvoiceFax),
            ["fldInvoiceEmail"] = nameof(BulkContractEntity.InvoiceEmail),
            [ColAccountNumber] = nameof(BulkContractEntity.AccountNumber),
            [ColVatNumber] = nameof(BulkContractEntity.VatNumber),
            [ColVatRating] = nameof(BulkContractEntity.VatRating),
            [ColPurchaseOrderNumber] = nameof(BulkContractEntity.PurchaseOrderNumber),
        });

        Map<BulkContractItemEntity>(new(StringComparer.OrdinalIgnoreCase)
        {
            ["fldContractId"] = nameof(BulkContractItemEntity.ContractId),
            [ColParticipantSchemeId] = nameof(BulkContractItemEntity.ParticipantSchemeId),
            ["fldParticipantId"] = nameof(BulkContractItemEntity.ParticipantId),
            [ColSchemeId] = nameof(BulkContractItemEntity.SchemeId),
            [ColIdentifier] = nameof(BulkContractItemEntity.Identifier),
            ["fldSchemeName"] = nameof(BulkContractItemEntity.SchemeName),
            ["fldLabCode"] = nameof(BulkContractItemEntity.LabCode),
            [ColLabName] = nameof(BulkContractItemEntity.LabName),
            ["fldNoOfDistributions"] = nameof(BulkContractItemEntity.NoOfDistributions),
            [ColPrice] = nameof(BulkContractItemEntity.Price),
        });

        Map<CoreScheme>(new(StringComparer.OrdinalIgnoreCase)
        {
            [ColSchemeId] = nameof(CoreScheme.SchemeId),
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
            ["fldCanEditJan"] = nameof(CoreScheme.CanEditJan),
            ["fldCanEditFeb"] = nameof(CoreScheme.CanEditFeb),
            ["fldCanEditMar"] = nameof(CoreScheme.CanEditMar),
            ["fldCanEditApr"] = nameof(CoreScheme.CanEditApr),
            ["fldCanEditMay"] = nameof(CoreScheme.CanEditMay),
            ["fldCanEditJun"] = nameof(CoreScheme.CanEditJun),
            ["fldCanEditJul"] = nameof(CoreScheme.CanEditJul),
            ["fldCanEditAug"] = nameof(CoreScheme.CanEditAug),
            ["fldCanEditSep"] = nameof(CoreScheme.CanEditSep),
            ["fldCanEditOct"] = nameof(CoreScheme.CanEditOct),
            ["fldCanEditNov"] = nameof(CoreScheme.CanEditNov),
            ["fldCanEditDec"] = nameof(CoreScheme.CanEditDec),
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
            [ColSchemeId] = nameof(SchemeCurrencyEntity.SchemeId),
            [ColCurrencyId] = nameof(SchemeCurrencyEntity.CurrencyId),
            [ColPrice] = nameof(SchemeCurrencyEntity.Price),
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
            [ColCountry] = nameof(CountryEntity.Country),
            ["fldCountryType"] = nameof(CountryEntity.CountryType),
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
            [ColVatRating] = nameof(VatRatingEntity.VatRating),
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
            [ColQalNumber] = nameof(SampleAddressEntity.QalNumber),
            [ColLabCode] = nameof(SampleAddressEntity.LabCode),
            [ColContactName] = nameof(SampleAddressEntity.ContactName),
            [ColOrganisation] = nameof(SampleAddressEntity.Organisation),
            [ColAddress1] = nameof(SampleAddressEntity.Address1),
            [ColAddress2] = nameof(SampleAddressEntity.Address2),
            [ColAddress3] = nameof(SampleAddressEntity.Address3),
            [ColAddress4] = nameof(SampleAddressEntity.Address4),
            [ColAddress5] = nameof(SampleAddressEntity.Address5),
            [ColCountry] = nameof(SampleAddressEntity.Country),
            [ColTelephone] = nameof(SampleAddressEntity.Telephone),
            [ColFax] = nameof(SampleAddressEntity.Fax),
            [ColEmail] = nameof(SampleAddressEntity.Email),
            [ColVatNumber] = nameof(SampleAddressEntity.VatNumber),
            [ColAccountNumber] = nameof(SampleAddressEntity.AccountNumber),
            [ColVatRating] = nameof(SampleAddressEntity.VatRating),
            [ColPurchaseOrderNumber] = nameof(SampleAddressEntity.PurchaseOrderNumber),
        });

        Map<SampleAddressSchemeEntity>(new(StringComparer.OrdinalIgnoreCase)
        {
            [ColContractId] = nameof(SampleAddressSchemeEntity.ContractId),
            [ColParticipantId] = nameof(SampleAddressSchemeEntity.ParticipantId),
            [ColParticipantSchemeId] = nameof(SampleAddressSchemeEntity.ParticipantSchemeId),
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
            [ColCountry] = nameof(ContractRenewalEntity.Country),
            ["ContractStartDate"] = nameof(ContractRenewalEntity.ContractStartDate),
            ["ContractEndDate"] = nameof(ContractRenewalEntity.ContractEndDate),
            ["RenewalInformation"] = nameof(ContractRenewalEntity.RenewalInformation),
        });

        Map<AdministrationChargeEntity>(new(StringComparer.OrdinalIgnoreCase)
        {
            ["fldAdministrationChargeId"] = nameof(AdministrationChargeEntity.AdministrationChargeId),
            [ColAdministrationCharge] = nameof(AdministrationChargeEntity.Name),
        });

        Map<AdministrationChargeCurrencyEntity>(new(StringComparer.OrdinalIgnoreCase)
        {
            ["fldAdministrationChargeCurrencyId"] = nameof(AdministrationChargeCurrencyEntity.AdministrationChargeCurrencyId),
            ["fldAdministrationChargeId"] = nameof(AdministrationChargeCurrencyEntity.AdministrationChargeId),
            [ColCurrencyId] = nameof(AdministrationChargeCurrencyEntity.CurrencyId),
            [ColPrice] = nameof(AdministrationChargeCurrencyEntity.Price),
        });

        Map<PricingPercentageEntity>(new(StringComparer.OrdinalIgnoreCase)
        {
            ["fldPricingPercentageId"] = nameof(PricingPercentageEntity.PricingPercentageId),
            [ColYearId] = nameof(PricingPercentageEntity.YearId),
            ["fldNumberOfDistributionsOnScheme"] = nameof(PricingPercentageEntity.NumberOfDistributionsOnScheme),
            ["fldNumberOfDistributionsChosen"] = nameof(PricingPercentageEntity.NumberOfDistributionsChosen),
            ["fldWeight"] = nameof(PricingPercentageEntity.Weight),
        });

        // spgaExportContractDetailsForAutomaticInvoicing's first result set (docs/analysis/
        // invoice-analysis.md) - only the columns InvoiceCsvBuilder/InvoiceService actually need
        // are mapped; every other column the procedure returns is left unmapped and silently skipped.
        Map<InvoiceContractEntity>(new(StringComparer.OrdinalIgnoreCase)
        {
            [ColContractId] = nameof(InvoiceContractEntity.ContractId),
            [ColCustomerId] = nameof(InvoiceContractEntity.CustomerId),
            [ColSuffix] = nameof(InvoiceContractEntity.Suffix),
            [ColYearId] = nameof(InvoiceContractEntity.YearId),
            [ColAdministrationCharge] = nameof(InvoiceContractEntity.AdministrationCharge),
            ["fldDiscountRate"] = nameof(InvoiceContractEntity.DiscountRate),
            ["fldNumberPostage"] = nameof(InvoiceContractEntity.NumberPostage),
            ["fldPostagePrice"] = nameof(InvoiceContractEntity.PostagePrice),
            ["fldNumberCourier"] = nameof(InvoiceContractEntity.NumberCourier),
            ["fldCourierPrice"] = nameof(InvoiceContractEntity.CourierPrice),
            ["fldNumberSpecialDelivery"] = nameof(InvoiceContractEntity.NumberSpecialDelivery),
            ["fldSpecialDeliveryPrice"] = nameof(InvoiceContractEntity.SpecialDeliveryPrice),
            [ColQalNumber] = nameof(InvoiceContractEntity.QalNumber),
            [ColInvoiceOrganisation] = nameof(InvoiceContractEntity.InvoiceOrganisation),
            [ColInvoiceAddress1] = nameof(InvoiceContractEntity.InvoiceAddress1),
            [ColInvoiceAddress2] = nameof(InvoiceContractEntity.InvoiceAddress2),
            [ColInvoiceAddress3] = nameof(InvoiceContractEntity.InvoiceAddress3),
            [ColInvoiceAddress4] = nameof(InvoiceContractEntity.InvoiceAddress4),
            [ColInvoiceAddress5] = nameof(InvoiceContractEntity.InvoiceAddress5),
            ["fldInvoiceCountry"] = nameof(InvoiceContractEntity.InvoiceCountry),
            [ColVatNumber] = nameof(InvoiceContractEntity.VatNumber),
            [ColVatRating] = nameof(InvoiceContractEntity.VatRating),
            [ColPurchaseOrderNumber] = nameof(InvoiceContractEntity.PurchaseOrderNumber),
            ["fldCustomerNumber"] = nameof(InvoiceContractEntity.CustomerNumber),
            ["fldCustomerType"] = nameof(InvoiceContractEntity.CustomerType),
            ["fldOptOutOfInvoiceGeneration"] = nameof(InvoiceContractEntity.OptOutOfInvoiceGeneration),
        });

        // spgaExportContractDetailsForAutomaticInvoicing's second result set.
        Map<InvoiceContractItemEntity>(new(StringComparer.OrdinalIgnoreCase)
        {
            [ColContractId] = nameof(InvoiceContractItemEntity.ContractId),
            [ColParticipantSchemeId] = nameof(InvoiceContractItemEntity.ParticipantSchemeId),
            [ColIdentifier] = nameof(InvoiceContractItemEntity.SchemeIdentifier),
            ["fldSchemeName"] = nameof(InvoiceContractItemEntity.SchemeName),
            [ColPrice] = nameof(InvoiceContractItemEntity.Price),
            ["fldNonFeePaying"] = nameof(InvoiceContractItemEntity.NonFeePaying),
            ["fldHasOverride"] = nameof(InvoiceContractItemEntity.HasOverride),
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
