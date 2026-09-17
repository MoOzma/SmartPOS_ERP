using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPOS_ERP.Data;
using SmartPOS_ERP.Models;
using SmartPOS_ERP.Security;
using SmartPOS_ERP.Services;

namespace SmartPOS_ERP.Controllers
{
    [Authorize(Policy = AppPermissions.Expenses)]
    public class ExpensesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ExpensesController> _logger;
        private readonly ExpenseReportService _reports;

        public ExpensesController(ApplicationDbContext context, ILogger<ExpensesController> logger, ExpenseReportService reports)
        {
            _context = context;
            _logger = logger;
            _reports = reports;
        }

        public async Task<IActionResult> Index(
            string? period,
            DateTime? selectedDate,
            int? selectedMonth,
            int? selectedYear,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            CancellationToken cancellationToken = default)
        {
            var model = await _reports.BuildAsync(
                period,
                selectedDate,
                selectedMonth,
                selectedYear,
                DateTime.Now,
                fromDate,
                toDate,
                cancellationToken);
            return View(model);
        }

        public IActionResult Create()
        {
            return View(new Expense
            {
                ExpenseDate = DateTime.Today,
                FromCash = true
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Description,Amount,ExpenseDate,Category,FromCash")] Expense expense)
        {
            if (string.IsNullOrWhiteSpace(expense.Description))
            {
                ModelState.AddModelError(nameof(expense.Description), InputRules.DescriptionRequired);
            }

            if (expense.Amount <= 0)
            {
                ModelState.AddModelError(nameof(expense.Amount), InputRules.AmountMustBePositive);
            }

            if (expense.FromCash)
            {
                var shiftId = await _context.GetOpenShiftIdAsync(User.Identity?.Name);
                if (shiftId is null)
                {
                    TempData["Error"] = "يجب فتح وردية أولاً لتسجيل مصروف من الصندوق، أو ألغِ خيار الصندوق.";
                    return RedirectToAction("Open", "Shifts");
                }

                expense.ShiftId = shiftId;
            }
            else
            {
                expense.ShiftId = null;
            }

            if (ModelState.IsValid)
            {
                _context.Add(expense);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Expense {ExpenseId} created for {Amount}", expense.Id, expense.Amount);
                return RedirectToAction(nameof(Index));
            }
            return View(expense);
        }

        // حذف مصروف
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var expense = await _context.Expenses.FindAsync(id);
            if (expense != null)
            {
                _context.Expenses.Remove(expense);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Expense {ExpenseId} deleted", id);
            }
            return RedirectToAction(nameof(Index));
        }
    }
}