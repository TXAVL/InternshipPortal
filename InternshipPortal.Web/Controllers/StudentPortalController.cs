using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InternshipPortal.Web.Data;
using InternshipPortal.Web.Infrastructure;
using InternshipPortal.Web.Models;
using InternshipPortal.Web.Models.ViewModels;

namespace InternshipPortal.Web.Controllers;

/// <summary>
/// Cổng Sinh Viên: tiến độ ứng tuyển và nhật ký thực tập của chính sinh viên đang đăng nhập
/// (lấy StudentProfileId từ claim "RelatedId", không nhận mã SV từ URL).
/// </summary>
[Authorize(Roles = Roles.Student)]
public class StudentPortalController : Controller
{
    private readonly ApplicationDbContext _context;

    public StudentPortalController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: /StudentPortal
    public async Task<IActionResult> Index()
    {
        var studentId = User.GetRelatedId();
        var model = new StudentPortalViewModel();

        if (studentId == null)
        {
            return View(model);
        }

        model.Student = await _context.StudentProfiles.FirstOrDefaultAsync(s => s.Id == studentId.Value);
        if (model.Student != null)
        {
            model.Applications = await _context.Applications
                .Where(a => a.StudentProfileId == model.Student.Id)
                .Include(a => a.JobPost)
                    .ThenInclude(j => j.Company)
                .Include(a => a.WeeklyReports)
                .OrderByDescending(a => a.Status == ApplicationStatus.Accepted)
                .ThenByDescending(a => a.AppliedAt)
                .ToListAsync();
        }

        return View(model);
    }

    // POST: /StudentPortal/SubmitReport
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitReport(WeeklyReportInput input)
    {
        var studentId = User.GetRelatedId();

        var application = await _context.Applications
            .Include(a => a.WeeklyReports)
            .FirstOrDefaultAsync(a => a.Id == input.ApplicationId);

        if (application == null)
        {
            return NotFound();
        }

        if (application.StudentProfileId != studentId)
        {
            return Forbid();
        }

        if (application.Status != ApplicationStatus.Accepted)
        {
            TempData["ErrorMessage"] = "Chỉ đơn đã Trúng tuyển mới được nộp nhật ký thực tập.";
            return RedirectToAction(nameof(Index));
        }

        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .FirstOrDefault() ?? "Nội dung báo cáo chưa hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        var nextWeek = application.WeeklyReports.Count == 0
            ? 1
            : application.WeeklyReports.Max(r => r.WeekNumber) + 1;

        _context.WeeklyReports.Add(new WeeklyReport
        {
            ApplicationId = application.Id,
            WeekNumber = nextWeek,
            TasksDone = input.TasksDone.Trim(),
            NextWeekPlan = input.NextWeekPlan?.Trim() ?? string.Empty,
            SubmittedAt = DateTime.Now
        });
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Đã nộp báo cáo Tuần {nextWeek}.";
        return RedirectToAction(nameof(Index));
    }
}
