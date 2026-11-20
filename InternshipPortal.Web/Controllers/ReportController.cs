using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InternshipPortal.Web.Data;
using InternshipPortal.Web.Infrastructure;
using InternshipPortal.Web.Models;

namespace InternshipPortal.Web.Controllers;

/// <summary>
/// Dành cho GVHD / Khoa (Admin) và Doanh nghiệp: xem nhật ký thực tập và nhập nhận xét.
/// Doanh nghiệp chỉ thấy báo cáo của sinh viên thực tập tại công ty mình.
/// </summary>
[Authorize(Roles = "Admin,Company")]
public class ReportController : Controller
{
    private readonly ApplicationDbContext _context;

    public ReportController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: /Report
    public async Task<IActionResult> Index()
    {
        var companyId = User.GetCompanyScope();

        var reports = await _context.WeeklyReports
            .Include(r => r.Application).ThenInclude(a => a.StudentProfile)
            .Include(r => r.Application).ThenInclude(a => a.JobPost).ThenInclude(j => j.Company)
            .Where(r => !companyId.HasValue || r.Application.JobPost.CompanyId == companyId.Value)
            .OrderByDescending(r => r.SubmittedAt)
            .ToListAsync();

        return View(reports);
    }

    // POST: /Report/AddFeedback
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddFeedback(int reportId, string feedback)
    {
        var report = await _context.WeeklyReports
            .Include(r => r.Application).ThenInclude(a => a.JobPost)
            .FirstOrDefaultAsync(r => r.Id == reportId);

        if (report == null)
        {
            return NotFound();
        }

        var companyId = User.GetCompanyScope();
        if (companyId.HasValue && report.Application.JobPost.CompanyId != companyId.Value)
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(feedback))
        {
            TempData["ErrorMessage"] = "Vui lòng nhập nội dung nhận xét.";
            return RedirectToAction(nameof(Index));
        }

        // Admin (Khoa/GVHD) ghi vào MentorFeedback, Company ghi vào CompanyFeedback: 2 nhận xét độc lập
        if (User.IsInRole(Roles.Admin))
        {
            report.MentorFeedback = feedback.Trim();
        }
        else
        {
            report.CompanyFeedback = feedback.Trim();
        }
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Đã lưu nhận xét cho báo cáo Tuần {report.WeekNumber}.";
        return RedirectToAction(nameof(Index));
    }
}
