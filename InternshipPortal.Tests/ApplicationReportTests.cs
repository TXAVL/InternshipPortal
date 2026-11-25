using System.Net;
using ClosedXML.Excel;
using InternshipPortal.Web.Models;

namespace InternshipPortal.Tests;

[Collection("app")]
public class ApplicationReportTests
{
    private readonly TestApp _app;
    public ApplicationReportTests(TestApp app) => _app = app;

    // ---------- Quản lý hồ sơ ----------
    [Fact]
    public async Task Doanh_nghiep_chi_thay_ho_so_vao_tin_cua_minh()
    {
        var mineStudent = _app.NewStudent(); var otherStudent = _app.NewStudent();
        _app.NewApplication(_app.NewJob(_app.TxaCompanyId), mineStudent.StudentId, ApplicationStatus.Pending);
        _app.NewApplication(_app.NewJob(_app.FptCompanyId), otherStudent.StudentId, ApplicationStatus.Pending);

        var html = await (await Client.As(_app, Accounts.Hr)).HtmlAsync("/Application");
        Assert.Contains(mineStudent.Code, html);
        Assert.DoesNotContain(otherStudent.Code, html);

        var adminHtml = await (await Client.As(_app, Accounts.Admin)).HtmlAsync("/Application");
        Assert.Contains(mineStudent.Code, adminHtml);
        Assert.Contains(otherStudent.Code, adminHtml);
    }

    [Fact]
    public async Task Doanh_nghiep_khong_duyet_duoc_ho_so_cua_cong_ty_khac()
    {
        var s = _app.NewStudent();
        var app = _app.NewApplication(_app.NewJob(_app.FptCompanyId), s.StudentId, ApplicationStatus.Pending);
        var c = await Client.As(_app, Accounts.Hr);
        await c.PostAsync("/Application/UpdateStatus", new Dictionary<string, string> { ["id"] = app.ToString(), ["status"] = "Accepted" });
        Assert.Equal(ApplicationStatus.Pending, _app.Query(db => db.Applications.Single(a => a.Id == app).Status));
    }

    [Theory]
    [InlineData("Interview", ApplicationStatus.Interview)]
    [InlineData("Accepted", ApplicationStatus.Accepted)]
    [InlineData("Rejected", ApplicationStatus.Rejected)]
    public async Task Admin_doi_trang_thai_ho_so(string status, ApplicationStatus expected)
    {
        var s = _app.NewStudent();
        var app = _app.NewApplication(_app.NewJob(_app.TxaCompanyId), s.StudentId, ApplicationStatus.Pending);
        var c = await Client.As(_app, Accounts.Admin);
        await c.PostAsync("/Application/UpdateStatus", new Dictionary<string, string> { ["id"] = app.ToString(), ["status"] = status });
        Assert.Equal(expected, _app.Query(db => db.Applications.Single(a => a.Id == app).Status));
    }

    [Theory]
    [InlineData("99")]
    [InlineData("-1")]
    [InlineData("Hacked")]
    public async Task Trang_thai_khong_hop_le_khong_duoc_luu(string status)
    {
        var s = _app.NewStudent();
        var app = _app.NewApplication(_app.NewJob(_app.TxaCompanyId), s.StudentId, ApplicationStatus.Accepted);
        var c = await Client.As(_app, Accounts.Admin);
        await c.PostAsync("/Application/UpdateStatus", new Dictionary<string, string> { ["id"] = app.ToString(), ["status"] = status });
        var saved = _app.Query(db => db.Applications.Single(a => a.Id == app).Status);
        Assert.True(Enum.IsDefined(saved), $"Trạng thái lưu vào DB là giá trị không hợp lệ: {(int)saved}");
        Assert.Equal(ApplicationStatus.Accepted, saved); // giữ nguyên, không bị reset về Pending
    }

    [Fact]
    public async Task Xuat_Excel_Admin_co_du_cot_diem_va_chu_Chua_cham()
    {
        var withScore = _app.NewAcceptedStudent(_app.TxaCompanyId, 4);
        var noScore = _app.NewAcceptedStudent(_app.TxaCompanyId, 0);
        _app.Db(db => db.Evaluations.Add(new Evaluation { ApplicationId = withScore.AppId, CompanyScore = 9, SchoolScore = 8, FinalScore = 8.4 }));

        var c = await Client.As(_app, Accounts.Admin);
        var r = await c.Http.GetAsync("/Application/ExportExcel");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Contains("spreadsheetml", r.Content.Headers.ContentType!.MediaType);

        using var wb = new XLWorkbook(await r.Content.ReadAsStreamAsync());
        var ws = wb.Worksheet(1);
        Assert.Equal("Điểm Doanh nghiệp (40%)", ws.Cell(4, 12).GetString());
        Assert.Equal("Điểm Khoa (60%)", ws.Cell(4, 13).GetString());
        Assert.Equal("Điểm Tổng Kết", ws.Cell(4, 14).GetString());

        string[] RowOf(string code)
        {
            var row = ws.RowsUsed().First(x => x.Cell(2).GetString() == code);
            return new[] { row.Cell(12).GetString(), row.Cell(13).GetString(), row.Cell(14).GetString() };
        }
        Assert.Equal(new[] { "9", "8", "8.4" }, RowOf(withScore.Code));
        Assert.Equal(new[] { "Chưa chấm", "Chưa chấm", "Chưa chấm" }, RowOf(noScore.Code));
    }

    // ---------- Báo cáo tuần của sinh viên ----------
    [Fact]
    public async Task Don_chua_trung_tuyen_khong_nop_duoc_nhat_ky()
    {
        var s = _app.NewStudent();
        var app = _app.NewApplication(_app.NewJob(_app.TxaCompanyId), s.StudentId, ApplicationStatus.Pending);
        var c = await Client.As(_app, s.Code);
        await c.PostAsync("/StudentPortal/SubmitReport", new Dictionary<string, string> { ["ApplicationId"] = app.ToString(), ["TasksDone"] = "làm việc" });
        Assert.Equal(0, _app.Query(db => db.WeeklyReports.Count(r => r.ApplicationId == app)));
    }

    [Fact]
    public async Task Nop_bao_cao_tuan_dau_tien_va_chan_nop_lan_hai_trong_cung_tuan()
    {
        var a = _app.NewAcceptedStudent(_app.TxaCompanyId, 0);
        var c = await Client.As(_app, a.Code);
        var form = new Dictionary<string, string> { ["ApplicationId"] = a.AppId.ToString(), ["TasksDone"] = "Tuần đầu", ["NextWeekPlan"] = "Tiếp" };
        await c.PostAsync("/StudentPortal/SubmitReport", form);
        await c.PostAsync("/StudentPortal/SubmitReport", form);

        var reports = _app.Query(db => db.WeeklyReports.Where(r => r.ApplicationId == a.AppId).ToList());
        Assert.Single(reports);
        Assert.Equal(1, reports[0].WeekNumber);
    }

    [Fact]
    public async Task Bao_cao_tuan_tiep_theo_duoc_danh_so_tu_dong_khi_sang_tuan_moi()
    {
        var a = _app.NewAcceptedStudent(_app.TxaCompanyId, 2); // 2 báo cáo cũ, tuần hiện tại còn trống
        var c = await Client.As(_app, a.Code);
        await c.PostAsync("/StudentPortal/SubmitReport", new Dictionary<string, string> { ["ApplicationId"] = a.AppId.ToString(), ["TasksDone"] = "Tuần mới" });
        Assert.Equal(new[] { 1, 2, 3 }, _app.Query(db => db.WeeklyReports.Where(r => r.ApplicationId == a.AppId).Select(r => r.WeekNumber).OrderBy(x => x).ToArray()));
    }

    [Fact]
    public async Task Noi_dung_bao_cao_rong_bi_tu_choi()
    {
        var a = _app.NewAcceptedStudent(_app.TxaCompanyId, 0);
        var c = await Client.As(_app, a.Code);
        await c.PostAsync("/StudentPortal/SubmitReport", new Dictionary<string, string> { ["ApplicationId"] = a.AppId.ToString(), ["TasksDone"] = "   " });
        Assert.Equal(0, _app.Query(db => db.WeeklyReports.Count(r => r.ApplicationId == a.AppId)));
    }

    [Fact]
    public async Task Sinh_vien_khong_nop_bao_cao_thay_sinh_vien_khac()
    {
        var victim = _app.NewAcceptedStudent(_app.TxaCompanyId, 0);
        var attacker = _app.NewStudent();
        var c = await Client.As(_app, attacker.Code);
        await c.PostAsync("/StudentPortal/SubmitReport", new Dictionary<string, string> { ["ApplicationId"] = victim.AppId.ToString(), ["TasksDone"] = "mạo danh" });
        Assert.Equal(0, _app.Query(db => db.WeeklyReports.Count(r => r.ApplicationId == victim.AppId)));
    }

    [Fact]
    public async Task Noi_dung_bao_cao_chua_script_duoc_ma_hoa_khi_hien_thi()
    {
        var a = _app.NewAcceptedStudent(_app.TxaCompanyId, 0);
        var c = await Client.As(_app, a.Code);
        await c.PostAsync("/StudentPortal/SubmitReport", new Dictionary<string, string> { ["ApplicationId"] = a.AppId.ToString(), ["TasksDone"] = "<script>window.__x=1</script>" });
        var student = await c.HtmlAsync("/StudentPortal");
        Assert.DoesNotContain("<script>window.__x", student);
        var admin = await (await Client.As(_app, Accounts.Admin)).HtmlAsync("/Report");
        Assert.DoesNotContain("<script>window.__x", admin);
    }

    // ---------- Nhận xét báo cáo ----------
    private int ReportOf(int companyId, out string code)
    {
        var a = _app.NewAcceptedStudent(companyId, 1);
        code = a.Code;
        return _app.Query(db => db.WeeklyReports.Single(r => r.ApplicationId == a.AppId).Id);
    }

    [Fact]
    public async Task Hai_ben_nhan_xet_doc_lap_khong_ghi_de_nhau()
    {
        var report = ReportOf(_app.TxaCompanyId, out _);
        await (await Client.As(_app, Accounts.Admin)).PostAsync("/Report/AddFeedback", new Dictionary<string, string> { ["reportId"] = report.ToString(), ["feedback"] = "Khoa nhận xét" });
        await (await Client.As(_app, Accounts.Hr)).PostAsync("/Report/AddFeedback", new Dictionary<string, string> { ["reportId"] = report.ToString(), ["feedback"] = "Doanh nghiệp nhận xét" });
        var saved = _app.Query(db => db.WeeklyReports.Single(r => r.Id == report));
        Assert.Equal("Khoa nhận xét", saved.MentorFeedback);
        Assert.Equal("Doanh nghiệp nhận xét", saved.CompanyFeedback);
    }

    [Fact]
    public async Task Doanh_nghiep_khong_nhan_xet_bao_cao_cua_cong_ty_khac()
    {
        var report = ReportOf(_app.FptCompanyId, out _);
        var r = await (await Client.As(_app, Accounts.Hr)).PostAsync("/Report/AddFeedback", new Dictionary<string, string> { ["reportId"] = report.ToString(), ["feedback"] = "chen ngang" });
        Assert.Null(_app.Query(db => db.WeeklyReports.Single(x => x.Id == report).CompanyFeedback));
    }

    [Fact]
    public async Task Doanh_nghiep_khong_thay_nhat_ky_cua_cong_ty_khac()
    {
        ReportOf(_app.TxaCompanyId, out var mine);
        ReportOf(_app.FptCompanyId, out var other);
        var html = await (await Client.As(_app, Accounts.Hr)).HtmlAsync("/Report");
        Assert.Contains(mine, html);
        Assert.DoesNotContain(other, html);
    }

    [Fact]
    public async Task Nhan_xet_rong_bi_tu_choi_va_sinh_vien_khong_nhan_xet_duoc()
    {
        var report = ReportOf(_app.TxaCompanyId, out _);
        await (await Client.As(_app, Accounts.Admin)).PostAsync("/Report/AddFeedback", new Dictionary<string, string> { ["reportId"] = report.ToString(), ["feedback"] = "  " });
        Assert.Null(_app.Query(db => db.WeeklyReports.Single(x => x.Id == report).MentorFeedback));

        var s = _app.NewStudent();
        var denied = await (await Client.As(_app, s.Code)).PostAsync("/Report/AddFeedback", new Dictionary<string, string> { ["reportId"] = report.ToString(), ["feedback"] = "sv chen" });
        Assert.True(Client.IsDenied(denied));
    }
}
