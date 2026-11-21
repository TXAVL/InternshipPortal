using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using InternshipPortal.Web.Data;
using InternshipPortal.Web.Infrastructure;
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

        // Sinh viên đã đăng nhập: cho biết đã ứng tuyển vị trí này chưa (và trạng thái hiện tại)
        if (User.IsInRole(Roles.Student) && User.GetRelatedId() is int studentId)
        {
            ViewBag.MyApplicationStatus = await _context.Applications
                .Where(a => a.JobPostId == id && a.StudentProfileId == studentId)
                .Select(a => (ApplicationStatus?)a.Status)
                .FirstOrDefaultAsync();
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

    // GET: /Job/Create
    [Authorize(Roles = "Admin,Company")]
    public async Task<IActionResult> Create()
    {
        var companies = await GetSelectableCompaniesAsync();

        ViewBag.Companies = new SelectList(companies, "Id", "CompanyName");
        return View(new JobPost());
    }

    // POST: /Job/Create
    [Authorize(Roles = "Admin,Company")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(JobPost job)
    {
        ModelState.Remove("Company");
        ModelState.Remove("Applications");

        var ownCompanyId = CurrentCompanyId();
        if (ownCompanyId.HasValue && job.CompanyId != ownCompanyId.Value)
        {
            ModelState.AddModelError(nameof(JobPost.CompanyId), "Doanh nghiệp chỉ được đăng tin cho công ty mình.");
        }

        if (ModelState.IsValid)
        {
            _context.JobPosts.Add(job);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Đăng tin tuyển dụng mới thành công!";
            return RedirectToAction(nameof(Detail), new { id = job.Id });
        }

        var companies = await GetSelectableCompaniesAsync();

        ViewBag.Companies = new SelectList(companies, "Id", "CompanyName", job.CompanyId);
        return View(job);
    }

    // GET: /Job/Edit/5
    [Authorize(Roles = "Admin,Company")]
    public async Task<IActionResult> Edit(int id)
    {
        var job = await _context.JobPosts.FindAsync(id);
        if (job == null)
        {
            return NotFound();
        }

        var forbidden = ForbidOtherCompany(job.CompanyId, job.Id);
        if (forbidden != null)
        {
            return forbidden;
        }

        var companies = await GetSelectableCompaniesAsync();

        ViewBag.Companies = new SelectList(companies, "Id", "CompanyName", job.CompanyId);
        return View(job);
    }

    // POST: /Job/Edit/5
    [Authorize(Roles = "Admin,Company")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, JobPost job)
    {
        if (id != job.Id)
        {
            return BadRequest();
        }

        ModelState.Remove("Company");
        ModelState.Remove("Applications");

        var originalCompanyId = await _context.JobPosts
            .Where(j => j.Id == id)
            .Select(j => (int?)j.CompanyId)
            .FirstOrDefaultAsync();
        if (originalCompanyId == null)
        {
            return NotFound();
        }

        var forbidden = ForbidOtherCompany(originalCompanyId.Value, id)
                        ?? ForbidOtherCompany(job.CompanyId, id);
        if (forbidden != null)
        {
            return forbidden;
        }

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(job);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Cập nhật tin tuyển dụng thành công!";
                return RedirectToAction(nameof(Detail), new { id = job.Id });
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.JobPosts.AnyAsync(j => j.Id == id))
                {
                    return NotFound();
                }
                throw;
            }
        }

        var companies = await GetSelectableCompaniesAsync();

        ViewBag.Companies = new SelectList(companies, "Id", "CompanyName", job.CompanyId);
        return View(job);
    }

    // POST: /Job/ToggleStatus/5
    [Authorize(Roles = "Admin,Company")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(int id)
    {
        var job = await _context.JobPosts.FindAsync(id);
        if (job == null)
        {
            return NotFound();
        }

        var forbidden = ForbidOtherCompany(job.CompanyId, job.Id);
        if (forbidden != null)
        {
            return forbidden;
        }

        job.IsActive = !job.IsActive;
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = job.IsActive
            ? "Đã mở lại tuyển dụng cho vị trí này!"
            : "Đã tạm đóng tuyển dụng cho vị trí này!";

        return RedirectToAction(nameof(Detail), new { id = job.Id });
    }

    // POST: /Job/Apply
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Apply(ApplyJobViewModel model)
    {
        if (User.IsInRole(Roles.Admin) || User.IsInRole(Roles.Company))
        {
            TempData["WarningMessage"] = "Tài khoản Khoa / Doanh nghiệp không nộp hồ sơ ứng tuyển. Hãy dùng tài khoản sinh viên.";
            return RedirectToAction(nameof(Detail), new { id = model.JobPostId });
        }

        // Sinh viên đã đăng nhập: lấy họ tên, mã SV, email, SĐT, ngành từ hồ sơ tài khoản, chỉ cần đính kèm CV
        if (User.IsInRole(Roles.Student))
        {
            var profileId = User.GetRelatedId();
            var profile = profileId.HasValue ? await _context.StudentProfiles.FindAsync(profileId.Value) : null;
            if (profile == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy hồ sơ sinh viên gắn với tài khoản. Vui lòng đăng nhập lại.";
                return RedirectToAction(nameof(Detail), new { id = model.JobPostId });
            }

            model.StudentCode = profile.StudentCode;
            model.FullName = profile.FullName;
            model.Major = profile.Major;
            model.Email = profile.Email ?? string.Empty;
            model.PhoneNumber = profile.PhoneNumber ?? string.Empty;
            foreach (var key in new[] { "StudentCode", "FullName", "Major", "Email", "PhoneNumber" })
            {
                ModelState.Remove(key);
            }
        }

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

    /// <summary>Doanh nghiệp chỉ được đăng/sửa tin của công ty mình; Admin chọn mọi công ty đã duyệt.</summary>
    private async Task<List<Company>> GetSelectableCompaniesAsync()
    {
        var companyId = CurrentCompanyId();
        return await _context.Companies
            .Where(c => c.IsApproved && (!companyId.HasValue || c.Id == companyId.Value))
            .OrderBy(c => c.CompanyName)
            .ToListAsync();
    }

    private int? CurrentCompanyId()
    {
        return User.GetCompanyScope();
    }

    private IActionResult? ForbidOtherCompany(int jobCompanyId, int jobId)
    {
        var companyId = CurrentCompanyId();
        if (companyId.HasValue && jobCompanyId != companyId.Value)
        {
            TempData["ErrorMessage"] = "Doanh nghiệp chỉ được quản lý tin tuyển dụng của công ty mình.";
            return RedirectToAction(nameof(Detail), new { id = jobId });
        }
        return null;
    }
}
