using System.Net;
using Microsoft.Extensions.DependencyInjection;
using InternshipPortal.Web.Infrastructure;
using InternshipPortal.Web.Models;

namespace InternshipPortal.Tests;

[Collection("app")]
public class SettingsDashboardTests
{
    private readonly TestApp _app;
    public SettingsDashboardTests(TestApp app) => _app = app;

    private SiteSettingsStore Store => _app.Services.GetRequiredService<SiteSettingsStore>();

    private static Dictionary<string, string> Form(string org = "Đơn vị thử", bool allowReg = true, bool annEnabled = false, string ann = "", string min = "4") => new()
    {
        ["OrganizationName"] = org, ["AllowStudentRegistration"] = allowReg ? "true" : "false",
        ["AnnouncementEnabled"] = annEnabled ? "true" : "false", ["AnnouncementText"] = ann, ["MinReportsForEvaluation"] = min
    };

    [Fact]
    public async Task Admin_luu_cai_dat_va_ten_don_vi_hien_tren_trang()
    {
        var original = Store.Current;
        try
        {
            var admin = await Client.As(_app, Accounts.Admin);
            var org = TestApp.Uniq("Truong");
            var r = await admin.PostAsync("/Settings/Save", Form(org, true, true, "Bảo trì tối nay", "6"));
            Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);

            Assert.Equal(org, Store.Current.OrganizationName);
            Assert.Equal(6, Store.Current.MinReportsForEvaluation);
            var html = await (await Client.As(_app, null)).HtmlAsync("/Job");
            Assert.Contains(org, html);
            Assert.Contains("txa-ui.js", html);

            // thông báo toàn hệ thống hiện khi bật, mất khi tắt
            Assert.Contains("site-banner", html);
            await admin.PostAsync("/Settings/Save", Form(org, true, false, "Bảo trì tối nay", "6"));
            Assert.DoesNotContain("site-banner", await (await Client.As(_app, null)).HtmlAsync("/Job"));
        }
        finally { Store.Save(original); }
    }

    [Fact]
    public async Task Cai_dat_chi_Admin_luu_duoc()
    {
        var before = Store.Current.OrganizationName;
        var r = await (await Client.As(_app, Accounts.Hr)).PostAsync("/Settings/Save", Form("Bi doi trom"));
        Assert.True(Client.IsDenied(r));
        var r2 = await (await Client.As(_app, Accounts.Student)).PostAsync("/Settings/Save", Form("Bi doi trom"));
        Assert.True(Client.IsDenied(r2));
        Assert.Equal(before, Store.Current.OrganizationName);
    }

    [Theory]
    [InlineData("53")]
    [InlineData("-1")]
    public async Task So_tuan_toi_thieu_ngoai_0_52_bi_tu_choi(string min)
    {
        var before = Store.Current.MinReportsForEvaluation;
        await (await Client.As(_app, Accounts.Admin)).PostAsync("/Settings/Save", Form(min: min));
        Assert.Equal(before, Store.Current.MinReportsForEvaluation);
    }

    [Fact]
    public async Task Bat_thong_bao_ma_de_trong_noi_dung_bi_tu_choi()
    {
        var original = Store.Current;
        try
        {
            await (await Client.As(_app, Accounts.Admin)).PostAsync("/Settings/Save", Form(annEnabled: true, ann: "   "));
            Assert.False(Store.Current.AnnouncementEnabled);
        }
        finally { Store.Save(original); }
    }

    [Fact]
    public async Task Ten_don_vi_chua_ma_html_duoc_ma_hoa()
    {
        var original = Store.Current;
        try
        {
            await (await Client.As(_app, Accounts.Admin)).PostAsync("/Settings/Save", Form("<script>window.__org=1</script>"));
            Assert.DoesNotContain("<script>window.__org", await (await Client.As(_app, null)).HtmlAsync("/Job"));
        }
        finally { Store.Save(original); }
    }

    [Fact]
    public async Task Dashboard_Admin_mo_duoc_va_co_du_lieu_that()
    {
        var r = await (await Client.As(_app, Accounts.Admin)).Http.GetAsync("/Dashboard");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var html = await r.Content.ReadAsStringAsync();
        Assert.Contains("TXA Studio", html);
        Assert.Contains("ResetDemoData", html);
    }

    [Fact]
    public async Task Khoi_phuc_du_lieu_mau_xoa_du_lieu_them_va_nap_lai_seed()
    {
        var extra = TestApp.Uniq("CtyThem");
        _app.Db(db => db.Companies.Add(new Company { CompanyName = extra }));

        var denied = await (await Client.As(_app, Accounts.Hr)).PostAsync("/Dashboard/ResetDemoData");
        Assert.True(Client.IsDenied(denied));
        Assert.Equal(1, _app.Query(db => db.Companies.Count(c => c.CompanyName == extra))); // HR không xóa được

        var r = await (await Client.As(_app, Accounts.Admin)).PostAsync("/Dashboard/ResetDemoData");
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);

        Assert.Equal(0, _app.Query(db => db.Companies.Count(c => c.CompanyName == extra)));
        Assert.Equal(3, _app.Query(db => db.Companies.Count()));
        // seed được nạp lại: đăng nhập được 3 tài khoản mẫu, sinh viên test có 4 báo cáo và điểm DN 8,5 chưa công bố
        foreach (var user in new[] { Accounts.Admin, Accounts.Hr, Accounts.Student })
            await Client.As(_app, user);
        var app = _app.Query(db => db.Applications.Single(a => a.StudentProfile.StudentCode == "test"));
        Assert.Equal(ApplicationStatus.Accepted, app.Status);
        Assert.Equal(4, _app.Query(db => db.WeeklyReports.Count(w => w.ApplicationId == app.Id)));
        var ev = _app.Query(db => db.Evaluations.Single(e => e.ApplicationId == app.Id));
        Assert.Equal(8.5, ev.CompanyScore);
        Assert.Null(ev.SchoolScore);
        Assert.False(ev.IsPublished);
    }
}
