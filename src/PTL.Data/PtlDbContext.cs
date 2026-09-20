using Microsoft.EntityFrameworkCore;
using PTL.Core.Customer;
using PTL.Core.Participant;
using PTL.Core.Contract;
using PTL.Core.Scheme;
using PTL.Core.Lookup;
using CoreCustomer = PTL.Core.Customer.Customer;
using CoreParticipant = PTL.Core.Participant.Participant;
using CoreContract = PTL.Core.Contract.Contract;
using CoreScheme = PTL.Core.Scheme.Scheme;

namespace PTL.Data;

// The database schema already exists; this context only maps entities to existing tables/columns
// so that stored procedures can be executed and their results materialised via EF Core.
public class PtlDbContext(DbContextOptions<PtlDbContext> options) : DbContext(options)
{
    public DbSet<CoreCustomer> Customers => Set<CoreCustomer>();
    public DbSet<CustomerSummaryEntity> CustomerSummaries => Set<CustomerSummaryEntity>();
    public DbSet<CoreParticipant> Participants => Set<CoreParticipant>();
    public DbSet<ParticipantSummaryEntity> ParticipantSummaries => Set<ParticipantSummaryEntity>();
    public DbSet<CoreContract> Contracts => Set<CoreContract>();
    public DbSet<ContractSummaryEntity> ContractSummaries => Set<ContractSummaryEntity>();
    public DbSet<CoreScheme> Schemes => Set<CoreScheme>();
    public DbSet<SchemeSummaryEntity> SchemeSummaries => Set<SchemeSummaryEntity>();
    public DbSet<SchemeHistoryEntity> SchemeHistory => Set<SchemeHistoryEntity>();
    public DbSet<SchemeCurrencyEntity> SchemeCurrencies => Set<SchemeCurrencyEntity>();
    public DbSet<PostagePricingPlanEntity> PostagePricingPlans => Set<PostagePricingPlanEntity>();
    public DbSet<CountryEntity> Countries => Set<CountryEntity>();
    public DbSet<CurrencyEntity> Currencies => Set<CurrencyEntity>();
    public DbSet<CustomerTypeEntity> CustomerTypes => Set<CustomerTypeEntity>();
    public DbSet<VatRatingEntity> VatRatings => Set<VatRatingEntity>();
    public DbSet<LabTypeEntity> LabTypes => Set<LabTypeEntity>();
    public DbSet<YearEntity> Years => Set<YearEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CoreCustomer>(entity =>
        {
            entity.ToTable("tblCustomer");
            entity.HasKey(c => c.CustomerId);
            entity.Property(c => c.CustomerId).HasColumnName("fldCustomerId");
            entity.Property(c => c.QalNumber).HasColumnName("fldQalNumber");
            entity.Property(c => c.RegisteredFileNumber).HasColumnName("fldRegisteredFileNumber");
            entity.Property(c => c.Name).HasColumnName("fldName");
            entity.Property(c => c.PreviousName).HasColumnName("fldPreviousName");
            entity.Property(c => c.CustomerTypeId).HasColumnName("fldCustomerTypeID");
            entity.Property(c => c.VatNumber).HasColumnName("fldVatNumber");
            entity.Property(c => c.VatRatingId).HasColumnName("fldVatRatingId");
            entity.Property(c => c.AccountNumber).HasColumnName("fldAccountNumber");
            entity.Property(c => c.CustomerFinanceId).HasColumnName("fldCustomerFinanceId");
            entity.Property(c => c.ContactName).HasColumnName("fldContactName");
            entity.Property(c => c.Organisation).HasColumnName("fldOrganisation");
            entity.Property(c => c.Address1).HasColumnName("fldAddress1");
            entity.Property(c => c.Address2).HasColumnName("fldAddress2");
            entity.Property(c => c.Address3).HasColumnName("fldAddress3");
            entity.Property(c => c.Address4).HasColumnName("fldAddress4");
            entity.Property(c => c.Address5).HasColumnName("fldAddress5");
            entity.Property(c => c.CountryId).HasColumnName("fldCountryId");
            entity.Property(c => c.Telephone).HasColumnName("fldTelephone");
            entity.Property(c => c.Telephone2).HasColumnName("fldTelephone2");
            entity.Property(c => c.Fax).HasColumnName("fldFax");
            entity.Property(c => c.Email).HasColumnName("fldEmail");
            entity.Property(c => c.CurrencyId).HasColumnName("fldCurrencyId");
            entity.Property(c => c.Comments).HasColumnName("fldComments");
            entity.Property(c => c.InitialStartDate).HasColumnName("fldInitialStartDate");
            entity.Property(c => c.PostageArrangements).HasColumnName("fldPostageArrangements");
            entity.Property(c => c.PaymentNonUK).HasColumnName("fldPaymentNonUK");
            entity.Property(c => c.InvoiceName).HasColumnName("fldInvoiceName");
            entity.Property(c => c.InvoiceOrganisation).HasColumnName("fldInvoiceOrganisation");
            entity.Property(c => c.InvoiceAddress1).HasColumnName("fldInvoiceAddress1");
            entity.Property(c => c.InvoiceAddress2).HasColumnName("fldInvoiceAddress2");
            entity.Property(c => c.InvoiceAddress3).HasColumnName("fldInvoiceAddress3");
            entity.Property(c => c.InvoiceAddress4).HasColumnName("fldInvoiceAddress4");
            entity.Property(c => c.InvoiceAddress5).HasColumnName("fldInvoiceAddress5");
            entity.Property(c => c.InvoiceCountryId).HasColumnName("fldInvoiceCountryId");
            entity.Property(c => c.InvoiceTelephone).HasColumnName("fldInvoiceTelephone");
            entity.Property(c => c.InvoiceTelephone2).HasColumnName("fldInvoiceTelephone2");
            entity.Property(c => c.InvoiceFax).HasColumnName("fldInvoiceFax");
            entity.Property(c => c.InvoiceEmail).HasColumnName("fldInvoiceEmail");
            entity.Property(c => c.IsActive).HasColumnName("fldIsActive");
            entity.Property(c => c.CanOrderOnline).HasColumnName("fldCanOrderOnline");
            entity.Property(c => c.InactiveDate).HasColumnName("fldInactiveDate");
            entity.Property(c => c.CustomerStatusId).HasColumnName("fldCustomerStatusId");
        });

        modelBuilder.Entity<CustomerSummaryEntity>(entity =>
        {
            entity.HasNoKey();
            entity.Property(c => c.CustomerId).HasColumnName("fldCustomerId");
            entity.Property(c => c.QalNumber).HasColumnName("fldQalNumber");
            entity.Property(c => c.Name).HasColumnName("fldName");
            entity.Property(c => c.Organisation).HasColumnName("fldOrganisation");
            entity.Property(c => c.IsActive).HasColumnName("fldIsActive");
        });

        modelBuilder.Entity<CoreParticipant>(entity =>
        {
            entity.ToTable("tblParticipant");
            entity.HasKey(p => p.ParticipantId);
            entity.Property(p => p.ParticipantId).HasColumnName("fldParticipantId");
            entity.Property(p => p.SsoId).HasColumnName("fldSsoId");
            entity.Property(p => p.CustomerId).HasColumnName("fldCustomerId");
            entity.Property(p => p.LabCode).HasColumnName("fldLabCode");
            entity.Property(p => p.LabName).HasColumnName("fldLabName");
            entity.Property(p => p.LabTypeId).HasColumnName("fldLabTypeId");
            entity.Property(p => p.ContactName).HasColumnName("fldContactName");
            entity.Property(p => p.Organisation).HasColumnName("fldOrganisation");
            entity.Property(p => p.Address1).HasColumnName("fldAddress1");
            entity.Property(p => p.Address2).HasColumnName("fldAddress2");
            entity.Property(p => p.Address3).HasColumnName("fldAddress3");
            entity.Property(p => p.Address4).HasColumnName("fldAddress4");
            entity.Property(p => p.Address5).HasColumnName("fldAddress5");
            entity.Property(p => p.CountryId).HasColumnName("fldCountryId");
            entity.Property(p => p.Telephone).HasColumnName("fldTelephone");
            entity.Property(p => p.Fax).HasColumnName("fldFax");
            entity.Property(p => p.Email).HasColumnName("fldEmail");
            entity.Property(p => p.Email2).HasColumnName("fldEmail2");
            entity.Property(p => p.Comments).HasColumnName("fldComments");
            entity.Property(p => p.IsActive).HasColumnName("fldIsActive");
            entity.Property(p => p.InactiveDate).HasColumnName("fldInactiveDate");
            entity.Property(p => p.InactiveError).HasColumnName("fldInactiveError");
            entity.Property(p => p.InactiveErrorDate).HasColumnName("fldInactiveErrorDate");
        });

        modelBuilder.Entity<ParticipantSummaryEntity>(entity =>
        {
            entity.HasNoKey();
            entity.Property(p => p.ParticipantId).HasColumnName("fldParticipantId");
            entity.Property(p => p.CustomerId).HasColumnName("fldCustomerId");
            entity.Property(p => p.LabCode).HasColumnName("fldLabCode");
            entity.Property(p => p.LabName).HasColumnName("fldLabName");
            entity.Property(p => p.ContactName).HasColumnName("fldContactName");
            entity.Property(p => p.IsActive).HasColumnName("fldIsActive");
        });

        // CustomerName and QalNumber are joined from tblCustomer, and IsReadOnly is computed -
        // none are real tblContract columns, but this entity is only ever populated via
        // FromSqlRaw(spgContractByContractId) / written via ExecuteSqlRawAsync(spiContract/spuContract),
        // never EF's own change-tracked SaveChanges, so mapping them to the stored procedure's result
        // column aliases here is safe (see ContractRepository).
        modelBuilder.Entity<CoreContract>(entity =>
        {
            entity.ToTable("tblContract");
            entity.HasKey(c => c.ContractId);
            entity.Property(c => c.ContractId).HasColumnName("fldContractId");
            entity.Property(c => c.CustomerId).HasColumnName("fldCustomerId");
            entity.Property(c => c.CustomerName).HasColumnName("fldCustomerName");
            entity.Property(c => c.QalNumber).HasColumnName("fldQALNumber");
            entity.Property(c => c.YearId).HasColumnName("fldYearId");
            entity.Property(c => c.UTNumber).HasColumnName("fldUTNumber");
            entity.Property(c => c.FTNumber).HasColumnName("fldFTNumber");
            entity.Property(c => c.ContractSignatory).HasColumnName("fldContractSignatory");
            entity.Property(c => c.ActionsRequired).HasColumnName("fldActionsRequired");
            entity.Property(c => c.RenewalInformation).HasColumnName("fldRenewalInformation");
            entity.Property(c => c.DiscountRate).HasColumnName("fldDiscountRate");
            entity.Property(c => c.AdministrationCharge).HasColumnName("fldAdministrationCharge");
            entity.Property(c => c.NumberCourier).HasColumnName("fldNumberCourier");
            entity.Property(c => c.CourierPrice).HasColumnName("fldCourierPrice");
            entity.Property(c => c.NumberPostage).HasColumnName("fldNumberPostage");
            entity.Property(c => c.PostagePrice).HasColumnName("fldPostagePrice");
            entity.Property(c => c.NumberSpecialDelivery).HasColumnName("fldNumberSpecialDelivery");
            entity.Property(c => c.SpecialDeliveryPrice).HasColumnName("fldSpecialDeliveryPrice");
            entity.Property(c => c.AcknowledgementPostedDate).HasColumnName("fldAcknowledgementPostedDate");
            entity.Property(c => c.AcknowledgementReturnedDate).HasColumnName("fldAcknowledgementReturnedDate");
            entity.Property(c => c.JobSheetPostedDate).HasColumnName("fldJobSheetPostedDate");
            entity.Property(c => c.ReasonForClosure).HasColumnName("fldReasonForClosure");
            entity.Property(c => c.DateOfLeaving).HasColumnName("fldDateOfLeaving");
            entity.Property(c => c.IsActive).HasColumnName("fldIsActive");
            entity.Property(c => c.IsReadOnly).HasColumnName("Readonly");
            entity.Property(c => c.Suffix).HasColumnName("fldSuffix");
            entity.Property(c => c.CommencementDate).HasColumnName("fldCommencementDate");
            entity.Property(c => c.PurchaseOrderNumber).HasColumnName("fldPurchaseOrderNumber");
            entity.Property(c => c.OptOutOfInvoiceGeneration).HasColumnName("fldOptOutOfInvoiceGeneration");
            entity.Property(c => c.IsInvoiceSent).HasColumnName("fldIsInvoiceSent");
            entity.Property(c => c.IsOnlineOrder).HasColumnName("fldIsOnlineOrder");
            entity.Property(c => c.ApprovedBy).HasColumnName("fldApprovedBy");
            entity.Property(c => c.ApprovedDate).HasColumnName("fldApprovedDate");
        });

        modelBuilder.Entity<ContractSummaryEntity>(entity =>
        {
            entity.HasNoKey();
            entity.Property(c => c.ContractId).HasColumnName("fldContractId");
            entity.Property(c => c.CustomerId).HasColumnName("fldCustomerId");
            entity.Property(c => c.YearId).HasColumnName("fldYearId");
            entity.Property(c => c.IsActive).HasColumnName("fldIsActive");
            entity.Property(c => c.Suffix).HasColumnName("fldSuffix");
        });

        // SampleNoSequence is joined from tblSchedule (fldSampleNoSequence) and IsReadOnly is
        // computed - neither is a real tblScheme column, but this entity is only ever populated
        // via FromSqlRaw(spgSchemeBySchemeId) / written via ExecuteSqlRawAsync(spiScheme/spuScheme),
        // never EF's own change-tracked SaveChanges - see SchemeRepository, and ContractRepository
        // for the equivalent pattern already established for Contract.IsReadOnly.
        modelBuilder.Entity<CoreScheme>(entity =>
        {
            entity.ToTable("tblScheme");
            entity.HasKey(s => s.SchemeId);
            entity.Property(s => s.SchemeId).HasColumnName("fldSchemeId");
            entity.Property(s => s.SharedId).HasColumnName("fldSharedId");
            entity.Property(s => s.YearId).HasColumnName("fldYearId");
            entity.Property(s => s.Identifier).HasColumnName("fldIdentifier");
            entity.Property(s => s.Name).HasColumnName("fldName");
            entity.Property(s => s.ScheduleId).HasColumnName("fldScheduleId");
            entity.Property(s => s.ScheduleCodeId).HasColumnName("fldScheduleCodeId");
            entity.Property(s => s.StartDate).HasColumnName("fldStartDate");
            entity.Property(s => s.DistributionMonthJan).HasColumnName("fldDistributionMonthJan");
            entity.Property(s => s.DistributionMonthFeb).HasColumnName("fldDistributionMonthFeb");
            entity.Property(s => s.DistributionMonthMar).HasColumnName("fldDistributionMonthMar");
            entity.Property(s => s.DistributionMonthApr).HasColumnName("fldDistributionMonthApr");
            entity.Property(s => s.DistributionMonthMay).HasColumnName("fldDistributionMonthMay");
            entity.Property(s => s.DistributionMonthJun).HasColumnName("fldDistributionMonthJun");
            entity.Property(s => s.DistributionMonthJul).HasColumnName("fldDistributionMonthJul");
            entity.Property(s => s.DistributionMonthAug).HasColumnName("fldDistributionMonthAug");
            entity.Property(s => s.DistributionMonthSep).HasColumnName("fldDistributionMonthSep");
            entity.Property(s => s.DistributionMonthOct).HasColumnName("fldDistributionMonthOct");
            entity.Property(s => s.DistributionMonthNov).HasColumnName("fldDistributionMonthNov");
            entity.Property(s => s.DistributionMonthDec).HasColumnName("fldDistributionMonthDec");
            entity.Property(s => s.DistributionAsAvailable).HasColumnName("fldDistributionAsAvailable");
            entity.Property(s => s.WeekNumber).HasColumnName("fldWeekNumber");
            entity.Property(s => s.DayOfWeekId).HasColumnName("fldDayOfWeekId");
            entity.Property(s => s.Deadline).HasColumnName("fldDeadline");
            entity.Property(s => s.Pilot).HasColumnName("fldPilot");
            entity.Property(s => s.Accredited).HasColumnName("fldAccredited");
            entity.Property(s => s.ComerciallyAvailable).HasColumnName("fldComerciallyAvailable");
            entity.Property(s => s.LimitedSampleAvailability).HasColumnName("fldLimitedSampleAvailability");
            entity.Property(s => s.NoVLALabs).HasColumnName("fldNoVLALabs");
            entity.Property(s => s.CombinedPackaging).HasColumnName("fldCombinedPackaging");
            entity.Property(s => s.SampleOrigin).HasColumnName("fldSampleOrigin");
            entity.Property(s => s.Subcontractor).HasColumnName("fldSubcontractor");
            entity.Property(s => s.NumberOfSamples).HasColumnName("fldNumberOfSamples");
            entity.Property(s => s.SamplePackingInstructions).HasColumnName("fldSamplePackingInstructions");
            entity.Property(s => s.TestConsultant1).HasColumnName("fldTestConsultant1");
            entity.Property(s => s.TestConsultant2).HasColumnName("fldTestConsultant2");
            entity.Property(s => s.TestConsultant3).HasColumnName("fldTestConsultant3");
            entity.Property(s => s.CommentsRequired).HasColumnName("fldCommentsRequired");
            entity.Property(s => s.DateOfReceipt).HasColumnName("fldDateOfReceipt");
            entity.Property(s => s.StorageConditions).HasColumnName("fldStorageConditions");
            entity.Property(s => s.ConditionOnReceipt).HasColumnName("fldConditionOnReceipt");
            entity.Property(s => s.Instructions).HasColumnName("fldInstructions");
            entity.Property(s => s.SampleNoSequence).HasColumnName("fldSampleNoSequence");
            entity.Property(s => s.TestConsultantTabulationId).HasColumnName("fldTestConsultantTabulationId");
            entity.Property(s => s.UseExternalReference).HasColumnName("fldUseExternalReference");
            entity.Property(s => s.LastModified).HasColumnName("fldLastModified");
            entity.Property(s => s.StoreRatings).HasColumnName("fldStoreRatings");
            entity.Property(s => s.Assessor1).HasColumnName("fldAssessor1");
            entity.Property(s => s.Assessor2).HasColumnName("fldAssessor2");
            entity.Property(s => s.Assessor3).HasColumnName("fldAssessor3");
            entity.Property(s => s.Assessor4).HasColumnName("fldAssessor4");
            entity.Property(s => s.RequiresAssessment).HasColumnName("fldRequiresAssessment");
            entity.Property(s => s.StandardTabulationText).HasColumnName("fldStandardTabulationText");
            entity.Property(s => s.Postage).HasColumnName("fldPostage");
            entity.Property(s => s.CustomsDescription).HasColumnName("fldCustomsDocumentDescription");
            entity.Property(s => s.CustomsVolume).HasColumnName("fldCustomsDocumentVolume");
            entity.Property(s => s.DataConsentDeclarationActive).HasColumnName("fldDataConsentDeclarationActive");
            entity.Property(s => s.DataConsentDeclarationText).HasColumnName("fldDataConsentDeclarationText");
            entity.Property(s => s.IsReadOnly).HasColumnName("Readonly");
        });

        modelBuilder.Entity<SchemeSummaryEntity>(entity =>
        {
            entity.HasNoKey();
            entity.Property(s => s.SharedId).HasColumnName("fldSharedId");
            entity.Property(s => s.YearId).HasColumnName("fldYearId");
            entity.Property(s => s.CurrentSchemeId).HasColumnName("fldCurrentSchemeId");
            entity.Property(s => s.CurrentIdentifier).HasColumnName("fldCurrentIdentifier");
            entity.Property(s => s.CurrentName).HasColumnName("fldCurrentName");
            entity.Property(s => s.NextSchemeId).HasColumnName("fldNextSchemeId");
            entity.Property(s => s.NextIdentifier).HasColumnName("fldNextIdentifier");
            entity.Property(s => s.NextName).HasColumnName("fldNextName");
            entity.Property(s => s.RecentSchemeId).HasColumnName("fldRecentSchemeId");
            entity.Property(s => s.RecentIdentifier).HasColumnName("fldRecentIdentifier");
            entity.Property(s => s.RecentName).HasColumnName("fldRecentName");
        });

        modelBuilder.Entity<SchemeHistoryEntity>(entity =>
        {
            entity.HasNoKey();
            entity.Property(s => s.SchemeId).HasColumnName("fldCurrentSchemeId");
            entity.Property(s => s.SharedId).HasColumnName("fldSharedId");
            entity.Property(s => s.YearId).HasColumnName("fldYearId");
            entity.Property(s => s.Identifier).HasColumnName("fldCurrentIdentifier");
            entity.Property(s => s.Name).HasColumnName("fldCurrentName");
        });

        modelBuilder.Entity<SchemeCurrencyEntity>(entity =>
        {
            entity.HasNoKey();
            entity.Property(s => s.SchemeCurrencyId).HasColumnName("fldSchemeCurrencyId");
            entity.Property(s => s.SchemeId).HasColumnName("fldSchemeId");
            entity.Property(s => s.CurrencyId).HasColumnName("fldCurrencyId");
            entity.Property(s => s.Price).HasColumnName("fldPrice");
            entity.Property(s => s.CurrencyName).HasColumnName("fldCurrencyName");
            entity.Property(s => s.CurrencySymbol).HasColumnName("fldCurrencySymbol");
        });

        modelBuilder.Entity<PostagePricingPlanEntity>(entity =>
        {
            entity.HasNoKey();
            entity.Property(p => p.PostageId).HasColumnName("fldPostageId");
            entity.Property(p => p.Name).HasColumnName("fldName");
            entity.Property(p => p.UKPrice).HasColumnName("fldUkPrice");
            entity.Property(p => p.EUPrice).HasColumnName("fldEuPrice");
            entity.Property(p => p.NonEUPrice).HasColumnName("fldNonEuPrice");
            entity.Property(p => p.YearId).HasColumnName("fldYearId");
        });

        // spgaCountry also returns fldCountryTypeId/fldCountryType/fldAllocationCount, but only the
        // id/name are needed for a dropdown - EF ignores unmapped result columns automatically.
        modelBuilder.Entity<CountryEntity>(entity =>
        {
            entity.HasNoKey();
            entity.Property(c => c.CountryId).HasColumnName("fldCountryId");
            entity.Property(c => c.Country).HasColumnName("fldCountry");
        });

        // LongName is computed client-side (Symbol + " - " + Name), matching
        // PtaBusinessObjects.BusinessObjects.SystemObjects.Currency.LongName - it is not a
        // spgaCurrency result column, so it must be excluded from the model.
        modelBuilder.Entity<CurrencyEntity>(entity =>
        {
            entity.HasNoKey();
            entity.Property(c => c.CurrencyId).HasColumnName("fldCurrencyId");
            entity.Property(c => c.Name).HasColumnName("fldName");
            entity.Property(c => c.Symbol).HasColumnName("fldSymbol");
            entity.Ignore(c => c.LongName);
        });

        modelBuilder.Entity<CustomerTypeEntity>(entity =>
        {
            entity.HasNoKey();
            entity.Property(c => c.CustomerTypeId).HasColumnName("fldCustomerTypeId");
            entity.Property(c => c.CustomerType).HasColumnName("fldCustomerType");
        });

        modelBuilder.Entity<VatRatingEntity>(entity =>
        {
            entity.HasNoKey();
            entity.Property(v => v.VatRatingId).HasColumnName("fldVatRatingId");
            entity.Property(v => v.VatRating).HasColumnName("fldVatRating");
        });

        modelBuilder.Entity<LabTypeEntity>(entity =>
        {
            entity.HasNoKey();
            entity.Property(l => l.LabTypeId).HasColumnName("fldLabTypeId");
            entity.Property(l => l.Name).HasColumnName("fldName");
        });

        modelBuilder.Entity<YearEntity>(entity =>
        {
            entity.HasNoKey();
            entity.Property(y => y.YearId).HasColumnName("fldYearId");
            entity.Property(y => y.Year).HasColumnName("fldYear");
        });
    }
}
