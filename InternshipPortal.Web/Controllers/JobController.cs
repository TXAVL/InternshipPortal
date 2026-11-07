using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InternshipPortal.Web.Data;
using InternshipPortal.Web.Models;
using InternshipPortal.Web.Models.ViewModels;

namespace InternshipPortal.Web.Controllers;

public class JobController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _environment;

    public JobController(ApplicationDbContext context, IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    // GET: /Job or /
    public async Task<IActionResult> Index(string? keyword, string? major, string? location)
    {
        var query = _context.JobPosts
            .Include(j => j.Company)
            .Where(j => j.IsActive);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim().ToLower();
            query = query.Where(j => j.Title.ToLower().Contains(kw) ||
                                     j.Company.CompanyName.ToLower().Contains(kw) ||
                                     (j.Description != null && j.Description.ToLower().Contains(kw)));
        }

        if (!string.IsNullOrWhiteSpace(major))
        {
            query = query.Where(j => j.Major == major);
        }

        if (!string.IsNullOrWhiteSpace(location))
        {
            query = query.Where(j => j.Location == location);
        }

        var jobs = await query.OrderByDescending(j => j.Id).ToListAsync();

        var availableMajors = await _context.JobPosts
            .Select(j => j.Major)
            .Distinct()
            .ToListAsync();

        var availableLocations = await _context.JobPosts
            .Select(j => j.Location)
            .Distinct()
            .ToListAsync();

        var model = new JobFilterViewModel
        {
            Keyword = keyword,
            Major = major,
            Location = location,
            Jobs = jobs,
            AvailableMajors = availableMajors,
            AvailableLocations = availableLocations
        };

        return View(model);
    }

    // GET: /Job/Detail/5
    public async Task<IActionResult> Detail(int id)
    {
        var job = await _context.JobPosts
            .Include(j => j.Company)
            .FirstOrDefaultAsync(j => j.Id == id);

        if (job == null)
        {
            return NotFound();
        }

        ViewBag.ApplyModel = new ApplyJobViewModel
        {
            JobPostId = job.Id,
            JobTitle = job.Title,
            CompanyName = job.Company?.CompanyName ?? "Doanh nghiệp",
            Major = job.Major
        };

        return View(job);
    }

    // POST: /Job/Apply
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Apply(ApplyJobViewModel model)
    {
        var job = await _context.JobPosts
            .Include(j => j.Company)
            .FirstOrDefaultAsync(j => j.Id == model.JobPostId);

        if (job == null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Thông tin nộp hồ sơ chưa hợp lệ. Vui lòng kiểm tra lại!";
            return RedirectToAction(nameof(Detail), new { id = model.JobPostId });
        }

        // Xử lý upload file CV nếu có
        string? cvPath = null;
        if (model.CVFile != null && model.CVFile.Length > 0)
        {
            var allowedExtensions = new[] { ".pdf", ".doc", ".docx" };
            var fileExt = Path.GetExtension(model.CVFile.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(fileExt))
            {
                TempData["ErrorMessage"] = "Định dạng file không hỗ trợ! Vui lòng tải lên file .pdf, .doc hoặc .docx";
                return RedirectToAction(nameof(Detail), new { id = model.JobPostId });
            }

            if (model.CVFile.Length > 10 * 1024 * 1024) // 10MB limit
            {
                TempData["ErrorMessage"] = "Dung lượng file CV vượt quá giới hạn cho phép (tối đa 10MB)!";
                return RedirectToAction(nameof(Detail), new { id = model.JobPostId });
            }

            var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "cvs");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var uniqueFileName = $"{Guid.NewGuid():N}_{Path.GetFileName(model.CVFile.FileName)}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await model.CVFile.CopyToAsync(stream);
            }

            cvPath = $"/uploads/cvs/{uniqueFileName}";
        }

        // Tìm hoặc cập nhật thông tin StudentProfile theo mã sinh viên
        var student = await _context.StudentProfiles
            .FirstOrDefaultAsync(s => s.StudentCode.ToLower() == model.StudentCode.Trim().ToLower());

        if (student == null)
        {
            student = new StudentProfile
            {
                StudentCode = model.StudentCode.Trim(),
                FullName = model.FullName.Trim(),
                Major = model.Major,
                Email = model.Email.Trim(),
                PhoneNumber = model.PhoneNumber.Trim(),
                CVPath = cvPath
            };
            _context.StudentProfiles.Add(student);
            await _context.SaveChangesAsync();
        }
        else
        {
            student.FullName = model.FullName.Trim();
            student.Email = model.Email.Trim();
            student.PhoneNumber = model.PhoneNumber.Trim();
            student.Major = model.Major ?? student.Major;
            if (!string.IsNullOrEmpty(cvPath))
            {
                student.CVPath = cvPath;
            }
            await _context.SaveChangesAsync();
        }

        // Kiểm tra xem sinh viên đã nộp đơn cho vị trí này chưa
        var alreadyApplied = await _context.Applications.AnyAsync(a =>
            a.JobPostId == model.JobPostId && a.StudentProfileId == student.Id);

        if (alreadyApplied)
        {
            TempData["WarningMessage"] = "Bạn đã nộp hồ sơ ứng tuyển vị trí này trước đó rồi! Vui lòng chờ nhà tuyển dụng phản hồi.";
            return RedirectToAction(nameof(Detail), new { id = model.JobPostId });
        }

        // Tạo bản ghi Application mới
        var application = new Application
        {
            JobPostId = model.JobPostId,
            StudentProfileId = student.Id,
            AppliedAt = DateTime.Now,
            Status = ApplicationStatus.Pending
        };

        _context.Applications.Add(application);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Nộp hồ sơ ứng tuyển vị trí '{job.Title}' thành công! Hồ sơ của bạn đã được chuyển tới {job.Company?.CompanyName}.";
        return RedirectToAction(nameof(Detail), new { id = model.JobPostId });
    }
}
