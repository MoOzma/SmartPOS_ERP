using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SmartPOS_ERP.Controllers;
using SmartPOS_ERP.Data;
using SmartPOS_ERP.Models;
using SmartPOS_ERP.Services;
using SmartPOS_ERP.Security;

namespace SmartPOS_ERP.Tests;

public sealed class PosHarness : IAsyncDisposable
{
    private readonly SqliteConnection _connection;

    public ApplicationDbContext Db { get; }

    public PosHarness()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        Db = new ApplicationDbContext(options);
        Db.Database.EnsureCreated();
    }

    public Product SeedProduct(
        decimal salePrice,
        decimal costPrice,
        decimal stock,
        decimal taxRate = 0,
        bool trackInventory = true,
        string unit = "Piece")
    {
        var product = new Product
        {
            Name = "Test product",
            SalePrice = salePrice,
            CostPrice = costPrice,
            StockQuantity = stock,
            TaxRate = taxRate,
            UseCustomTax = taxRate > 0,
            TrackInventory = trackInventory,
            Unit = unit,
            ReorderLevel = 0,
            RowVersion = new byte[] { 1 }
        };

        Db.Products.Add(product);
        Db.SaveChanges();
        return product;
    }

    public ProductsController ProductsController(bool seedShift = true)
    {
        if (seedShift)
        {
            SeedOpenShift();
        }

        var controller = new ProductsController(Db, NullLogger<ProductsController>.Instance, Ledger(), StoreSettings());
        AttachUser(controller);
        return controller;
    }

    public Shift SeedOpenShift(string username = "cashier")
    {
        var user = Db.Users.SingleOrDefault(u => u.Username == username);
        if (user == null)
        {
            user = new User
            {
                Username = username,
                DisplayName = username,
                Password = "hashed",
                Role = "Admin",
                IsActive = true
            };
            Db.Users.Add(user);
            Db.SaveChanges();
        }

        var shift = Db.Shifts.SingleOrDefault(s => s.UserId == user.Id && s.ClosedAt == null);
        if (shift == null)
        {
            shift = new Shift { UserId = user.Id, OpenedAt = DateTime.Now, OpeningCash = 0m };
            Db.Shifts.Add(shift);
            Db.SaveChanges();
        }

        return shift;
    }

    public OrderController OrderController(string role = "Admin", params string[] permissions)
    {
        var controller = new OrderController(Db, NullLogger<OrderController>.Instance, Ledger());
        AttachUser(controller, role, permissions);
        return controller;
    }

    public PurchasesController PurchasesController(string role = "Admin")
    {
        var controller = new PurchasesController(
            Db,
            NullLogger<PurchasesController>.Instance,
            Ledger(),
            new SupplierDirectoryService(Db),
            new PurchaseInvoiceReportService(Db));
        AttachUser(controller, role);
        return controller;
    }

    public ExpensesController ExpensesController(string role = "Admin")
    {
        var controller = new ExpensesController(
            Db,
            NullLogger<ExpensesController>.Instance,
            new ExpenseReportService(Db));
        AttachUser(controller, role);
        return controller;
    }

        public DashboardService Dashboard() => new(Db, NullLogger<DashboardService>.Instance, new ShiftCashService(Db));

    public StoreSettingsService StoreSettings() => new(Db);

    public CreditInvoiceService CreditInvoices() => new(Db, Ledger(), StoreSettings());

    public DatabaseBackupService Backup() => new(Db);

    public UsersController UsersController(string role = "Admin")
    {
        var controller = new UsersController(Db, NullLogger<UsersController>.Instance);
        AttachUser(controller, role);
        return controller;
    }

    public SettingsController SettingsController(string role = "Admin")
    {
        var controller = new SettingsController(StoreSettings(), Backup());
        AttachUser(controller, role);
        return controller;
    }

    public CustomersController CustomersController(string role = "Admin")
    {
        var controller = new CustomersController(Db);
        AttachUser(controller, role);
        return controller;
    }

    private StockLedgerService Ledger() => new(Db, new HttpContextAccessor());

    private static void AttachUser(Controller controller, string role = "Admin", IEnumerable<string>? permissions = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, "cashier"),
            new(ClaimTypes.Role, role),
            new(ClaimTypes.NameIdentifier, "1")
        };

        if (permissions != null)
        {
            claims.AddRange(permissions.Select(p => new Claim(AppPermissions.ClaimType, p)));
        }

        var identity = new ClaimsIdentity(claims, authenticationType: "Test");
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity),
            Session = new StubSession()
        };

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
        controller.TempData = new TempDataDictionary(httpContext, new NullTempDataProvider());
    }

    private sealed class StubSession : ISession
    {
        private readonly Dictionary<string, byte[]> _store = new();
        public bool IsAvailable => true;
        public string Id => "test";
        public IEnumerable<string> Keys => _store.Keys;
        public void Clear() => _store.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => _store.Remove(key);
        public void Set(string key, byte[] value) => _store[key] = value;
        public bool TryGetValue(string key, out byte[] value) => _store.TryGetValue(key, out value!);
    }

    private sealed class NullTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    public async ValueTask DisposeAsync()
    {
        await Db.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
