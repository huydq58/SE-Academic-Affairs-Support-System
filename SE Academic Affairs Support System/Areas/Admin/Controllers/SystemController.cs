using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SE_Academic_Affairs_Support_System.Areas.Admin.Controllers
{
    [Authorize(Roles = "Admin")]
    [Area("Admin")]
    [Route("Admin/System/[action]")]
    public class SystemController : Controller
    {
        public IActionResult Index() => View();
    }
}
