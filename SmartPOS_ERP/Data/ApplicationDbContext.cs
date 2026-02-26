using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SmartPOS_ERP.Models;

namespace SmartPOS_ERP.Data
{
    public class ApplicationDbContext : IdentityDbContext
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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // ضروري جداً لتشغيل جداول Identity (المستخدمين والصلاحيات)
            base.OnModelCreating(modelBuilder);

            // 1. ربط فاتورة التوريد بالمورد
            modelBuilder.Entity<PurchaseInvoice>()
                .HasOne(i => i.Supplier)
                .WithMany(s => s.Invoices) // تأكد أن كلاس Supplier يحتوي على List<PurchaseInvoice> Invoices
                .HasForeignKey(i => i.SupplierId)
                .OnDelete(DeleteBehavior.Restrict); // منع مسح المورد إذا كان له فواتير

            // 2. ربط تفاصيل الفاتورة بالفاتورة الأم
            modelBuilder.Entity<PurchaseDetail>()
                .HasOne(d => d.PurchaseInvoice)
                .WithMany(i => i.Details)
                .HasForeignKey(d => d.PurchaseInvoiceId);

            modelBuilder.Entity<SupplierPayment>()
                .HasOne(p => p.Supplier)
                .WithMany(s => s.Payments) // تأكد أن كلاس Supplier يحتوي على List<SupplierPayment> Payments
                .HasForeignKey(p => p.SupplierId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}