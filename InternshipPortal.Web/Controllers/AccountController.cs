using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InternshipPortal.Web.Data;
using InternshipPortal.Web.Infrastructure;
using InternshipPortal.Web.Models;
using InternshipPortal.Web.Models.ViewModels;

namespace InternshipPortal.Web.Controllers;

/// <summary>
/// Đăng nhập / đăng ký / đăng xuất bằng Cookie Authentication với tài khoản thật trong bảng UserAccounts.
/// </summary>
public class AccountController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly PasswordHasher<UserAccount> _hasher = new();
    private readonly SiteSettingsStore _site;

    public AccountController(ApplicationDbContext context, SiteSettingsStore site)
    {
        _context = context;
        _site = site;
    }

    // GET: /Account/Login
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToHome();
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    // POST: /Account/Login
    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var username = model.Username.Trim();
        var account = await _context.UserAccounts.FirstOrDefaultAsync(u => u.Username == username);

        var verified = account != null &&
            _hasher.VerifyHashedPassword(account, account.PasswordHash, model.Password) != PasswordVerificationResult.Failed;

        if (!verified)
        {
            ModelState.AddModelError(string.Empty, "Tài khoản hoặc mật khẩu không đúng.");
            model.Password = string.Empty;
            return View(model);
        }

        await SignInAsync(account!);

        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return LocalRedirect(model.ReturnUrl);
        }

        return RedirectToHome(account!.Role);
    }

    // GET: /Account/Register
    [AllowAnonymous]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToHome();
        }

        if (!_site.Current.AllowStudentRegistration)
        {
            TempData["WarningMessage"] = "Hiện hệ thống tạm đóng đăng ký tài khoản sinh viên. Vui lòng liên hệ Khoa.";
            return RedirectToAction(nameof(Login));
        }

        return View(new RegisterViewModel());
    }

    // POST: /Account/Register  (Sinh viên tự đăng ký: tạo StudentProfile + UserAccount)
    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!_site.Current.AllowStudentRegistration)
        {
            TempData["WarningMessage"] = "Hiện hệ thống tạm đóng đăng ký tài khoản sinh viên. Vui lòng liên hệ Khoa.";
            return RedirectToAction(nameof(Login));
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var code = model.StudentCode.Trim();
        var email = model.Email.Trim();

        if (await _context.UserAccounts.AnyAsync(u => u.Username == code))
        {
            ModelState.AddModelError(nameof(model.StudentCode), "Mã sinh viên này đã có tài khoản.");
            return View(model);
        }

        var student = await _context.StudentProfiles.FirstOrDefaultAsync(s => s.StudentCode == code);
        if (student == null)
        {
            student = new StudentProfile
            {
                StudentCode = code,
                FullName = model.FullName.Trim(),
                Major = model.Major,
                Email = email,
                PhoneNumber = model.PhoneNumber?.Trim()
            };
            _context.StudentProfiles.Add(student);
        }
        else if (!string.Equals(student.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            // Hồ sơ đã tồn tại do từng nộp đơn: chỉ liên kết khi đúng email đã dùng lúc nộp
            ModelState.AddModelError(nameof(model.Email), "Mã sinh viên đã có hồ sơ ứng tuyển với email khác. Hãy nhập đúng email đã dùng khi nộp hồ sơ.");
            return View(model);
        }

        await _context.SaveChangesAsync();

        var account = new UserAccount
        {
            Username = code,
            FullName = student.FullName,
            Role = Roles.Student,
            RelatedId = student.Id
        };
        account.PasswordHash = _hasher.HashPassword(account, model.Password);
        _context.UserAccounts.Add(account);
        await _context.SaveChangesAsync();

        await SignInAsync(account);
        TempData["SuccessMessage"] = "Đăng ký thành công! Chào mừng bạn đến với Cổng sinh viên.";
        return RedirectToAction("Index", "StudentPortal");
    }

    // POST: /Account/Logout
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    // GET: /Account/AccessDenied
    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        return View();
    }

    private async Task SignInAsync(UserAccount account)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, account.Id.ToString()),
            new(ClaimTypes.Name, account.FullName),
            new(ClaimTypes.Role, account.Role)
        };
        if (account.RelatedId.HasValue)
        {
            claims.Add(new Claim(UserClaims.RelatedIdType, account.RelatedId.Value.ToString()));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));
    }

    private IActionResult RedirectToHome(string? role = null)
    {
        role ??= User.FindFirstValue(ClaimTypes.Role);
        return role switch
        {
            Roles.Admin => RedirectToAction("Index", "Dashboard"),
            Roles.Company => RedirectToAction("Index", "Application"),
            Roles.Student => RedirectToAction("Index", "StudentPortal"),
            _ => RedirectToAction("Index", "Job")
        };
    }
}
