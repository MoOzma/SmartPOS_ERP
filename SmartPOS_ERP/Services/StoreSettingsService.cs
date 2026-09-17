using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SmartPOS_ERP.Data;
using SmartPOS_ERP.Models;

namespace SmartPOS_ERP.Services;

public class StoreSettingsService
{
    public const int SingletonId = 1;
    public const string DefaultStoreName = "Sama_POS";
    public const string DefaultInvoiceFooter = "شكراً لتعاملكم معنا — Sama_POS";
    public const long MaxLogoBytes = 2 * 1024 * 1024;

    private static readonly HashSet<string> LogoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };

    private readonly ApplicationDbContext _db;
    private readonly IWebHostEnvironment? _env;

    public StoreSettingsService(ApplicationDbContext db, IWebHostEnvironment? env = null)
    {
        _db = db;
        _env = env;
    }

    public static decimal EffectiveTaxRate(Product product, StoreSettings settings)
    {
        if (!settings.TaxEnabled)
        {
            return 0m;
        }

        return product.UseCustomTax ? product.TaxRate : settings.DefaultTaxRate;
    }

    public static StoreSettings CreateDefault() => new()
    {
        Id = SingletonId,
        StoreName = DefaultStoreName,
        InvoiceFooter = DefaultInvoiceFooter,
        TaxEnabled = true,
        DefaultTaxRate = 0m,
        PrintReceiptAfterSale = false,
        EnablePaymentCash = true,
        EnablePaymentCard = true,
        EnablePaymentTransfer = true
    };

    public async Task<StoreSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        var row = await _db.StoreSettings.FindAsync([SingletonId], cancellationToken);
        if (row != null)
        {
            return row;
        }

        row = CreateDefault();
        _db.StoreSettings.Add(row);
        await _db.SaveChangesAsync(cancellationToken);
        return row;
    }

    public async Task<string?> SaveAsync(
        StoreSettings incoming,
        IFormFile? logo,
        bool removeLogo,
        CancellationToken cancellationToken = default)
    {
        var current = await GetAsync(cancellationToken);
        current.StoreName = incoming.StoreName.Trim();
        current.Phone = string.IsNullOrWhiteSpace(incoming.Phone) ? null : incoming.Phone.Trim();
        current.Address = string.IsNullOrWhiteSpace(incoming.Address) ? null : incoming.Address.Trim();
        current.InvoiceFooter = string.IsNullOrWhiteSpace(incoming.InvoiceFooter)
            ? DefaultInvoiceFooter
            : incoming.InvoiceFooter.Trim();
        current.TaxEnabled = incoming.TaxEnabled;
        current.DefaultTaxRate = incoming.DefaultTaxRate;
        current.PrintReceiptAfterSale = incoming.PrintReceiptAfterSale;
        current.EnablePaymentCash = incoming.EnablePaymentCash;
        current.EnablePaymentCard = incoming.EnablePaymentCard;
        current.EnablePaymentTransfer = incoming.EnablePaymentTransfer;
        if (!current.EnablePaymentCash && !current.EnablePaymentCard && !current.EnablePaymentTransfer)
        {
            current.EnablePaymentCash = true;
        }

        if (removeLogo && (logo == null || logo.Length == 0))
        {
            DeleteLogoFile(current.LogoPath);
            current.LogoPath = null;
        }

        if (logo is { Length: > 0 })
        {
            var error = await SaveLogoAsync(current, logo, cancellationToken);
            if (error != null)
            {
                return error;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return null;
    }

    private async Task<string?> SaveLogoAsync(StoreSettings current, IFormFile logo, CancellationToken cancellationToken)
    {
        if (logo.Length > MaxLogoBytes)
        {
            return "حجم الشعار يجب ألا يتجاوز 2 ميجابايت.";
        }

        var ext = Path.GetExtension(logo.FileName);
        if (string.IsNullOrWhiteSpace(ext) || !LogoExtensions.Contains(ext))
        {
            return "صيغة الشعار غير مدعومة. استخدم jpg أو png أو webp.";
        }

        if (string.IsNullOrWhiteSpace(_env?.WebRootPath))
        {
            return "تعذر حفظ الشعار.";
        }

        var dir = Path.Combine(_env.WebRootPath, "uploads", "store");
        Directory.CreateDirectory(dir);

        DeleteLogoFile(current.LogoPath);

        var fileName = "logo" + ext.ToLowerInvariant();
        var physical = Path.Combine(dir, fileName);
        await using (var stream = File.Create(physical))
        {
            await logo.CopyToAsync(stream, cancellationToken);
        }

        current.LogoPath = "/uploads/store/" + fileName;
        return null;
    }

    private void DeleteLogoFile(string? logoPath)
    {
        if (string.IsNullOrWhiteSpace(logoPath) || string.IsNullOrWhiteSpace(_env?.WebRootPath))
        {
            return;
        }

        var relative = logoPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        if (relative.Contains("..", StringComparison.Ordinal))
        {
            return;
        }

        var physical = Path.Combine(_env.WebRootPath, relative);
        if (File.Exists(physical))
        {
            File.Delete(physical);
        }
    }
}
