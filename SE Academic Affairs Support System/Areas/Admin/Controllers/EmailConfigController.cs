using System.Net;
using System.Net.Mail;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using SE_Academic_Affairs_Support_System.Services.EmailConfig;
using SE_Academic_Affairs_Support_System.ViewModels;

namespace SE_Academic_Affairs_Support_System.Areas.Admin.Controllers
{
    [Authorize(Roles = "Admin")]
    [Area("Admin")]
    [Route("Admin/EmailConfig/[action]/{id?}")]
    public class EmailConfigController : Controller
    {
        private readonly IEmailConfigurationService _svc;
        private readonly IDataProtector _protector;
        private readonly ILogger<EmailConfigController> _logger;

        public EmailConfigController(
            IEmailConfigurationService svc,
            IDataProtectionProvider dpProvider,
            ILogger<EmailConfigController> logger)
        {
            _svc = svc;
            _protector = dpProvider.CreateProtector("EmailConfig.AppPassword");
            _logger = logger;
        }

        // GET /Admin/EmailConfig/Index
        public async Task<IActionResult> Index()
        {
            var list = await _svc.GetAllAsync();
            return View(list);
        }

        // GET /Admin/EmailConfig/Create
        public IActionResult Create() => View("CreateEdit", new EmailConfigFormViewModel());

        // POST /Admin/EmailConfig/Create
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(EmailConfigFormViewModel vm)
        {
            if (!ModelState.IsValid) return View("CreateEdit", vm);

            if (string.IsNullOrWhiteSpace(vm.AppPassword))
            {
                ModelState.AddModelError(nameof(vm.AppPassword), "App Password là bắt buộc khi tạo mới.");
                return View("CreateEdit", vm);
            }

            var (ok, msg) = await _svc.CreateAsync(vm);
            if (!ok)
            {
                ModelState.AddModelError(string.Empty, msg);
                return View("CreateEdit", vm);
            }

            TempData["Success"] = msg;
            return RedirectToAction(nameof(Index));
        }

        // GET /Admin/EmailConfig/Edit/{id}
        public async Task<IActionResult> Edit(int id)
        {
            var vm = await _svc.GetForEditAsync(id);
            if (vm == null) return NotFound();
            return View("CreateEdit", vm);
        }

        // POST /Admin/EmailConfig/Edit
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EmailConfigFormViewModel vm)
        {
            // AppPassword không bắt buộc khi edit (để trống = giữ nguyên)
            ModelState.Remove(nameof(vm.AppPassword));

            if (!ModelState.IsValid) return View("CreateEdit", vm);

            var (ok, msg) = await _svc.UpdateAsync(vm);
            if (!ok)
            {
                ModelState.AddModelError(string.Empty, msg);
                return View("CreateEdit", vm);
            }

            TempData["Success"] = msg;
            return RedirectToAction(nameof(Index));
        }

        // POST /Admin/EmailConfig/Delete
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var (ok, msg) = await _svc.DeleteAsync(id);
            if (ok) TempData["Success"] = msg;
            else TempData["Error"] = msg;
            return RedirectToAction(nameof(Index));
        }

        // POST /Admin/EmailConfig/SetActive
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SetActive(int id)
        {
            var (ok, msg) = await _svc.SetActiveAsync(id);
            if (ok) TempData["Success"] = msg;
            else TempData["Error"] = msg;
            return RedirectToAction(nameof(Index));
        }

        // GET /Admin/EmailConfig/TestSend/{id}
        public async Task<IActionResult> TestSend(int id)
        {
            var config = await _svc.GetForEditAsync(id);
            if (config == null) return NotFound();

            var vm = new EmailConfigTestViewModel { ConfigId = id };
            ViewBag.ConfigName = config.DisplayName;
            return View(vm);
        }

        // POST /Admin/EmailConfig/TestSend
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> TestSend(EmailConfigTestViewModel vm)
        {
            var config = await _svc.GetForEditAsync(vm.ConfigId);
            if (config == null) return NotFound();
            ViewBag.ConfigName = config.DisplayName;

            if (!ModelState.IsValid) return View(vm);

            // Fetch the actual entity to decrypt password
            var entity = await _svc.GetActiveEntityByIdAsync(vm.ConfigId);
            if (entity == null)
            {
                TempData["Error"] = "Không tìm thấy cấu hình.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var password = _protector.Unprotect(entity.EncryptedAppPassword);
                using var smtp = new SmtpClient(entity.SmtpHost, entity.SmtpPort)
                {
                    Credentials = new NetworkCredential(entity.SenderEmail, password),
                    EnableSsl = entity.EnableSsl
                };

                var mail = new MailMessage
                {
                    From = new MailAddress(entity.SenderEmail, entity.SenderName),
                    Subject = "[Test] Kiểm tra cấu hình email - Học vụ UIT",
                    Body = $"<p>Email test từ cấu hình <strong>{entity.DisplayName}</strong>.</p><p>Nếu bạn nhận được email này, cấu hình SMTP đang hoạt động đúng.</p>",
                    IsBodyHtml = true
                };
                mail.To.Add(vm.TestEmail);

                await smtp.SendMailAsync(mail);
                TempData["Success"] = $"Gửi email thử thành công tới {vm.TestEmail}.";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Test email failed for config {Id}", vm.ConfigId);
                TempData["Error"] = $"Gửi thất bại: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
