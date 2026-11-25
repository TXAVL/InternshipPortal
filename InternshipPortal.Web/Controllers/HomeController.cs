using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using InternshipPortal.Web.Infrastructure;
using InternshipPortal.Web.Models;

namespace InternshipPortal.Web.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    // Trang chủ điều hướng theo vai trò: Admin -> Dashboard, Sinh viên -> Cổng SV, còn lại -> Tin tuyển dụng
    public IActionResult Index()
    {
        return User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value switch
        {
            Roles.Admin => RedirectToAction("Index", "Dashboard"),
            Roles.Student => RedirectToAction("Index", "StudentPortal"),
            Roles.Company => RedirectToAction("Index", "Application"),
            _ => RedirectToAction("Index", "Job")
        };
    }

    public IActionResult Privacy()
    {
        return View();
    }

    // Trang thông báo thân thiện cho 404 / 403 / lỗi khác (được UseStatusCodePagesWithReExecute gọi, mã trạng thái gốc vẫn giữ nguyên)
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Status(int code)
    {
        ViewBag.Code = code;
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
