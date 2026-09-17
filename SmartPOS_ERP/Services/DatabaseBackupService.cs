using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using SmartPOS_ERP.Data;
using SmartPOS_ERP.Models;
using SmartPOS_ERP.Security;

namespace SmartPOS_ERP.Services;

public sealed class DatabaseBackupService
{
    public const int FormatVersion = 1;
    public const string AppName = "Sama_POS";
    public const long MaxImportBytes = 50 * 1024 * 1024;
    public const string InvalidFileMessage = "ملف النسخة الاحتياطية غير صالح.";
    public const string UnsupportedVersionMessage = "إصدار ملف النسخة غير مدعوم.";
    public const string MissingAdminMessage = "تعذر العثور على حساب المدير الحالي.";
    public const string IncompleteFileMessage = "ملف النسخة ناقص البيانات.";

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private readonly ApplicationDbContext _db;
    private readonly IWebHostEnvironment? _env;

    public DatabaseBackupService(ApplicationDbContext db, IWebHostEnvironment? env = null)
    {
        _db = db;
        _env = env;
    }

    public Task<bool> UserExistsAsync(string username, CancellationToken cancellationToken = default)
        => _db.Users.AnyAsync(u => u.Username == username, cancellationToken);

    public async Task<byte[]> ExportAsync(CancellationToken cancellationToken = default)
    {
        var payload = await LoadPayloadAsync(cancellationToken);
        var manifest = new DatabaseBackupManifest
        {
            FormatVersion = FormatVersion,
            App = AppName,
            CreatedAt = DateTime.Now,
            Counts = payload.Counts()
        };

        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            await WriteEntryAsync(zip, "manifest.json", JsonSerializer.Serialize(manifest, JsonOptions), cancellationToken);
            await WriteEntryAsync(zip, "data.json", JsonSerializer.Serialize(payload, JsonOptions), cancellationToken);
            await AddUploadsAsync(zip, cancellationToken);
        }

        return output.ToArray();
    }

    public async Task<string?> ImportAsync(Stream zipStream, CancellationToken cancellationToken = default)
    {
        if (zipStream.CanSeek && zipStream.Length > MaxImportBytes)
        {
            return "حجم ملف النسخة أكبر من الحد المسموح.";
        }

        ZipArchive zip;
        try
        {
            zip = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: true);
        }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentException or IOException)
        {
            return InvalidFileMessage;
        }

        using (zip)
        {
            var manifestEntry = zip.GetEntry("manifest.json");
            var dataEntry = zip.GetEntry("data.json");
            if (manifestEntry is null || dataEntry is null)
            {
                return InvalidFileMessage;
            }

            DatabaseBackupManifest? manifest;
            try
            {
                await using var manifestStream = manifestEntry.Open();
                manifest = await JsonSerializer.DeserializeAsync<DatabaseBackupManifest>(manifestStream, JsonOptions, cancellationToken);
            }
            catch (JsonException)
            {
                return InvalidFileMessage;
            }

            if (manifest is null)
            {
                return InvalidFileMessage;
            }

            if (manifest.FormatVersion != FormatVersion)
            {
                return UnsupportedVersionMessage;
            }

            if (!string.Equals(manifest.App, AppName, StringComparison.Ordinal))
            {
                return InvalidFileMessage;
            }

            DatabaseBackupPayload? payload;
            try
            {
                await using var dataStream = dataEntry.Open();
                payload = await JsonSerializer.DeserializeAsync<DatabaseBackupPayload>(dataStream, JsonOptions, cancellationToken);
            }
            catch (JsonException)
            {
                return InvalidFileMessage;
            }

            if (payload is null || !payload.IsComplete())
            {
                return IncompleteFileMessage;
            }

            StripNavigations(payload);
            EnsureProductRowVersions(payload);

            Dictionary<string, byte[]> uploads;
            try
            {
                uploads = await ReadUploadsAsync(zip, cancellationToken);
            }
            catch (InvalidOperationException)
            {
                return InvalidFileMessage;
            }

            await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
            await WipeAsync(keepUserId: null, cancellationToken);
            await InsertPayloadAsync(payload, cancellationToken);
            await tx.CommitAsync(cancellationToken);

            ReplaceUploads(uploads);
            OwnerAccount.Ensure(_db);
            return null;
        }
    }

    public async Task<string?> ResetAsync(string currentUsername, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(currentUsername))
        {
            return MissingAdminMessage;
        }

        var admin = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username == currentUsername, cancellationToken);
        if (admin is null)
        {
            return MissingAdminMessage;
        }

        var snapshot = new User
        {
            Id = admin.Id,
            Username = admin.Username,
            DisplayName = admin.DisplayName,
            Phone = admin.Phone,
            Password = admin.Password,
            Role = admin.Role,
            IsActive = admin.IsActive,
            CanProcessReturn = admin.CanProcessReturn,
            CanManageProducts = admin.CanManageProducts,
            CanViewAllOrders = admin.CanViewAllOrders,
            CanManageExpenses = admin.CanManageExpenses,
            CanManagePurchases = admin.CanManagePurchases,
            CanViewDashboard = admin.CanViewDashboard
        };

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        await WipeAsync(snapshot.Id, cancellationToken);

        if (!await _db.Users.AnyAsync(u => u.Id == snapshot.Id, cancellationToken))
        {
            _db.Users.Add(snapshot);
            await _db.SaveChangesAsync(cancellationToken);
            _db.ChangeTracker.Clear();
        }

        await RestoreDefaultSettingsAsync(cancellationToken);
        await ReseedIdentitiesAsync(snapshot.Id, cancellationToken);
        await tx.CommitAsync(cancellationToken);

        ReplaceUploads(new Dictionary<string, byte[]>());
        OwnerAccount.Ensure(_db);
        return null;
    }

    private async Task<DatabaseBackupPayload> LoadPayloadAsync(CancellationToken cancellationToken)
    {
        return new DatabaseBackupPayload
        {
            Users = await OwnerAccount.Visible(_db.Users).AsNoTracking().ToListAsync(cancellationToken),
            StoreSettings = await _db.StoreSettings.AsNoTracking().ToListAsync(cancellationToken),
            Products = await _db.Products.AsNoTracking().ToListAsync(cancellationToken),
            Suppliers = await _db.Suppliers.AsNoTracking().ToListAsync(cancellationToken),
            Customers = await _db.Customers.AsNoTracking().ToListAsync(cancellationToken),
            Shifts = await _db.Shifts.AsNoTracking().ToListAsync(cancellationToken),
            Orders = await _db.Orders.AsNoTracking().ToListAsync(cancellationToken),
            OrderDetails = await _db.OrderDetails.AsNoTracking().ToListAsync(cancellationToken),
            CreditInvoices = await _db.CreditInvoices.AsNoTracking().ToListAsync(cancellationToken),
            CreditInvoiceDetails = await _db.CreditInvoiceDetails.AsNoTracking().ToListAsync(cancellationToken),
            CreditPayments = await _db.CreditPayments.AsNoTracking().ToListAsync(cancellationToken),
            PurchaseInvoices = await _db.PurchaseInvoices.AsNoTracking().ToListAsync(cancellationToken),
            PurchaseDetails = await _db.PurchaseDetails.AsNoTracking().ToListAsync(cancellationToken),
            PurchaseReturns = await _db.PurchaseReturns.AsNoTracking().ToListAsync(cancellationToken),
            PurchaseReturnDetails = await _db.PurchaseReturnDetails.AsNoTracking().ToListAsync(cancellationToken),
            SupplierPayments = await _db.SupplierPayments.AsNoTracking().ToListAsync(cancellationToken),
            Expenses = await _db.Expenses.AsNoTracking().ToListAsync(cancellationToken),
            SalesReturns = await _db.SalesReturns.AsNoTracking().ToListAsync(cancellationToken),
            StockLedgers = await _db.StockLedgers.AsNoTracking().ToListAsync(cancellationToken),
            HeldSales = await _db.HeldSales.AsNoTracking().ToListAsync(cancellationToken)
        };
    }

    private async Task WipeAsync(int? keepUserId, CancellationToken cancellationToken)
    {
        await _db.StockLedgers.ExecuteDeleteAsync(cancellationToken);
        await _db.HeldSales.ExecuteDeleteAsync(cancellationToken);
        await _db.SalesReturns.ExecuteDeleteAsync(cancellationToken);
        await _db.CreditPayments.ExecuteDeleteAsync(cancellationToken);
        await _db.CreditInvoiceDetails.ExecuteDeleteAsync(cancellationToken);
        await _db.CreditInvoices.ExecuteDeleteAsync(cancellationToken);
        await _db.OrderDetails.ExecuteDeleteAsync(cancellationToken);
        await _db.Orders.ExecuteDeleteAsync(cancellationToken);
        await _db.PurchaseReturnDetails.ExecuteDeleteAsync(cancellationToken);
        await _db.PurchaseReturns.ExecuteDeleteAsync(cancellationToken);
        await _db.PurchaseDetails.ExecuteDeleteAsync(cancellationToken);
        await _db.SupplierPayments.ExecuteDeleteAsync(cancellationToken);
        await _db.PurchaseInvoices.ExecuteDeleteAsync(cancellationToken);
        await _db.Expenses.ExecuteDeleteAsync(cancellationToken);
        await _db.Shifts.ExecuteDeleteAsync(cancellationToken);
        await _db.Products.ExecuteDeleteAsync(cancellationToken);
        await _db.Suppliers.ExecuteDeleteAsync(cancellationToken);
        await _db.Customers.ExecuteDeleteAsync(cancellationToken);

        if (keepUserId is int id)
        {
            await _db.Users.Where(u => u.Id != id).ExecuteDeleteAsync(cancellationToken);
        }
        else
        {
            await _db.Users.ExecuteDeleteAsync(cancellationToken);
            await _db.StoreSettings.ExecuteDeleteAsync(cancellationToken);
        }

        _db.ChangeTracker.Clear();
    }

    private async Task InsertPayloadAsync(DatabaseBackupPayload payload, CancellationToken cancellationToken)
    {
        await InsertWithIdsAsync(payload.Users, "Users", cancellationToken);
        await InsertWithIdsAsync(payload.StoreSettings, "StoreSettings", cancellationToken, identity: false);
        await InsertWithIdsAsync(payload.Products, "Products", cancellationToken);
        await InsertWithIdsAsync(payload.Suppliers, "Suppliers", cancellationToken);
        await InsertWithIdsAsync(payload.Customers, "Customers", cancellationToken);
        await InsertWithIdsAsync(payload.Shifts, "Shifts", cancellationToken);
        await InsertWithIdsAsync(payload.Orders, "Orders", cancellationToken);
        await InsertWithIdsAsync(payload.OrderDetails, "OrderDetails", cancellationToken);
        await InsertWithIdsAsync(payload.CreditInvoices, "CreditInvoices", cancellationToken);
        await InsertWithIdsAsync(payload.CreditInvoiceDetails, "CreditInvoiceDetails", cancellationToken);
        await InsertWithIdsAsync(payload.CreditPayments, "CreditPayments", cancellationToken);
        await InsertWithIdsAsync(payload.PurchaseInvoices, "PurchaseInvoices", cancellationToken);
        await InsertWithIdsAsync(payload.PurchaseDetails, "PurchaseDetails", cancellationToken);
        await InsertWithIdsAsync(payload.PurchaseReturns ?? [], "PurchaseReturns", cancellationToken);
        await InsertWithIdsAsync(payload.PurchaseReturnDetails ?? [], "PurchaseReturnDetails", cancellationToken);
        await InsertWithIdsAsync(payload.SupplierPayments, "SupplierPayments", cancellationToken);
        await InsertWithIdsAsync(payload.Expenses, "Expenses", cancellationToken);
        await InsertWithIdsAsync(payload.SalesReturns, "SalesReturns", cancellationToken);
        await InsertWithIdsAsync(payload.StockLedgers, "StockLedgers", cancellationToken);
        await InsertWithIdsAsync(payload.HeldSales ?? [], "HeldSales", cancellationToken);
    }

    private async Task InsertWithIdsAsync<T>(
        IReadOnlyCollection<T> rows,
        string table,
        CancellationToken cancellationToken,
        bool identity = true)
        where T : class
    {
        if (rows.Count == 0)
        {
            return;
        }

        var sqlServerIdentity = identity && _db.Database.IsSqlServer();
        if (sqlServerIdentity)
        {
#pragma warning disable EF1002
            await _db.Database.ExecuteSqlRawAsync($"SET IDENTITY_INSERT [{table}] ON", cancellationToken);
#pragma warning restore EF1002
        }

        try
        {
            _db.Set<T>().AddRange(rows);
            await _db.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            _db.ChangeTracker.Clear();
            if (sqlServerIdentity)
            {
#pragma warning disable EF1002
                await _db.Database.ExecuteSqlRawAsync($"SET IDENTITY_INSERT [{table}] OFF", cancellationToken);
#pragma warning restore EF1002
            }
        }
    }

    private async Task RestoreDefaultSettingsAsync(CancellationToken cancellationToken)
    {
        var settings = await _db.StoreSettings.FindAsync([StoreSettingsService.SingletonId], cancellationToken);
        if (settings is null)
        {
            settings = StoreSettingsService.CreateDefault();
            _db.StoreSettings.Add(settings);
        }
        else
        {
            settings.StoreName = StoreSettingsService.DefaultStoreName;
            settings.InvoiceFooter = StoreSettingsService.DefaultInvoiceFooter;
            settings.Phone = null;
            settings.Address = null;
            settings.LogoPath = null;
            settings.TaxEnabled = true;
            settings.DefaultTaxRate = 0m;
            settings.PrintReceiptAfterSale = false;
            settings.EnablePaymentCash = true;
            settings.EnablePaymentCard = true;
            settings.EnablePaymentTransfer = true;
            settings.FactoryResetPinHash = null;
        }

        await _db.SaveChangesAsync(cancellationToken);
        _db.ChangeTracker.Clear();
    }

    private async Task ReseedIdentitiesAsync(int remainingUserId, CancellationToken cancellationToken)
    {
        if (_db.Database.IsSqlServer())
        {
            string[] tables =
            [
                "Products", "Suppliers", "Customers", "Shifts", "Orders", "OrderDetails",
                "CreditInvoices", "CreditInvoiceDetails", "CreditPayments",
                "PurchaseInvoices", "PurchaseDetails", "PurchaseReturns", "PurchaseReturnDetails", "SupplierPayments",
                "Expenses", "SalesReturns", "StockLedgers", "HeldSales"
            ];
            foreach (var table in tables)
            {
#pragma warning disable EF1002
                await _db.Database.ExecuteSqlRawAsync($"DBCC CHECKIDENT ('{table}', RESEED, 0)", cancellationToken);
#pragma warning restore EF1002
            }

#pragma warning disable EF1002
            await _db.Database.ExecuteSqlRawAsync($"DBCC CHECKIDENT ('Users', RESEED, {remainingUserId})", cancellationToken);
#pragma warning restore EF1002
        }
        else
        {
            try
            {
                await _db.Database.ExecuteSqlRawAsync(
                    "DELETE FROM sqlite_sequence WHERE name <> 'Users'", cancellationToken);
                await _db.Database.ExecuteSqlRawAsync(
                    "UPDATE sqlite_sequence SET seq = (SELECT IFNULL(MAX(Id), 0) FROM Users) WHERE name = 'Users'",
                    cancellationToken);
            }
            catch (Exception)
            {
                // sqlite_sequence exists only when AUTOINCREMENT is used.
            }
        }
    }

    private async Task AddUploadsAsync(ZipArchive zip, CancellationToken cancellationToken)
    {
        var root = UploadsRoot;
        if (root is null || !Directory.Exists(root))
        {
            return;
        }

        foreach (var file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            var entry = zip.CreateEntry("uploads/" + relative);
            await using var source = File.OpenRead(file);
            await using var target = entry.Open();
            await source.CopyToAsync(target, cancellationToken);
        }
    }

    private static async Task<Dictionary<string, byte[]>> ReadUploadsAsync(ZipArchive zip, CancellationToken cancellationToken)
    {
        var files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in zip.Entries)
        {
            var name = entry.FullName.Replace('\\', '/');
            if (!name.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase) || name.EndsWith('/'))
            {
                continue;
            }

            if (name.Contains("..", StringComparison.Ordinal))
            {
                throw new InvalidOperationException();
            }

            await using var stream = entry.Open();
            using var copy = new MemoryStream();
            await stream.CopyToAsync(copy, cancellationToken);
            files[name["uploads/".Length..]] = copy.ToArray();
        }

        return files;
    }

    private void ReplaceUploads(Dictionary<string, byte[]> files)
    {
        var root = UploadsRoot;
        if (root is null)
        {
            return;
        }

        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }

        if (files.Count == 0)
        {
            return;
        }

        Directory.CreateDirectory(root);
        foreach (var (relative, bytes) in files)
        {
            var physical = Path.GetFullPath(Path.Combine(root, relative));
            if (!physical.StartsWith(Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var dir = Path.GetDirectoryName(physical);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllBytes(physical, bytes);
        }
    }

    private string? UploadsRoot =>
        string.IsNullOrWhiteSpace(_env?.WebRootPath)
            ? null
            : Path.Combine(_env.WebRootPath, "uploads");

    private static async Task WriteEntryAsync(ZipArchive zip, string name, string content, CancellationToken cancellationToken)
    {
        var entry = zip.CreateEntry(name);
        await using var stream = entry.Open();
        await using var writer = new StreamWriter(stream);
        await writer.WriteAsync(content.AsMemory(), cancellationToken);
    }

    private static void StripNavigations(DatabaseBackupPayload payload)
    {
        foreach (var order in payload.Orders)
        {
            order.OrderDetails = [];
            order.Shift = null;
        }

        foreach (var detail in payload.OrderDetails)
        {
            detail.Order = null;
            detail.Product = null;
        }

        foreach (var invoice in payload.PurchaseInvoices)
        {
            invoice.Details = [];
            invoice.Supplier = null!;
        }

        foreach (var detail in payload.PurchaseDetails)
        {
            detail.PurchaseInvoice = null!;
            detail.Product = null!;
        }

        foreach (var supplier in payload.Suppliers)
        {
            supplier.Invoices = [];
            supplier.Payments = [];
            supplier.Returns = [];
            supplier.PurchaseInvoices = [];
        }

        foreach (var payment in payload.SupplierPayments)
        {
            payment.Supplier = null!;
            payment.Shift = null;
        }

        foreach (var returnedPurchase in payload.PurchaseReturns ?? [])
        {
            returnedPurchase.Supplier = null;
            returnedPurchase.PurchaseInvoice = null;
            returnedPurchase.Shift = null;
            returnedPurchase.Details = [];
        }

        foreach (var detail in payload.PurchaseReturnDetails ?? [])
        {
            detail.PurchaseReturn = null;
            detail.PurchaseDetail = null;
            detail.Product = null;
        }

        foreach (var shift in payload.Shifts)
        {
            shift.User = null!;
        }

        foreach (var returned in payload.SalesReturns)
        {
            returned.Order = null!;
            returned.Product = null!;
            returned.Shift = null;
        }

        foreach (var ledger in payload.StockLedgers)
        {
            ledger.Product = null!;
            ledger.Shift = null;
        }
    }

    private static void EnsureProductRowVersions(DatabaseBackupPayload payload)
    {
        foreach (var product in payload.Products)
        {
            if (product.RowVersion is not { Length: > 0 })
            {
                product.RowVersion = [1];
            }
        }
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver
            {
                Modifiers = { IgnoreNavigations }
            }
        };
    }

    private static void IgnoreNavigations(JsonTypeInfo info)
    {
        if (info.Kind != JsonTypeInfoKind.Object
            || info.Type == typeof(DatabaseBackupPayload)
            || info.Type == typeof(DatabaseBackupManifest)
            || info.Type == typeof(Dictionary<string, int>))
        {
            return;
        }

        foreach (var prop in info.Properties)
        {
            var type = prop.PropertyType;
            if (type == typeof(byte[]) && prop.Name == nameof(Product.RowVersion))
            {
                prop.ShouldSerialize = static (_, _) => false;
                continue;
            }

            if (type == typeof(string) || type == typeof(byte[]))
            {
                continue;
            }

            var underlying = Nullable.GetUnderlyingType(type) ?? type;
            if (underlying.IsPrimitive
                || underlying.IsEnum
                || underlying == typeof(decimal)
                || underlying == typeof(DateTime)
                || underlying == typeof(DateTimeOffset)
                || underlying == typeof(Guid)
                || underlying == typeof(TimeSpan))
            {
                continue;
            }

            prop.ShouldSerialize = static (_, _) => false;
        }
    }
}

public sealed class DatabaseBackupManifest
{
    public int FormatVersion { get; set; }
    public string App { get; set; } = DatabaseBackupService.AppName;
    public DateTime CreatedAt { get; set; }
    public Dictionary<string, int> Counts { get; set; } = new();
}

public sealed class DatabaseBackupPayload
{
    public List<User> Users { get; set; } = [];
    public List<StoreSettings> StoreSettings { get; set; } = [];
    public List<Product> Products { get; set; } = [];
    public List<Supplier> Suppliers { get; set; } = [];
    public List<Customer> Customers { get; set; } = [];
    public List<Shift> Shifts { get; set; } = [];
    public List<Order> Orders { get; set; } = [];
    public List<OrderDetail> OrderDetails { get; set; } = [];
    public List<CreditInvoice> CreditInvoices { get; set; } = [];
    public List<CreditInvoiceDetail> CreditInvoiceDetails { get; set; } = [];
    public List<CreditPayment> CreditPayments { get; set; } = [];
    public List<PurchaseInvoice> PurchaseInvoices { get; set; } = [];
    public List<PurchaseDetail> PurchaseDetails { get; set; } = [];
    public List<PurchaseReturn> PurchaseReturns { get; set; } = [];
    public List<PurchaseReturnDetail> PurchaseReturnDetails { get; set; } = [];
    public List<SupplierPayment> SupplierPayments { get; set; } = [];
    public List<Expense> Expenses { get; set; } = [];
    public List<SalesReturn> SalesReturns { get; set; } = [];
    public List<StockLedger> StockLedgers { get; set; } = [];
    public List<HeldSale> HeldSales { get; set; } = [];

    public bool IsComplete() =>
        Users is not null
        && StoreSettings is not null
        && Products is not null
        && Suppliers is not null
        && Shifts is not null
        && Orders is not null
        && OrderDetails is not null
        && PurchaseInvoices is not null
        && PurchaseDetails is not null
        && SupplierPayments is not null
        && Expenses is not null
        && SalesReturns is not null
        && StockLedgers is not null;

    public Dictionary<string, int> Counts() => new()
    {
        [nameof(Users)] = Users.Count,
        [nameof(StoreSettings)] = StoreSettings.Count,
        [nameof(Products)] = Products.Count,
        [nameof(Suppliers)] = Suppliers.Count,
        [nameof(Customers)] = Customers.Count,
        [nameof(Shifts)] = Shifts.Count,
        [nameof(Orders)] = Orders.Count,
        [nameof(OrderDetails)] = OrderDetails.Count,
        [nameof(CreditInvoices)] = CreditInvoices.Count,
        [nameof(CreditInvoiceDetails)] = CreditInvoiceDetails.Count,
        [nameof(CreditPayments)] = CreditPayments.Count,
        [nameof(PurchaseInvoices)] = PurchaseInvoices.Count,
        [nameof(PurchaseDetails)] = PurchaseDetails.Count,
        [nameof(PurchaseReturns)] = PurchaseReturns.Count,
        [nameof(PurchaseReturnDetails)] = PurchaseReturnDetails.Count,
        [nameof(SupplierPayments)] = SupplierPayments.Count,
        [nameof(Expenses)] = Expenses.Count,
        [nameof(SalesReturns)] = SalesReturns.Count,
        [nameof(StockLedgers)] = StockLedgers.Count,
        [nameof(HeldSales)] = HeldSales.Count
    };
}
