using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InternshipPortal.Web.Data;
using InternshipPortal.Web.Infrastructure;
using InternshipPortal.Web.Models;

namespace InternshipPortal.Web.Controllers;

/// <summary>
/// Đánh giá và chấm điểm cuối kỳ. Doanh nghiệp chỉ chấm phần của mình (40%) cho sinh viên của công ty mình,
/// Khoa / Admin chấm phần của Khoa (60%).
/// </summary>
[Authorize(Roles = "Admin,Company")]
public class EvaluationController : Controller
{
    private readonly ApplicationDbContext _context;

    public EvaluationController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: /Evaluation
    public async Task<IActionResult> Index()
    {
        var query = _context.Applications
            .Where(a => a.Status == ApplicationStatus.Accepted)
            .Include(a => a.StudentProfile)
            .Include(a => a.JobPost)
                .ThenInclude(j => j.Company)
            .Include(a => a.Evaluation)
            .AsQueryable();

        var companyId = User.GetCompanyScope();
        if (companyId.HasValue)
        {
            query = query.Where(a => a.JobPost.CompanyId == companyId.Value);
        }

        var model = await query
            .OrderBy(a => a.StudentProfile.FullName)
            .ToListAsync();
        return View(model);
    }

    // POST: /Evaluation/SubmitScore
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitScore(int applicationId, string? score, string? comment)
    {
        // Chấp nhận cả "8.5" lẫn "8,5" để không phụ thuộc cấu hình ngôn ngữ của máy chủ
        var raw = (score ?? string.Empty).Trim().Replace(',', '.');
        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            || value < 0 || value > 10)
        {
            TempData["ErrorMessage"] = "Điểm phải là số từ 0 đến 10.";
            return RedirectToAction(nameof(Index));
        }
        value = Math.Round(value, 1);

        var application = await _context.Applications
            .Include(a => a.JobPost)
            .Include(a => a.Evaluation)
            .FirstOrDefaultAsync(a => a.Id == applicationId);
        if (application == null)
        {
            return NotFound();
        }

        // Doanh nghiệp chỉ được chấm sinh viên thực tập tại công ty của mình
        var companyId = User.GetCompanyScope();
        if (companyId.HasValue && application.JobPost.CompanyId != companyId.Value)
        {
            return Forbid();
        }

        if (application.Status != ApplicationStatus.Accepted)
        {
            TempData["ErrorMessage"] = "Chỉ chấm điểm cho sinh viên đã Trúng tuyển.";
            return RedirectToAction(nameof(Index));
        }

        var evaluation = application.Evaluation;
        if (evaluation == null)
        {
            evaluation = new Evaluation { ApplicationId = application.Id };
            _context.Evaluations.Add(evaluation);
        }

        var text = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        if (User.IsInRole(Roles.Company))
        {
            evaluation.CompanyScore = value;
            evaluation.CompanyComment = text;
        }
        else
        {
            evaluation.SchoolScore = value;
            evaluation.SchoolComment = text;
        }

        evaluation.EvaluatedAt = DateTime.Now;
        evaluation.Recalculate();

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Hai người bấm lưu cùng lúc cho một đơn mới: chỉ mục duy nhất chặn bản trùng
            TempData["WarningMessage"] = "Bản đánh giá vừa được cập nhật bởi người khác, vui lòng chấm lại.";
            return RedirectToAction(nameof(Index));
        }

        TempData["SuccessMessage"] = evaluation.FinalScore.HasValue
            ? $"Đã lưu điểm. Điểm tổng kết: {evaluation.FinalScore.Value.ToString("0.##", CultureInfo.InvariantCulture)}."
            : "Đã lưu điểm. Điểm tổng kết sẽ có khi đủ cả điểm Doanh nghiệp và điểm Khoa.";
        return RedirectToAction(nameof(Index));
    }
}
