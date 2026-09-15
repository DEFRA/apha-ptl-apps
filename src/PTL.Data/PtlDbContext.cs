using Microsoft.EntityFrameworkCore;
using PTL.Core.Customer;
using CoreCustomer = PTL.Core.Customer.Customer;

namespace PTL.Data;

// The database schema already exists; this context only maps entities to existing tables/columns
// so that stored procedures can be executed and their results materialised via EF Core.
public class PtlDbContext(DbContextOptions<PtlDbContext> options) : DbContext(options)
{
    public DbSet<CoreCustomer> Customers => Set<CoreCustomer>();
    public DbSet<CustomerSummaryEntity> CustomerSummaries => Set<CustomerSummaryEntity>();

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
    }
}
