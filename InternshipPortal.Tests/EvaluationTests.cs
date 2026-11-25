using Microsoft.Extensions.DependencyInjection;
using InternshipPortal.Web.Infrastructure;
using InternshipPortal.Web.Models;

namespace InternshipPortal.Tests;

[Collection("app")]
public class EvaluationTests
{
    private readonly TestApp _app;
    public EvaluationTests(TestApp app) => _app = app;

    private SiteSettingsStore Store => _app.Services.GetRequiredService<SiteSettingsStore>();
    private Evaluation? EvalOf(int appId) => _app.Query(db => db.Evaluations.SingleOrDefault(e => e.ApplicationId == appId));
    private static Dictionary<string, string> Score(int appId, string score, string comment = "ok") =>
        new() { ["applicationId"] = appId.ToString(), ["score"] = score, ["comment"] = comment };

    [Fact]
    public async Task Chua_du_bao_cao_thi_khong_cham_duoc()
    {
        var min = Store.Current.MinReportsForEvaluation;
        var a = _app.NewAcceptedStudent(_app.TxaCompanyId, Math.Max(0, min - 1));
        if (min == 0) return;
        await (await Client.As(_app, Accounts.Hr)).PostAsync("/Evaluation/SubmitScore", Score(a.AppId, "9"));
        await (await Client.As(_app, Accounts.Admin)).PostAsync("/Evaluation/SubmitScore", Score(a.AppId, "9"));
        Assert.Null(EvalOf(a.AppId));
    }

    [Fact]
    public async Task Du_bao_cao_thi_cham_duoc_va_tong_ket_40_60()
    {
        var a = _app.NewAcceptedStudent(_app.TxaCompanyId, Store.Current.MinReportsForEvaluation);
        await (await Client.As(_app, Accounts.Hr)).PostAsync("/Evaluation/SubmitScore", Score(a.AppId, "9", "DN tốt"));
        var e1 = EvalOf(a.AppId)!;
        Assert.Equal(9, e1.CompanyScore);
        Assert.Null(e1.SchoolScore);
        Assert.Null(e1.FinalScore); // chưa đủ hai đầu điểm

        await (await Client.As(_app, Accounts.Admin)).PostAsync("/Evaluation/SubmitScore", Score(a.AppId, "7,5", "Khoa khá"));
        var e2 = EvalOf(a.AppId)!;
        Assert.Equal(7.5, e2.SchoolScore);
        Assert.Equal(9, e2.CompanyScore);                 // điểm doanh nghiệp không bị Khoa ghi đè
        Assert.Equal(8.1, e2.FinalScore);                 // 9*0.4 + 7.5*0.6
        Assert.Equal("DN tốt", e2.CompanyComment);
        Assert.Equal("Khoa khá", e2.SchoolComment);
    }

    [Theory]
    [InlineData("11")]
    [InlineData("-1")]
    [InlineData("10.01")]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("NaN")]
    public async Task Diem_ngoai_thang_0_10_hoac_khong_phai_so_bi_tu_choi(string score)
    {
        var a = _app.NewAcceptedStudent(_app.TxaCompanyId, Store.Current.MinReportsForEvaluation);
        await (await Client.As(_app, Accounts.Admin)).PostAsync("/Evaluation/SubmitScore", Score(a.AppId, score));
        Assert.Null(EvalOf(a.AppId));
    }

    [Theory]
    [InlineData("0", 0.0)]
    [InlineData("10", 10.0)]
    [InlineData("5.5", 5.5)]
    public async Task Diem_bien_hop_le_duoc_luu(string score, double expected)
    {
        var a = _app.NewAcceptedStudent(_app.TxaCompanyId, Store.Current.MinReportsForEvaluation);
        await (await Client.As(_app, Accounts.Admin)).PostAsync("/Evaluation/SubmitScore", Score(a.AppId, score));
        Assert.Equal(expected, EvalOf(a.AppId)!.SchoolScore);
    }

    [Fact]
    public async Task Doanh_nghiep_khong_cham_sinh_vien_cua_cong_ty_khac()
    {
        var a = _app.NewAcceptedStudent(_app.FptCompanyId, Store.Current.MinReportsForEvaluation);
        await (await Client.As(_app, Accounts.Hr)).PostAsync("/Evaluation/SubmitScore", Score(a.AppId, "9"));
        Assert.Null(EvalOf(a.AppId));
    }

    [Fact]
    public async Task Chi_cham_don_da_trung_tuyen()
    {
        var s = _app.NewStudent();
        var app = _app.NewApplication(_app.NewJob(_app.TxaCompanyId), s.StudentId, ApplicationStatus.Pending, Store.Current.MinReportsForEvaluation);
        await (await Client.As(_app, Accounts.Admin)).PostAsync("/Evaluation/SubmitScore", Score(app, "9"));
        Assert.Null(EvalOf(app));
    }

    [Fact]
    public async Task Sinh_vien_khong_cham_diem_duoc()
    {
        var a = _app.NewAcceptedStudent(_app.TxaCompanyId, Store.Current.MinReportsForEvaluation);
        var r = await (await Client.As(_app, a.Code)).PostAsync("/Evaluation/SubmitScore", Score(a.AppId, "10"));
        Assert.True(Client.IsDenied(r));
        Assert.Null(EvalOf(a.AppId));
    }

    [Fact]
    public async Task Sinh_vien_chi_thay_ket_qua_sau_khi_Admin_cong_bo()
    {
        var a = _app.NewAcceptedStudent(_app.TxaCompanyId, Store.Current.MinReportsForEvaluation);
        var marker = TestApp.Uniq("NhanXet");
        var admin = await Client.As(_app, Accounts.Admin);
        await (await Client.As(_app, Accounts.Hr)).PostAsync("/Evaluation/SubmitScore", Score(a.AppId, "9", marker));
        await admin.PostAsync("/Evaluation/SubmitScore", Score(a.AppId, "8"));

        var student = await Client.As(_app, a.Code);
        Assert.DoesNotContain(marker, await student.HtmlAsync("/StudentPortal"));   // chưa công bố

        await admin.PostAsync("/Evaluation/Publish", new Dictionary<string, string> { ["applicationId"] = a.AppId.ToString(), ["publish"] = "true" });
        var html = await student.HtmlAsync("/StudentPortal");
        Assert.Contains(marker, html);
        Assert.Contains(">8.4<", html);                                              // 9*0.4 + 8*0.6

        await admin.PostAsync("/Evaluation/Publish", new Dictionary<string, string> { ["applicationId"] = a.AppId.ToString(), ["publish"] = "false" });
        Assert.DoesNotContain(marker, await student.HtmlAsync("/StudentPortal"));    // thu hồi
    }

    [Fact]
    public async Task Doanh_nghiep_khong_cong_bo_duoc()
    {
        var a = _app.NewAcceptedStudent(_app.TxaCompanyId, Store.Current.MinReportsForEvaluation);
        _app.Db(db => db.Evaluations.Add(new Evaluation { ApplicationId = a.AppId, CompanyScore = 9, SchoolScore = 9, FinalScore = 9 }));
        var r = await (await Client.As(_app, Accounts.Hr)).PostAsync("/Evaluation/Publish", new Dictionary<string, string> { ["applicationId"] = a.AppId.ToString(), ["publish"] = "true" });
        Assert.True(Client.IsDenied(r));
        Assert.False(EvalOf(a.AppId)!.IsPublished);
    }

    [Fact]
    public async Task Khong_cong_bo_khi_chua_du_hai_dau_diem()
    {
        var a = _app.NewAcceptedStudent(_app.TxaCompanyId, Store.Current.MinReportsForEvaluation);
        _app.Db(db => db.Evaluations.Add(new Evaluation { ApplicationId = a.AppId, CompanyScore = 9 }));
        await (await Client.As(_app, Accounts.Admin)).PostAsync("/Evaluation/Publish", new Dictionary<string, string> { ["applicationId"] = a.AppId.ToString(), ["publish"] = "true" });
        Assert.False(EvalOf(a.AppId)!.IsPublished);
    }

    [Fact]
    public async Task Sua_diem_sau_khi_cong_bo_thi_tu_thu_hoi()
    {
        var a = _app.NewAcceptedStudent(_app.TxaCompanyId, Store.Current.MinReportsForEvaluation);
        _app.Db(db => db.Evaluations.Add(new Evaluation { ApplicationId = a.AppId, CompanyScore = 9, SchoolScore = 9, FinalScore = 9, IsPublished = true, PublishedAt = DateTime.Now }));
        await (await Client.As(_app, Accounts.Hr)).PostAsync("/Evaluation/SubmitScore", Score(a.AppId, "5"));
        var e = EvalOf(a.AppId)!;
        Assert.False(e.IsPublished);
        Assert.Null(e.PublishedAt);
        Assert.Equal(7.4, e.FinalScore); // 5*0.4 + 9*0.6
    }

    [Fact]
    public async Task Admin_dat_so_tuan_toi_thieu_bang_0_thi_cham_duoc_ngay()
    {
        var original = Store.Current;
        try
        {
            var s = Store.Current; s.MinReportsForEvaluation = 0; Store.Save(s);
            var a = _app.NewAcceptedStudent(_app.TxaCompanyId, 0);
            await (await Client.As(_app, Accounts.Admin)).PostAsync("/Evaluation/SubmitScore", Score(a.AppId, "7"));
            Assert.Equal(7, EvalOf(a.AppId)!.SchoolScore);
        }
        finally { Store.Save(original); }
    }

    [Fact]
    public async Task Moi_don_chi_co_mot_ban_danh_gia()
    {
        var a = _app.NewAcceptedStudent(_app.TxaCompanyId, Store.Current.MinReportsForEvaluation);
        var admin = await Client.As(_app, Accounts.Admin);
        await admin.PostAsync("/Evaluation/SubmitScore", Score(a.AppId, "6"));
        await admin.PostAsync("/Evaluation/SubmitScore", Score(a.AppId, "7"));
        Assert.Equal(1, _app.Query(db => db.Evaluations.Count(e => e.ApplicationId == a.AppId)));
        Assert.Equal(7, EvalOf(a.AppId)!.SchoolScore);
    }
}
