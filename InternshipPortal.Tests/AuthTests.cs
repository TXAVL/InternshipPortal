using System.Net;
using Microsoft.Extensions.DependencyInjection;
using InternshipPortal.Web.Infrastructure;

namespace InternshipPortal.Tests;

[Collection("app")]
public class AuthTests
{
    private readonly TestApp _app;
    public AuthTests(TestApp app) => _app = app;

    [Theory]
    [InlineData("/Dashboard")]
    [InlineData("/Application")]
    [InlineData("/Evaluation")]
    [InlineData("/Report")]
    [InlineData("/StudentPortal")]
    [InlineData("/Settings")]
    [InlineData("/Job/Create")]
    [InlineData("/Company/Create")]
    [InlineData("/Application/ExportExcel")]
    public async Task Khach_chua_dang_nhap_bi_chuyen_ve_trang_dang_nhap(string url)
    {
        var c = await Client.As(_app, null);
        Assert.True(Client.IsLoginRedirect(await c.Http.GetAsync(url)), url);
    }

    [Theory]
    [InlineData("/Job")]
    [InlineData("/Account/Login")]
    [InlineData("/Account/Register")]
    public async Task Trang_cong_khai_mo_duoc_khi_chua_dang_nhap(string url)
    {
        var c = await Client.As(_app, null);
        Assert.Equal(HttpStatusCode.OK, (await c.Http.GetAsync(url)).StatusCode);
    }

    // (tài khoản, đường dẫn, được vào?)
    [Theory]
    [InlineData(Accounts.Admin, "/Dashboard", true)]
    [InlineData(Accounts.Admin, "/Settings", true)]
    [InlineData(Accounts.Admin, "/Application", true)]
    [InlineData(Accounts.Admin, "/Application/ExportExcel", true)]
    [InlineData(Accounts.Admin, "/Evaluation", true)]
    [InlineData(Accounts.Admin, "/Report", true)]
    [InlineData(Accounts.Admin, "/Company/Create", true)]
    [InlineData(Accounts.Admin, "/StudentPortal", false)]
    [InlineData(Accounts.Hr, "/Application", true)]
    [InlineData(Accounts.Hr, "/Evaluation", true)]
    [InlineData(Accounts.Hr, "/Report", true)]
    [InlineData(Accounts.Hr, "/Job/Create", true)]
    [InlineData(Accounts.Hr, "/Dashboard", false)]
    [InlineData(Accounts.Hr, "/Settings", false)]
    [InlineData(Accounts.Hr, "/Company/Create", false)]
    [InlineData(Accounts.Hr, "/Application/ExportExcel", false)]
    [InlineData(Accounts.Hr, "/StudentPortal", false)]
    [InlineData(Accounts.Student, "/StudentPortal", true)]
    [InlineData(Accounts.Student, "/Dashboard", false)]
    [InlineData(Accounts.Student, "/Settings", false)]
    [InlineData(Accounts.Student, "/Application", false)]
    [InlineData(Accounts.Student, "/Evaluation", false)]
    [InlineData(Accounts.Student, "/Report", false)]
    [InlineData(Accounts.Student, "/Job/Create", false)]
    public async Task Ma_tran_phan_quyen_theo_vai_tro(string user, string url, bool allowed)
    {
        var c = await Client.As(_app, user);
        var r = await c.Http.GetAsync(url);
        if (allowed) Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        else Assert.True(Client.IsDenied(r), $"{user} không được vào {url} nhưng nhận {(int)r.StatusCode} {Client.Location(r)}");
    }

    [Fact]
    public async Task Sai_mat_khau_khong_dang_nhap_duoc()
    {
        var c = await Client.As(_app, null);
        var r = await c.PostAsync("/Account/Login", new Dictionary<string, string> { ["Username"] = Accounts.Admin, ["Password"] = "sai-mat-khau" });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.True(Client.IsLoginRedirect(await c.Http.GetAsync("/Dashboard")));
    }

    [Fact]
    public async Task Tai_khoan_khong_ton_tai_khong_dang_nhap_duoc()
    {
        var c = await Client.As(_app, null);
        var r = await c.PostAsync("/Account/Login", new Dictionary<string, string> { ["Username"] = "khong-co@x.vn", ["Password"] = "123456" });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
    }

    [Fact]
    public async Task ReturnUrl_ngoai_trang_khong_duoc_chuyen_huong_ra_ngoai()
    {
        var c = await Client.As(_app, null);
        var r = await c.PostAsync("/Account/Login", new Dictionary<string, string>
        {
            ["Username"] = Accounts.Admin, ["Password"] = "123456", ["ReturnUrl"] = "https://evil.example/phish"
        });
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.DoesNotContain("evil.example", Client.Location(r));
    }

    [Fact]
    public async Task ReturnUrl_noi_bo_duoc_giu()
    {
        var c = await Client.As(_app, null);
        var r = await c.PostAsync("/Account/Login", new Dictionary<string, string>
        {
            ["Username"] = Accounts.Admin, ["Password"] = "123456", ["ReturnUrl"] = "/Settings"
        });
        Assert.Equal("/Settings", Client.Location(r));
    }

    [Fact]
    public async Task Dang_xuat_xoa_phien()
    {
        var c = await Client.As(_app, Accounts.Admin);
        Assert.Equal(HttpStatusCode.OK, (await c.Http.GetAsync("/Dashboard")).StatusCode);
        await c.PostAsync("/Account/Logout");
        Assert.True(Client.IsLoginRedirect(await c.Http.GetAsync("/Dashboard")));
    }

    [Fact]
    public async Task Post_khong_co_token_chong_gia_mao_bi_chan()
    {
        var c = await Client.As(_app, Accounts.Admin);
        var r = await c.Http.PostAsync("/Settings/Save", new FormUrlEncodedContent(new Dictionary<string, string> { ["OrganizationName"] = "Hack" }));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    // ---------- Đăng ký sinh viên ----------
    private static Dictionary<string, string> RegisterForm(string code, string email, string pass = "abc123", string? confirm = null) => new()
    {
        ["StudentCode"] = code, ["FullName"] = "Nguyễn Test", ["Email"] = email, ["PhoneNumber"] = "0912345678",
        ["Major"] = "CNTT", ["Password"] = pass, ["ConfirmPassword"] = confirm ?? pass
    };

    [Fact]
    public async Task Dang_ky_thanh_cong_tao_tai_khoan_sinh_vien_va_dang_nhap_luon()
    {
        var code = TestApp.Uniq("DK");
        var c = await Client.As(_app, null);
        var r = await c.PostAsync("/Account/Register", RegisterForm(code, code + "@test.vn"));
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Contains("StudentPortal", Client.Location(r));
        Assert.NotNull(_app.Query(db => db.UserAccounts.SingleOrDefault(u => u.Username == code && u.Role == "Student")));
        // mật khẩu phải được băm, không lưu thô
        Assert.NotEqual("abc123", _app.Query(db => db.UserAccounts.Single(u => u.Username == code).PasswordHash));
    }

    [Fact]
    public async Task Dang_ky_trung_ma_sinh_vien_bi_tu_choi()
    {
        var (_, code) = _app.NewStudent();
        var c = await Client.As(_app, null);
        var r = await c.PostAsync("/Account/Register", RegisterForm(code, "khac@test.vn"));
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal(1, _app.Query(db => db.UserAccounts.Count(u => u.Username == code)));
    }

    [Theory]
    [InlineData("abc123", "khac456")]   // không khớp
    [InlineData("12345", "12345")]      // quá ngắn
    public async Task Dang_ky_mat_khau_khong_hop_le_bi_tu_choi(string pass, string confirm)
    {
        var code = TestApp.Uniq("MK");
        var c = await Client.As(_app, null);
        var r = await c.PostAsync("/Account/Register", RegisterForm(code, code + "@test.vn", pass, confirm));
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Null(_app.Query(db => db.UserAccounts.SingleOrDefault(u => u.Username == code)));
    }

    [Fact]
    public async Task Dang_ky_khong_chiem_ho_so_da_co_voi_email_khac()
    {
        // hồ sơ tồn tại do từng nộp đơn (chưa có tài khoản) -> chỉ liên kết khi đúng email
        var (_, code) = _app.NewStudent(withAccount: false, email: "that@test.vn");
        var c = await Client.As(_app, null);
        var r = await c.PostAsync("/Account/Register", RegisterForm(code, "ke-gian@test.vn"));
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Null(_app.Query(db => db.UserAccounts.SingleOrDefault(u => u.Username == code)));
    }

    [Fact]
    public async Task Dang_ky_khong_the_tao_tai_khoan_quan_tri()
    {
        // Mã SV trùng tên đăng nhập admin bị chặn, và vai trò luôn là Student
        var c = await Client.As(_app, null);
        var r = await c.PostAsync("/Account/Register", RegisterForm(Accounts.Admin, "x@test.vn"));
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal("Admin", _app.Query(db => db.UserAccounts.Single(u => u.Username == Accounts.Admin).Role));
    }

    [Fact]
    public async Task Admin_tat_dang_ky_thi_nut_va_trang_dang_ky_bi_chan()
    {
        var store = _app.Services.GetRequiredService<SiteSettingsStore>();
        var original = store.Current;
        try
        {
            var s = store.Current; s.AllowStudentRegistration = false; store.Save(s);

            var c = await Client.As(_app, null);
            var get = await c.Http.GetAsync("/Account/Register");
            Assert.Equal(HttpStatusCode.Redirect, get.StatusCode);
            Assert.Contains("/Account/Login", Client.Location(get));

            var code = TestApp.Uniq("TT");
            var post = await c.PostAsync("/Account/Register", RegisterForm(code, code + "@test.vn"));
            Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);
            Assert.Null(_app.Query(db => db.UserAccounts.SingleOrDefault(u => u.Username == code)));
            Assert.DoesNotContain("/Account/Register", await c.HtmlAsync("/Account/Login"));
        }
        finally { store.Save(original); }
    }
}
