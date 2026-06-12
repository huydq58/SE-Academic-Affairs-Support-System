using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SE_Academic_Affairs_Support_System.Services.AccountManagement;
using SE_Academic_Affairs_Support_System.ViewModels;

namespace SE_Academic_Affairs_Support_System.Areas.Admin.Controllers
{
    [Authorize(Roles = "Admin")]
    [Area("Admin")]
    [Route("Admin/Account/[action]/{id?}")]
    public class AccountController : Controller
    {
        private readonly IAccountService _svc;

        public AccountController(IAccountService svc)
        {
            _svc = svc;
        }

        // GET /Admin/Account/Index
        public async Task<IActionResult> Index(string? keyword, string? role)
        {
            var vm = await _svc.GetUsersAsync(keyword, role);
            return View(vm);
        }

        // GET /Admin/Account/Create
        public IActionResult Create() => View("CreateEdit", new UserFormViewModel());

        // POST /Admin/Account/Create
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(UserFormViewModel vm)
        {
            if (!ModelState.IsValid) return View("CreateEdit", vm);

            var (ok, msg) = await _svc.CreateUserAsync(vm);
            if (!ok)
            {
                ModelState.AddModelError(string.Empty, msg);
                return View("CreateEdit", vm);
            }

            TempData["Success"] = msg;
            return RedirectToAction(nameof(Index));
        }

        // GET /Admin/Account/Edit/{id}
        public async Task<IActionResult> Edit(string id)
        {
            var vm = await _svc.GetUserForEditAsync(id);
            if (vm == null) return NotFound();
            return View("CreateEdit", vm);
        }

        // POST /Admin/Account/Edit
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(UserFormViewModel vm)
        {
            // Khi edit, Password không bắt buộc
            if (vm.IsEditMode)
            {
                ModelState.Remove(nameof(vm.Password));
                ModelState.Remove(nameof(vm.ConfirmPassword));
            }

            if (!ModelState.IsValid) return View("CreateEdit", vm);

            var (ok, msg) = await _svc.UpdateUserAsync(vm);
            if (!ok)
            {
                ModelState.AddModelError(string.Empty, msg);
                return View("CreateEdit", vm);
            }

            TempData["Success"] = msg;
            return RedirectToAction(nameof(Index));
        }

        // POST /Admin/Account/Delete
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            var (ok, msg) = await _svc.DeleteUserAsync(id);
            if (ok)
                TempData["Success"] = msg;
            else
                TempData["Error"] = msg;

            return RedirectToAction(nameof(Index));
        }
    }
}
