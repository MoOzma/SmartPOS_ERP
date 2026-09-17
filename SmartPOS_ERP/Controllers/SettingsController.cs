using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartPOS_ERP.Models;
using SmartPOS_ERP.Security;
using SmartPOS_ERP.Services;

namespace SmartPOS_ERP.Controllers
{
    [Authorize(Roles = "Admin")]
    public class SettingsController : Controller
    {
        public const string PinRequiredMessage = "اضبط الرقم السري أولاً.";
        public const string WrongPinMessage = "الرقم السري غير صحيح.";
        public const string ConfirmRequiredMessage = "يجب تأكيد العملية.";
        public const string FileRequiredMessage = "اختر ملف النسخة الاحتياطية.";

        private readonly StoreSettingsService _storeSettings;
        private readonly DatabaseBackupService _backup;

        public SettingsController(StoreSettingsService storeSettings, DatabaseBackupService backup)
        {
            _storeSettings = storeSettings;
            _backup = backup;
        }

        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            return View(await PageAsync(cancellationToken));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(3_000_000)]
        public async Task<IActionResult> Index(
            SettingsPageViewModel page,
            IFormFile? logoFile,
            bool removeLogo,
            CancellationToken cancellationToken)
        {
            var model = page.Store ?? new StoreSettings();
            page.Store = model;
            if (string.IsNullOrWhiteSpace(model.StoreName))
            {
                ModelState.AddModelError($"{nameof(SettingsPageViewModel.Store)}.{nameof(model.StoreName)}", "اسم المحل مطلوب");
            }

            if (model.DefaultTaxRate < 0 || model.DefaultTaxRate > 100)
            {
                ModelState.AddModelError($"{nameof(SettingsPageViewModel.Store)}.{nameof(model.DefaultTaxRate)}", InputRules.TaxPercentRange);
            }

            if (!ModelState.IsValid)
            {
                var current = await _storeSettings.GetAsync(cancellationToken);
                model.LogoPath = current.LogoPath;
                page.HasResetPin = !string.IsNullOrEmpty(current.FactoryResetPinHash);
                return View(page);
            }

            var error = await _storeSettings.SaveAsync(model, logoFile, removeLogo, cancellationToken);
            if (error != null)
            {
                ModelState.AddModelError(string.Empty, error);
                var current = await _storeSettings.GetAsync(cancellationToken);
                model.LogoPath = current.LogoPath;
                page.HasResetPin = !string.IsNullOrEmpty(current.FactoryResetPinHash);
                return View(page);
            }

            TempData["Message"] = "تم حفظ الإعدادات.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetResetPin(
            string? currentPin,
            string? newPin,
            string? confirmPin,
            CancellationToken cancellationToken = default)
        {
            var settings = await _storeSettings.GetAsync(cancellationToken);
            var error = ResetPinRules.Apply(settings, currentPin, newPin, confirmPin);
            if (error != null)
            {
                TempData["Error"] = error;
                return RedirectToAction(nameof(Index));
            }

            await _storeSettings.SaveAsync(settings, null, false, cancellationToken);
            TempData["Message"] = "تم ضبط الرقم السري.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> ExportBackup(CancellationToken cancellationToken = default)
        {
            var bytes = await _backup.ExportAsync(cancellationToken);
            var name = $"sama-pos-backup-{DateTime.Now:yyyy-MM-dd-HHmm}.zip";
            return File(bytes, "application/zip", name);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(DatabaseBackupService.MaxImportBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = DatabaseBackupService.MaxImportBytes)]
        public async Task<IActionResult> ImportBackup(
            IFormFile? backupFile,
            string? pin,
            bool confirm,
            CancellationToken cancellationToken = default)
        {
            var settings = await _storeSettings.GetAsync(cancellationToken);
            if (string.IsNullOrEmpty(settings.FactoryResetPinHash))
            {
                TempData["Error"] = PinRequiredMessage;
                return RedirectToAction(nameof(Index));
            }

            if (!confirm)
            {
                TempData["Error"] = ConfirmRequiredMessage;
                return RedirectToAction(nameof(Index));
            }

            if (!ResetPinRules.Matches(pin, settings.FactoryResetPinHash))
            {
                TempData["Error"] = WrongPinMessage;
                return RedirectToAction(nameof(Index));
            }

            if (backupFile is null || backupFile.Length == 0)
            {
                TempData["Error"] = FileRequiredMessage;
                return RedirectToAction(nameof(Index));
            }

            await using var stream = backupFile.OpenReadStream();
            var error = await _backup.ImportAsync(stream, cancellationToken);
            if (error != null)
            {
                TempData["Error"] = error;
                return RedirectToAction(nameof(Index));
            }

            var username = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(username) || !await _backup.UserExistsAsync(username, cancellationToken))
            {
                HttpContext.Session.Clear();
                return RedirectToAction("Login", "Account");
            }

            TempData["Message"] = "تم استيراد النسخة الاحتياطية.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FactoryReset(
            string? pin,
            bool confirm,
            CancellationToken cancellationToken = default)
        {
            var settings = await _storeSettings.GetAsync(cancellationToken);
            if (string.IsNullOrEmpty(settings.FactoryResetPinHash))
            {
                TempData["Error"] = PinRequiredMessage;
                return RedirectToAction(nameof(Index));
            }

            if (!confirm)
            {
                TempData["Error"] = ConfirmRequiredMessage;
                return RedirectToAction(nameof(Index));
            }

            if (!ResetPinRules.Matches(pin, settings.FactoryResetPinHash))
            {
                TempData["Error"] = WrongPinMessage;
                return RedirectToAction(nameof(Index));
            }

            var username = User.Identity?.Name;
            var error = await _backup.ResetAsync(username ?? string.Empty, cancellationToken);
            if (error != null)
            {
                TempData["Error"] = error;
                return RedirectToAction(nameof(Index));
            }

            TempData["Message"] = "تم تصفير النظام.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<SettingsPageViewModel> PageAsync(CancellationToken cancellationToken)
        {
            var settings = await _storeSettings.GetAsync(cancellationToken);
            return new SettingsPageViewModel
            {
                Store = settings,
                HasResetPin = !string.IsNullOrEmpty(settings.FactoryResetPinHash)
            };
        }
    }
}
