using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;
using InternshipPortal.Web.Data;
using InternshipPortal.Web.Models;

namespace InternshipPortal.Web.Controllers;

public class ApplicationController : Controller
{
    private readonly ApplicationDbContext _context;

    public ApplicationController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: /Application
    public async Task<IActionResult> Index(int? jobPostId, ApplicationStatus? status, string? search)
    {
        var query = _context.Applications
            .Include(a => a.StudentProfile)
            .Include(a => a.JobPost)
                .ThenInclude(j => j.Company)
            .AsQueryable();

        if (jobPostId.HasValue && jobPostId.Value > 0)
        {
            query = query.Where(a => a.JobPostId == jobPostId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(a => a.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(a =>
                a.StudentProfile.FullName.ToLower().Contains(term) ||
                a.StudentProfile.StudentCode.ToLower().Contains(term) ||
                (a.StudentProfile.Email != null && a.StudentProfile.Email.ToLower().Contains(term)) ||
                a.JobPost.Title.ToLower().Contains(term) ||
                a.JobPost.Company.CompanyName.ToLower().Contains(term));
        }

        var applications = await query
            .OrderByDescending(a => a.AppliedAt)
            .ToListAsync();

        ViewBag.JobPosts = await _context.JobPosts.OrderBy(j => j.Title).ToListAsync();
        ViewBag.SelectedJobPostId = jobPostId;
        ViewBag.SelectedStatus = status;
        ViewBag.SearchTerm = search;

        return View(applications);
    }

    // POST: /Application/UpdateStatus
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, ApplicationStatus status)
    {
        var app = await _context.Applications.FindAsync(id);
        if (app == null)
        {
            return NotFound();
        }

        app.Status = status;
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Đã cập nhật trạng thái đơn ứng tuyển thành: {GetStatusDisplayName(status)}";
        return RedirectToAction(nameof(Index));
    }

    // GET: /Application/ExportExcel
    public async Task<IActionResult> ExportExcel(int? jobPostId, ApplicationStatus? status)
    {
        var query = _context.Applications
            .Include(a => a.StudentProfile)
            .Include(a => a.JobPost)
                .ThenInclude(j => j.Company)
            .AsQueryable();

        if (jobPostId.HasValue && jobPostId.Value > 0)
        {
            query = query.Where(a => a.JobPostId == jobPostId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(a => a.Status == status.Value);
        }

        var applications = await query
            .OrderByDescending(a => a.AppliedAt)
            .ToListAsync();

        using (var workbook = new XLWorkbook())
        {
            var worksheet = workbook.Worksheets.Add("DanhSachUngTuyen");

            // Tiêu đề bảng
            worksheet.Cell(1, 1).Value = "BÁO CÁO DANH SÁCH SINH VIÊN ỨNG TUYỂN THỰC TẬP";
            worksheet.Range("A1:K1").Merge();
            worksheet.Cell(1, 1).Style.Font.Bold = true;
            worksheet.Cell(1, 1).Style.Font.FontSize = 16;
            worksheet.Cell(1, 1).Style.Font.FontColor = XLColor.FromArgb(13, 110, 253);
            worksheet.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            worksheet.Cell(2, 1).Value = $"Ngày xuất: {DateTime.Now:dd/MM/yyyy HH:mm} | Tổng số: {applications.Count} hồ sơ";
            worksheet.Range("A2:K2").Merge();
            worksheet.Cell(2, 1).Style.Font.Italic = true;
            worksheet.Cell(2, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            // Header hàng 4
            string[] headers =
            {
                "STT", "Mã SV", "Họ và tên", "Chuyên ngành",
                "Email", "Số điện thoại", "Vị trí ứng tuyển",
                "Doanh nghiệp", "Ngày nộp", "Trạng thái", "File CV"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                var cell = worksheet.Cell(4, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Fill.BackgroundColor = XLColor.FromArgb(13, 110, 253);
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            }
            worksheet.Row(4).Height = 25;

            // Dữ liệu từ hàng 5
            int row = 5;
            for (int i = 0; i < applications.Count; i++)
            {
                var app = applications[i];
                worksheet.Cell(row, 1).Value = i + 1;
                worksheet.Cell(row, 2).Value = app.StudentProfile?.StudentCode ?? "";
                worksheet.Cell(row, 3).Value = app.StudentProfile?.FullName ?? "";
                worksheet.Cell(row, 4).Value = app.StudentProfile?.Major ?? "";
                worksheet.Cell(row, 5).Value = app.StudentProfile?.Email ?? "";
                worksheet.Cell(row, 6).Value = app.StudentProfile?.PhoneNumber ?? "";
                worksheet.Cell(row, 7).Value = app.JobPost?.Title ?? "";
                worksheet.Cell(row, 8).Value = app.JobPost?.Company?.CompanyName ?? "";
                worksheet.Cell(row, 9).Value = app.AppliedAt.ToString("dd/MM/yyyy HH:mm");
                worksheet.Cell(row, 10).Value = GetStatusDisplayName(app.Status);
                worksheet.Cell(row, 11).Value = !string.IsNullOrEmpty(app.StudentProfile?.CVPath) ? "Đã nộp" : "Chưa có";

                // Canh giữa STT, Mã SV, Ngày nộp, Trạng thái
                worksheet.Cell(row, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                worksheet.Cell(row, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                worksheet.Cell(row, 9).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                worksheet.Cell(row, 10).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                worksheet.Cell(row, 11).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                // Tô màu trạng thái
                var statusCell = worksheet.Cell(row, 10);
                switch (app.Status)
                {
                    case ApplicationStatus.Accepted:
                        statusCell.Style.Font.FontColor = XLColor.Green;
                        statusCell.Style.Font.Bold = true;
                        break;
                    case ApplicationStatus.Rejected:
                        statusCell.Style.Font.FontColor = XLColor.Red;
                        break;
                    case ApplicationStatus.Interview:
                        statusCell.Style.Font.FontColor = XLColor.FromArgb(13, 110, 253);
                        statusCell.Style.Font.Bold = true;
                        break;
                    default:
                        statusCell.Style.Font.FontColor = XLColor.DarkOrange;
                        break;
                }

                row++;
            }

            // Kẻ bảng border
            var dataRange = worksheet.Range(4, 1, Math.Max(row - 1, 4), headers.Length);
            dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            dataRange.Style.Border.InsideBorderColor = XLColor.LightGray;
            dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
            dataRange.Style.Border.OutsideBorderColor = XLColor.FromArgb(13, 110, 253);

            // Tự động căn chỉnh kích thước cột
            worksheet.Columns().AdjustToContents();

            using (var stream = new MemoryStream())
            {
                workbook.SaveAs(stream);
                var content = stream.ToArray();
                var fileName = $"DanhSachUngTuyen_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
                return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
        }
    }

    private static string GetStatusDisplayName(ApplicationStatus status) => status switch
    {
        ApplicationStatus.Pending => "Đang chờ duyệt",
        ApplicationStatus.Interview => "Mời phỏng vấn",
        ApplicationStatus.Accepted => "Trúng tuyển",
        ApplicationStatus.Rejected => "Từ chối",
        _ => status.ToString()
    };
}
