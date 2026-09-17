using Microsoft.EntityFrameworkCore;
using SmartPOS_ERP.Models;

namespace SmartPOS_ERP.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<Product> Products { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderDetail> OrderDetails { get; set; }
        public DbSet<Supplier> Suppliers { get; set; }
        public DbSet<SupplierPayment> SupplierPayments { get; set; }
        public DbSet<PurchaseInvoice> PurchaseInvoices { get; set; }
        public DbSet<PurchaseDetail> PurchaseDetails { get; set; }
        public DbSet<Expense> Expenses { get; set; }
        public DbSet<SalesReturn> SalesReturns { get; set; }
        public DbSet<StockLedger> StockLedgers { get; set; }
        public DbSet<Shift> Shifts { get; set; }
        public DbSet<StoreSettings> StoreSettings { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<CreditInvoice> CreditInvoices { get; set; }
        public DbSet<CreditInvoiceDetail> CreditInvoiceDetails { get; set; }
        public DbSet<CreditPayment> CreditPayments { get; set; }
        public DbSet<HeldSale> HeldSales { get; set; }
        public DbSet<PurchaseReturn> PurchaseReturns { get; set; }
        public DbSet<PurchaseReturnDetail> PurchaseReturnDetails { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Username)
                .IsUnique();

            modelBuilder.Entity<PurchaseInvoice>()
                .HasOne(i => i.Supplier)
                .WithMany(s => s.Invoices)
                .HasForeignKey(i => i.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PurchaseDetail>()
                .HasOne(d => d.PurchaseInvoice)
                .WithMany(i => i.Details)
                .HasForeignKey(d => d.PurchaseInvoiceId);

            modelBuilder.Entity<SupplierPayment>()
                .HasOne(p => p.Supplier)
                .WithMany(s => s.Payments)
                .HasForeignKey(p => p.SupplierId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PurchaseReturn>()
                .HasOne(r => r.Supplier)
                .WithMany(s => s.Returns)
                .HasForeignKey(r => r.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PurchaseReturn>()
                .HasOne(r => r.PurchaseInvoice)
                .WithMany(i => i.Returns)
                .HasForeignKey(r => r.PurchaseInvoiceId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PurchaseReturnDetail>()
                .HasOne(d => d.PurchaseReturn)
                .WithMany(r => r.Details)
                .HasForeignKey(d => d.PurchaseReturnId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PurchaseReturnDetail>()
                .HasOne(d => d.PurchaseDetail)
                .WithMany()
                .HasForeignKey(d => d.PurchaseDetailId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PurchaseReturnDetail>()
                .HasOne(d => d.Product)
                .WithMany()
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Expense>()
                .HasOne(e => e.Shift)
                .WithMany()
                .HasForeignKey(e => e.ShiftId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<SupplierPayment>()
                .HasOne(p => p.Shift)
                .WithMany()
                .HasForeignKey(p => p.ShiftId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<StockLedger>()
                .HasOne(s => s.Product)
                .WithMany()
                .HasForeignKey(s => s.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<OrderDetail>()
                .Property(d => d.UnitCost)
                .HasColumnType("decimal(18, 2)");

            modelBuilder.Entity<OrderDetail>()
                .Property(d => d.TaxRate)
                .HasColumnType("decimal(18, 2)");

            modelBuilder.Entity<StoreSettings>()
                .Property(s => s.DefaultTaxRate)
                .HasColumnType("decimal(18, 2)");

            modelBuilder.Entity<StoreSettings>()
                .Property(s => s.FactoryResetPinHash)
                .HasMaxLength(100);

            modelBuilder.Entity<StoreSettings>().HasData(new StoreSettings
            {
                Id = 1,
                StoreName = "Sama_POS",
                InvoiceFooter = "شكراً لتعاملكم معنا — Sama_POS",
                TaxEnabled = true,
                DefaultTaxRate = 0m,
                PrintReceiptAfterSale = false,
                EnablePaymentCash = true,
                EnablePaymentCard = true,
                EnablePaymentTransfer = true
            });

            modelBuilder.Entity<Shift>()
                .HasOne(s => s.User)
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            if (Database.IsSqlServer())
            {
                modelBuilder.Entity<Product>()
                    .HasIndex(p => p.Barcode)
                    .IsUnique()
                    .HasFilter("[Barcode] IS NOT NULL AND [Barcode] <> N''")
                    .HasDatabaseName("IX_Products_Barcode");

                modelBuilder.Entity<Shift>()
                    .HasIndex(s => s.UserId)
                    .IsUnique()
                    .HasFilter("[ClosedAt] IS NULL")
                    .HasDatabaseName("IX_Shifts_UserId_Open");
            }
            else
            {
                modelBuilder.Entity<Product>()
                    .Property(p => p.RowVersion)
                    .IsConcurrencyToken(false)
                    .ValueGeneratedNever();
            }

            modelBuilder.Entity<Order>()
                .HasOne(o => o.Shift)
                .WithMany()
                .HasForeignKey(o => o.ShiftId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<SalesReturn>()
                .HasOne(r => r.Shift)
                .WithMany()
                .HasForeignKey(r => r.ShiftId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<StockLedger>()
                .HasOne(s => s.Shift)
                .WithMany()
                .HasForeignKey(s => s.ShiftId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CreditInvoice>()
                .HasOne(i => i.Customer)
                .WithMany(c => c.Invoices)
                .HasForeignKey(i => i.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CreditInvoice>()
                .HasOne(i => i.Order)
                .WithMany()
                .HasForeignKey(i => i.OrderId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CreditInvoice>()
                .HasOne(i => i.Shift)
                .WithMany()
                .HasForeignKey(i => i.ShiftId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CreditInvoiceDetail>()
                .HasOne(d => d.CreditInvoice)
                .WithMany(i => i.Details)
                .HasForeignKey(d => d.CreditInvoiceId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CreditPayment>()
                .HasOne(p => p.CreditInvoice)
                .WithMany(i => i.Payments)
                .HasForeignKey(p => p.CreditInvoiceId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CreditPayment>()
                .HasOne(p => p.Shift)
                .WithMany()
                .HasForeignKey(p => p.ShiftId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CreditInvoiceDetail>()
                .Property(d => d.Quantity)
                .HasColumnType("decimal(18, 3)");

            modelBuilder.Entity<CreditInvoiceDetail>()
                .Property(d => d.UnitCost)
                .HasColumnType("decimal(18, 2)");

            modelBuilder.Entity<CreditInvoiceDetail>()
                .Property(d => d.TaxRate)
                .HasColumnType("decimal(18, 2)");

            modelBuilder.Entity<Order>()
                .Property(o => o.DiscountAmount)
                .HasColumnType("decimal(18, 2)");

            modelBuilder.Entity<Order>()
                .Property(o => o.PaymentMethod)
                .HasMaxLength(20);

            modelBuilder.Entity<Product>()
                .Property(p => p.Category)
                .HasMaxLength(50);

            modelBuilder.Entity<HeldSale>()
                .HasIndex(h => h.UserName);

            modelBuilder.Entity<HeldSale>()
                .Property(h => h.TotalAmount)
                .HasColumnType("decimal(18, 2)");
        }

        public Task<Product?> GetProductWithRowLockAsync(int productId)
        {
            if (!Database.IsSqlServer())
            {
                return Products.SingleOrDefaultAsync(p => p.Id == productId);
            }

            return Products
                .FromSqlInterpolated($"SELECT * FROM Products WITH (UPDLOCK, ROWLOCK) WHERE Id = {productId}")
                .SingleOrDefaultAsync();
        }

        public Task<int?> GetOpenShiftIdAsync(string? userName)
        {
            if (string.IsNullOrWhiteSpace(userName))
            {
                return Task.FromResult<int?>(null);
            }

            return Shifts
                .Where(s => s.ClosedAt == null && s.User.Username == userName)
                .Select(s => (int?)s.Id)
                .FirstOrDefaultAsync();
        }

        public int? GetOpenShiftId(string? userName)
        {
            if (string.IsNullOrWhiteSpace(userName))
            {
                return null;
            }

            return Shifts
                .Where(s => s.ClosedAt == null && s.User.Username == userName)
                .Select(s => (int?)s.Id)
                .FirstOrDefault();
        }
    }
}
