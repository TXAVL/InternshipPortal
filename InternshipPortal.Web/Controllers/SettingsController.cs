using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InternshipPortal.Web.Infrastructure;
using InternshipPortal.Web.Models;

namespace InternshipPortal.Web.Controllers;

/// <summary>Trang cài đặt hệ thống dành cho Admin: tên đơn vị, mở đăng ký sinh viên, thông báo toàn hệ thống.</summary>
[Authorize(Roles = Roles.Admin)]
public class SettingsController : Controller
{
    private readonly SiteSettingsStore _store;

    public SettingsController(SiteSettingsStore store)
    {
        _store = store;
    }

    // GET: /Settings
    public IActionResult Index() => View(_store.Current);

    // POST: /Settings/Save
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Save(SiteSettings model)
    {
        if (model.OrganizationName?.Length > 60 || model.AnnouncementText?.Length > 300)
        {
            TempData["ErrorMessage"] = "Tên đơn vị tối đa 60 ký tự, thông báo tối đa 300 ký tự.";
            return RedirectToAction(nameof(Index));
        }

        if (model.MinReportsForEvaluation < 0 || model.MinReportsForEvaluation > 52)
        {
            TempData["ErrorMessage"] = "Số tuần báo cáo tối thiểu phải từ 0 đến 52.";
            return RedirectToAction(nameof(Index));
        }

        if (model.AnnouncementEnabled && string.IsNullOrWhiteSpace(model.AnnouncementText))
        {
            TempData["ErrorMessage"] = "Hãy nhập nội dung thông báo trước khi bật.";
            return RedirectToAction(nameof(Index));
        }

        _store.Save(model);
        TempData["SuccessMessage"] = "Đã lưu cài đặt hệ thống.";
        return RedirectToAction(nameof(Index));
    }
}
