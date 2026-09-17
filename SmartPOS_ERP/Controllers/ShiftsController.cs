using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPOS_ERP.Data;
using SmartPOS_ERP.Models;
using SmartPOS_ERP.Services;

namespace SmartPOS_ERP.Controllers
{
    [Authorize]
    public class ShiftsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ShiftsController> _logger;
        private readonly ShiftCashService _shiftCash;

        public ShiftsController(ApplicationDbContext context, ILogger<ShiftsController> logger, ShiftCashService shiftCash)
        {
            _context = context;
            _logger = logger;
            _shiftCash = shiftCash;
        }

        public async Task<IActionResult> Index()
        {
            var shiftsQuery = VisibleShifts().OrderByDescending(s => s.OpenedAt);
            var shifts = await shiftsQuery.ToListAsync();
            ViewBag.OpenShift = shifts.FirstOrDefault(s => s.IsOpen && s.User.Username == User.Identity!.Name);
            return View(shifts);
        }

        public async Task<IActionResult> Details(int id)
        {
            var shift = await VisibleShifts().FirstOrDefaultAsync(s => s.Id == id);
            if (shift == null)
            {
                return NotFound();
            }

            var orders = await _context.Orders
                .Where(o => o.ShiftId == id)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            var returns = await _context.SalesReturns
                .Include(r => r.Product)
                .Where(r => r.ShiftId == id)
                .OrderByDescending(r => r.ReturnDate)
                .ToListAsync();

            var stockMovements = await _context.StockLedgers
                .Include(l => l.Product)
                .Where(l => l.ShiftId == id)
                .OrderByDescending(l => l.OccurredAt)
                .ToListAsync();

            var model = new ShiftDetailsViewModel
            {
                Shift = shift,
                Orders = orders,
                Returns = returns,
                StockMovements = stockMovements,
                SalesTotal = orders.Sum(o => o.TotalAmount),
                ReturnsTotal = returns.Sum(r => r.RefundAmount)
            };

            return View(model);
        }

        public async Task<IActionResult> Open()
        {
            if (await CurrentUserOpenShiftAsync() != null)
            {
                TempData["Error"] = "لديك وردية مفتوحة بالفعل.";
                return RedirectToAction(nameof(Index));
            }

            return View(new Shift { OpeningCash = 0 });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Open([Bind("OpeningCash,Notes")] Shift input)
        {
            var user = await CurrentUserAsync();
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            if (await CurrentUserOpenShiftAsync() != null)
            {
                TempData["Error"] = "لديك وردية مفتوحة بالفعل.";
                return RedirectToAction(nameof(Index));
            }

            if (input.OpeningCash < 0)
            {
                ModelState.AddModelError(nameof(input.OpeningCash), "عهدة أول المدة لا يمكن أن تكون سالبة.");
            }

            ModelState.Remove(nameof(Shift.User));
            ModelState.Remove(nameof(Shift.UserId));
            if (!ModelState.IsValid)
            {
                return View(input);
            }

            var shift = new Shift
            {
                UserId = user.Id,
                OpenedAt = DateTime.Now,
                OpeningCash = input.OpeningCash,
                Notes = input.Notes
            };

            _context.Shifts.Add(shift);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                _logger.LogWarning(ex, "Failed to open shift for user {UserId}", user.Id);
                ModelState.AddModelError(string.Empty, "لديك وردية مفتوحة بالفعل.");
                return View(input);
            }

            _logger.LogInformation("Shift {ShiftId} opened by {Username}", shift.Id, user.Username);
            TempData["Message"] = "تم فتح الوردية.";
            return RedirectToAction(nameof(Details), new { id = shift.Id });
        }

        public async Task<IActionResult> Close()
        {
            var shift = await CurrentUserOpenShiftAsync();
            if (shift == null)
            {
                TempData["Error"] = "لا توجد وردية مفتوحة لإقفالها.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.ExpectedCash = await _shiftCash.ExpectedAsync(shift.Id, shift.OpeningCash);
            return View(shift);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Close(int id, decimal closingCash, string? notes)
        {
            var shift = await CurrentUserOpenShiftAsync();
            if (shift == null || shift.Id != id)
            {
                TempData["Error"] = "لا يمكن إقفال هذه الوردية.";
                return RedirectToAction(nameof(Index));
            }

            if (closingCash < 0)
            {
                ModelState.AddModelError(nameof(closingCash), "العدد الفعلي لا يمكن أن يكون سالباً.");
                ViewBag.ExpectedCash = await _shiftCash.ExpectedAsync(shift.Id, shift.OpeningCash);
                shift.ClosingCash = closingCash;
                shift.Notes = notes;
                return View(shift);
            }

            var expected = await _shiftCash.ExpectedAsync(shift.Id, shift.OpeningCash);
            shift.ClosedAt = DateTime.Now;
            shift.ClosingCash = closingCash;
            shift.ExpectedCash = expected;
            shift.Difference = closingCash - expected;
            if (!string.IsNullOrWhiteSpace(notes))
            {
                shift.Notes = notes;
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Shift {ShiftId} closed by {Username}", shift.Id, User.Identity?.Name);
            TempData["Message"] = "تم إقفال الوردية.";
            return RedirectToAction(nameof(Details), new { id = shift.Id });
        }

        private bool IsAdmin => User.IsInRole("Admin");

        private IQueryable<Shift> VisibleShifts()
        {
            var query = _context.Shifts.Include(s => s.User).AsQueryable();
            if (IsAdmin)
            {
                return query;
            }

            var userName = User.Identity!.Name;
            return query.Where(s => s.User.Username == userName);
        }

        private Task<User?> CurrentUserAsync()
        {
            var userName = User.Identity?.Name;
            if (string.IsNullOrEmpty(userName))
            {
                return Task.FromResult<User?>(null);
            }

            return _context.Users.FirstOrDefaultAsync(u => u.Username == userName);
        }

        private async Task<Shift?> CurrentUserOpenShiftAsync()
        {
            var userName = User.Identity?.Name;
            if (string.IsNullOrEmpty(userName))
            {
                return null;
            }

            return await _context.Shifts
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.ClosedAt == null && s.User.Username == userName);
        }
    }
}
