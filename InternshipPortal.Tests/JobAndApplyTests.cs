using System.Net;
using InternshipPortal.Web.Models;

namespace InternshipPortal.Tests;

[Collection("app")]
public class JobAndApplyTests
{
    private readonly TestApp _app;
    public JobAndApplyTests(TestApp app) => _app = app;

    private static readonly byte[] Pdf = System.Text.Encoding.ASCII.GetBytes("%PDF-1.4\n%test\n");

    private Dictionary<string, string> JobForm(int companyId, string title) => new()
    {
        ["CompanyId"] = companyId.ToString(), ["Title"] = title, ["Major"] = "CNTT", ["Location"] = "Hà Nội",
        ["SalaryRange"] = "3 - 5 triệu", ["Quantity"] = "2", ["ExpireDate"] = DateTime.Now.AddDays(20).ToString("yyyy-MM-dd"),
        ["Description"] = "## Công việc\n- viết code", ["Requirements"] = "biết C#", ["IsActive"] = "true"
    };

    // ---------- Tin tuyển dụng ----------
    [Fact]
    public async Task Danh_sach_tin_tim_theo_tu_khoa_va_an_tin_da_dong()
    {
        var open = TestApp.Uniq("Mo");
        var closed = TestApp.Uniq("Dong");
        _app.NewJob(_app.TxaCompanyId, true, null, open);
        _app.NewJob(_app.TxaCompanyId, false, null, closed);
        var c = await Client.As(_app, null);

        var html = await c.HtmlAsync("/Job?keyword=" + open);
        Assert.Contains(open, html);
        Assert.DoesNotContain(closed, await c.HtmlAsync("/Job"));
        Assert.DoesNotContain(open, await c.HtmlAsync("/Job?keyword=khong-co-tu-khoa-nay-xyz"));
    }

    [Fact]
    public async Task Chi_tiet_tin_khong_ton_tai_tra_404()
    {
        var c = await Client.As(_app, null);
        Assert.Equal(HttpStatusCode.NotFound, (await c.Http.GetAsync("/Job/Detail/99999999")).StatusCode);
    }

    [Fact]
    public async Task Trang_khong_ton_tai_hien_trang_thong_bao_than_thien_va_van_giu_ma_404()
    {
        var c = await Client.As(_app, null);
        var r = await c.Http.GetAsync("/Job/Detail/99999999");
        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
        var html = await r.Content.ReadAsStringAsync();
        Assert.Contains("txa-status-page", html);
        Assert.Contains("data-status-code=\"404\"", html);
        Assert.Equal(HttpStatusCode.NotFound, (await c.Http.GetAsync("/duong-dan-khong-co")).StatusCode);
    }

    [Fact]
    public async Task Doanh_nghiep_dang_tin_cho_cong_ty_cua_minh_thanh_cong()
    {
        var c = await Client.As(_app, Accounts.Hr);
        var title = TestApp.Uniq("Tin moi ");
        var r = await c.PostAsync("/Job/Create", JobForm(_app.TxaCompanyId, title));
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Equal(1, _app.Query(db => db.JobPosts.Count(j => j.Title == title && j.CompanyId == _app.TxaCompanyId)));
    }

    [Fact]
    public async Task Doanh_nghiep_khong_dang_tin_thay_cong_ty_khac()
    {
        var c = await Client.As(_app, Accounts.Hr);
        var title = TestApp.Uniq("Gia mao ");
        var r = await c.PostAsync("/Job/Create", JobForm(_app.FptCompanyId, title));
        Assert.Equal(HttpStatusCode.OK, r.StatusCode); // form hiện lại kèm lỗi
        Assert.Equal(0, _app.Query(db => db.JobPosts.Count(j => j.Title == title)));
    }

    [Fact]
    public async Task Cong_ty_chua_duyet_khong_dang_duoc_tin()
    {
        var (companyId, hr) = _app.NewCompanyWithHr(approved: false);
        var c = await Client.As(_app, hr);
        var title = TestApp.Uniq("Chua duyet ");
        await c.PostAsync("/Job/Create", JobForm(companyId, title));
        Assert.Equal(0, _app.Query(db => db.JobPosts.Count(j => j.Title == title)));
    }

    [Fact]
    public async Task Doanh_nghiep_khong_sua_duoc_tin_cua_cong_ty_khac()
    {
        var job = _app.NewJob(_app.FptCompanyId, title: "Goc-" + TestApp.Uniq("x"));
        var original = _app.Query(db => db.JobPosts.Single(j => j.Id == job).Title);
        var c = await Client.As(_app, Accounts.Hr);

        var form = JobForm(_app.FptCompanyId, "Bi sua trom"); form["Id"] = job.ToString();
        await c.PostAsync("/Job/Edit/" + job, form);
        var form2 = JobForm(_app.TxaCompanyId, "Chuyen sang cty minh"); form2["Id"] = job.ToString();
        await c.PostAsync("/Job/Edit/" + job, form2);

        Assert.Equal(original, _app.Query(db => db.JobPosts.Single(j => j.Id == job).Title));
        Assert.Equal(_app.FptCompanyId, _app.Query(db => db.JobPosts.Single(j => j.Id == job).CompanyId));
    }

    [Fact]
    public async Task Doanh_nghiep_khong_dong_mo_duoc_tin_cua_cong_ty_khac()
    {
        var job = _app.NewJob(_app.FptCompanyId);
        var c = await Client.As(_app, Accounts.Hr);
        await c.PostAsync("/Job/ToggleStatus/" + job);
        Assert.True(_app.Query(db => db.JobPosts.Single(j => j.Id == job).IsActive));
    }

    [Fact]
    public async Task Doanh_nghiep_dong_mo_tin_cua_minh_duoc()
    {
        var job = _app.NewJob(_app.TxaCompanyId);
        var c = await Client.As(_app, Accounts.Hr);
        await c.PostAsync("/Job/ToggleStatus/" + job);
        Assert.False(_app.Query(db => db.JobPosts.Single(j => j.Id == job).IsActive));
    }

    [Fact]
    public async Task Sinh_vien_va_khach_khong_dang_tin_duoc()
    {
        var student = await Client.As(_app, Accounts.Student);
        Assert.True(Client.IsDenied(await student.PostAsync("/Job/Create", JobForm(_app.TxaCompanyId, "x"))));
        var guest = await Client.As(_app, null);
        Assert.True(Client.IsLoginRedirect(await guest.Http.PostAsync("/Job/Create", new FormUrlEncodedContent(JobForm(_app.TxaCompanyId, "y")))));
    }

    [Fact]
    public async Task Mo_ta_chua_script_khong_bi_in_nguyen_van_ra_trang()
    {
        var title = TestApp.Uniq("XSS ");
        var c = await Client.As(_app, Accounts.Admin);
        var form = JobForm(_app.TxaCompanyId, title);
        form["Description"] = "<script>window.__pwned=1</script><img src=x onerror=alert(1)>";
        var r = await c.PostAsync("/Job/Create", form);
        var id = _app.Query(db => db.JobPosts.Single(j => j.Title == title).Id);
        var html = await c.HtmlAsync("/Job/Detail/" + id);
        Assert.DoesNotContain("<script>window.__pwned", html);
        Assert.DoesNotContain("<img src=x onerror", html);
    }

    // ---------- Nộp hồ sơ ứng tuyển ----------
    private Dictionary<string, string> ApplyForm(int jobId, string code, string email = "sv@test.vn") => new()
    {
        ["JobPostId"] = jobId.ToString(), ["StudentCode"] = code, ["FullName"] = "Sinh Viên " + code,
        ["Major"] = "CNTT", ["Email"] = email, ["PhoneNumber"] = "0912345678"
    };

    [Fact]
    public async Task Sinh_vien_dang_nhap_nop_ho_so_kem_CV_pdf()
    {
        var (sid, code) = _app.NewStudent();
        var job = _app.NewJob(_app.TxaCompanyId);
        var c = await Client.As(_app, code);

        var r = await c.PostFileAsync("/Job/Apply", new Dictionary<string, string> { ["JobPostId"] = job.ToString() }, "CVFile", "cv.pdf", Pdf);
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);

        var app = _app.Query(db => db.Applications.Single(a => a.JobPostId == job && a.StudentProfileId == sid));
        Assert.Equal(ApplicationStatus.Pending, app.Status);
        var cv = _app.Query(db => db.StudentProfiles.Single(s => s.Id == sid).CVPath);
        Assert.StartsWith("/uploads/cvs/", cv);
        Assert.True(File.Exists(Path.Combine(_app.TempDir, "wwwroot", cv!.TrimStart('/').Replace('/', Path.DirectorySeparatorChar))));
    }

    [Fact]
    public async Task Nop_hai_lan_cung_mot_vi_tri_chi_tao_mot_don()
    {
        var (sid, code) = _app.NewStudent();
        var job = _app.NewJob(_app.TxaCompanyId);
        var c = await Client.As(_app, code);
        var form = new Dictionary<string, string> { ["JobPostId"] = job.ToString() };
        await c.PostFileAsync("/Job/Apply", form, "CVFile", "cv.pdf", Pdf);
        await c.PostFileAsync("/Job/Apply", form, "CVFile", "cv.pdf", Pdf);
        Assert.Equal(1, _app.Query(db => db.Applications.Count(a => a.JobPostId == job && a.StudentProfileId == sid)));
    }

    [Theory]
    [InlineData("virus.exe")]
    [InlineData("shell.php")]
    [InlineData("anh.png")]
    [InlineData("cv.pdf.exe")]
    public async Task File_CV_sai_dinh_dang_bi_tu_choi(string fileName)
    {
        var (sid, code) = _app.NewStudent();
        var job = _app.NewJob(_app.TxaCompanyId);
        var c = await Client.As(_app, code);
        await c.PostFileAsync("/Job/Apply", new Dictionary<string, string> { ["JobPostId"] = job.ToString() }, "CVFile", fileName, Pdf);
        Assert.Equal(0, _app.Query(db => db.Applications.Count(a => a.JobPostId == job && a.StudentProfileId == sid)));
    }

    [Fact]
    public async Task File_CV_qua_10MB_bi_tu_choi()
    {
        var (sid, code) = _app.NewStudent();
        var job = _app.NewJob(_app.TxaCompanyId);
        var c = await Client.As(_app, code);
        var big = new byte[10 * 1024 * 1024 + 1];
        await c.PostFileAsync("/Job/Apply", new Dictionary<string, string> { ["JobPostId"] = job.ToString() }, "CVFile", "big.pdf", big);
        Assert.Equal(0, _app.Query(db => db.Applications.Count(a => a.JobPostId == job && a.StudentProfileId == sid)));
    }

    [Theory]
    [InlineData(Accounts.Admin)]
    [InlineData(Accounts.Hr)]
    public async Task Admin_va_doanh_nghiep_khong_nop_ho_so_duoc(string user)
    {
        var job = _app.NewJob(_app.TxaCompanyId);
        var before = _app.Query(db => db.Applications.Count());
        var c = await Client.As(_app, user);
        await c.PostFileAsync("/Job/Apply", ApplyForm(job, TestApp.Uniq("AD")), "CVFile", "cv.pdf", Pdf);
        Assert.Equal(before, _app.Query(db => db.Applications.Count()));
    }

    [Fact]
    public async Task Khach_nop_ho_so_voi_email_sai_dinh_dang_bi_tu_choi()
    {
        var code = TestApp.Uniq("KH");
        var job = _app.NewJob(_app.TxaCompanyId);
        var c = await Client.As(_app, null);
        await c.PostFileAsync("/Job/Apply", ApplyForm(job, code, "khong-phai-email"), "CVFile", "cv.pdf", Pdf);
        Assert.Equal(0, _app.Query(db => db.Applications.Count(a => a.JobPostId == job)));
    }

    [Fact]
    public async Task Khach_nop_ho_so_Ngay_1_van_chay_va_tao_ho_so_sinh_vien()
    {
        var code = TestApp.Uniq("KH");
        var job = _app.NewJob(_app.TxaCompanyId);
        var c = await Client.As(_app, null);
        await c.PostFileAsync("/Job/Apply", ApplyForm(job, code), "CVFile", "cv.docx", Pdf);
        Assert.Equal(1, _app.Query(db => db.Applications.Count(a => a.JobPostId == job && a.StudentProfile.StudentCode == code)));
    }

    [Fact]
    public async Task Khong_nop_ho_so_vao_tin_da_dong()
    {
        var (sid, code) = _app.NewStudent();
        var job = _app.NewJob(_app.TxaCompanyId, active: false);
        var c = await Client.As(_app, code);
        await c.PostFileAsync("/Job/Apply", new Dictionary<string, string> { ["JobPostId"] = job.ToString() }, "CVFile", "cv.pdf", Pdf);
        Assert.Equal(0, _app.Query(db => db.Applications.Count(a => a.JobPostId == job)));
    }

    [Fact]
    public async Task Khong_nop_ho_so_vao_tin_da_het_han()
    {
        var (sid, code) = _app.NewStudent();
        var job = _app.NewJob(_app.TxaCompanyId, true, DateTime.Now.AddDays(-3));
        var c = await Client.As(_app, code);
        await c.PostFileAsync("/Job/Apply", new Dictionary<string, string> { ["JobPostId"] = job.ToString() }, "CVFile", "cv.pdf", Pdf);
        Assert.Equal(0, _app.Query(db => db.Applications.Count(a => a.JobPostId == job)));
    }

    [Fact]
    public async Task Khach_khong_duoc_ghi_de_ho_so_cua_sinh_vien_da_co_tai_khoan()
    {
        var (sid, code) = _app.NewStudent(withAccount: true, email: "that@test.vn");
        var job = _app.NewJob(_app.TxaCompanyId);
        var c = await Client.As(_app, null);
        var form = ApplyForm(job, code, "ke-gian@test.vn"); form["FullName"] = "KE GIAN DOI TEN";
        await c.PostFileAsync("/Job/Apply", form, "CVFile", "cv.pdf", Pdf);

        var profile = _app.Query(db => db.StudentProfiles.Single(s => s.Id == sid));
        Assert.Equal("that@test.vn", profile.Email);
        Assert.NotEqual("KE GIAN DOI TEN", profile.FullName);
        Assert.Null(profile.CVPath);
    }
}
