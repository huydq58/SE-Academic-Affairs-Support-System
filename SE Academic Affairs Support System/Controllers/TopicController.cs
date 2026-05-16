// Controllers/TopicController.cs
using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SE_Academic_Affairs_Support_System.Data;
using SE_Academic_Affairs_Support_System.Helper;
using SE_Academic_Affairs_Support_System.Models;
using SE_Academic_Affairs_Support_System.ViewModels;

public class TopicController : Controller
{
    private readonly GoogleSheetsService _sheets;
    private readonly AppDbContext _db;    // hoặc service lấy RegistrationPeriod
    private readonly UserManager<User> _userManager;

    public TopicController(GoogleSheetsService sheets, AppDbContext db, UserManager<User> userManager)
    {
        _sheets = sheets;
        _db = db;
        _userManager = userManager;
    }

    // GET: /Topic/List?periodId=5
    public async Task<IActionResult> List(int periodId)
    {
        var period = await _db.RegistrationPeriods.FindAsync(periodId);
        if (period == null) return NotFound();

        var sheetId = GoogleSheetHelper.ExtractSheetId(period.GoogleSheetLink);
        if (sheetId == null)
            return BadRequest("Đợt này chưa có link Google Sheet hợp lệ.");

        var topics = await _sheets.GetTopicsAsync(sheetId);

        var vm = new TopicListSheetViewModel
        {
            PeriodName = period.Name,
            CourseName = period.CourseName,
            SheetId = sheetId,
            Topics = topics
        };

        return View(vm);
    }

    // POST: /Topic/Register
    [Authorize(Roles = "Student")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(
        string sheetId, string periodName, string courseName,
        int rowIndex)
    {
        // Lấy thông tin sinh viên từ session/claims tùy hệ thống login của bạn
        var user = await _userManager.GetUserAsync(User);

        var studentName = user.FullName;
        var studentId = user.Id;

        var result = await _sheets.RegisterAsync(new RegisterTopicRequest
        {
            SheetId = sheetId,
            RowIndex = rowIndex,
            StudentName = studentName!,
            StudentId = studentId
        });

        var topics = await _sheets.GetTopicsAsync(sheetId);

        var vm = new TopicListSheetViewModel
        {
            PeriodName = periodName,
            CourseName = courseName,
            SheetId = sheetId,
            Topics = topics,
            StatusMessage = result.Message,
            IsSuccess = result.Success
        };

        return View("List", vm);
    }
}