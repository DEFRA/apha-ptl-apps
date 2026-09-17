using Microsoft.EntityFrameworkCore;
using PTL.Core.Customer;
using PTL.Core.Participant;
using CoreCustomer = PTL.Core.Customer.Customer;
using CoreParticipant = PTL.Core.Participant.Participant;

namespace PTL.Data;

// The database schema already exists; this context only maps entities to existing tables/columns
// so that stored procedures can be executed and their results materialised via EF Core.
public class PtlDbContext(DbContextOptions<PtlDbContext> options) : DbContext(options)
{
    public DbSet<CoreCustomer> Customers => Set<CoreCustomer>();
    public DbSet<CustomerSummaryEntity> CustomerSummaries => Set<CustomerSummaryEntity>();
    public DbSet<CoreParticipant> Participants => Set<CoreParticipant>();
    public DbSet<ParticipantSummaryEntity> ParticipantSummaries => Set<ParticipantSummaryEntity>();

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
    }
}
