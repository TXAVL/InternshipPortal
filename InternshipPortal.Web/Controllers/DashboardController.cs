using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InternshipPortal.Web.Data;
using InternshipPortal.Web.Infrastructure;
using InternshipPortal.Web.Models;
using InternshipPortal.Web.Models.ViewModels;

namespace InternshipPortal.Web.Controllers;

/// <summary>
/// Dashboard thống kê dành cho Khoa / Admin. Mọi số liệu lấy trực tiếp từ CSDL.
/// </summary>
[Authorize(Roles = Roles.Admin)]
public class DashboardController : Controller
{
    private readonly ApplicationDbContext _context;

    public DashboardController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: /Dashboard
    public async Task<IActionResult> Index()
    {
        var model = new DashboardViewModel
        {
            TotalCompanies = await _context.Companies.CountAsync(),
            ApprovedCompanies = await _context.Companies.CountAsync(c => c.IsApproved),
            TotalJobPosts = await _context.JobPosts.CountAsync(),
            ActiveJobPosts = await _context.JobPosts.CountAsync(j => j.IsActive),
            TotalApplications = await _context.Applications.CountAsync(),
            TotalStudentsApplied = await _context.Applications
                .Select(a => a.StudentProfileId).Distinct().CountAsync(),
            AcceptedStudents = await _context.Applications
                .Where(a => a.Status == ApplicationStatus.Accepted)
                .Select(a => a.StudentProfileId).Distinct().CountAsync(),
            StatusCounts = await _context.Applications
                .GroupBy(a => a.Status)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count),
            CompanyStats = await _context.Companies
                .Select(c => new CompanyStat
                {
                    CompanyId = c.Id,
                    CompanyName = c.CompanyName,
                    IsApproved = c.IsApproved,
                    JobPosts = c.JobPosts.Count,
                    Applications = c.JobPosts.SelectMany(j => j.Applications).Count(),
                    Accepted = c.JobPosts.SelectMany(j => j.Applications)
                        .Count(a => a.Status == ApplicationStatus.Accepted)
                })
                .OrderByDescending(c => c.Applications)
                .ThenBy(c => c.CompanyName)
                .ToListAsync(),
            ReportsAwaitingFeedback = await _context.WeeklyReports
                .CountAsync(r => (r.MentorFeedback == null || r.MentorFeedback == "") && (r.CompanyFeedback == null || r.CompanyFeedback == "")),
            RecentReports = await _context.WeeklyReports
                .Include(r => r.Application)
                    .ThenInclude(a => a.StudentProfile)
                .Include(r => r.Application)
                    .ThenInclude(a => a.JobPost)
                        .ThenInclude(j => j.Company)
                .OrderByDescending(r => r.SubmittedAt)
                .Take(5)
                .ToListAsync()
        };

        return View(model);
    }
}
