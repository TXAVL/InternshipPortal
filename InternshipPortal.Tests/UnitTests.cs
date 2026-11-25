using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using InternshipPortal.Web.Infrastructure;
using InternshipPortal.Web.Models;
using InternshipPortal.Web.Models.ViewModels;

namespace InternshipPortal.Tests;

/// <summary>Test logic thuần, không cần chạy web hay DB.</summary>
public class UnitTests
{
    [Theory]
    [InlineData(8.5, 9.0, 8.8)]
    [InlineData(10, 10, 10)]
    [InlineData(0, 0, 0)]
    [InlineData(9, 7.5, 8.1)]
    [InlineData(5, 9, 7.4)]
    public void Tong_ket_la_40_phan_tram_doanh_nghiep_cong_60_phan_tram_khoa(double company, double school, double expected)
    {
        var e = new Evaluation { CompanyScore = company, SchoolScore = school };
        e.Recalculate();
        Assert.Equal(expected, e.FinalScore);
    }

    [Theory]
    [InlineData(8.5, null)]
    [InlineData(null, 9.0)]
    [InlineData(null, null)]
    public void Thieu_mot_dau_diem_thi_chua_co_tong_ket(double? company, double? school)
    {
        var e = new Evaluation { CompanyScore = company, SchoolScore = school, FinalScore = 7 };
        e.Recalculate();
        Assert.Null(e.FinalScore);
    }

    [Fact]
    public void Trong_so_cong_lai_bang_1()
    {
        Assert.Equal(1.0, Evaluation.CompanyWeight + Evaluation.SchoolWeight, 10);
    }

    [Theory]
    [InlineData("2026-11-23", "2026-11-23")] // thứ Hai
    [InlineData("2026-11-25", "2026-11-23")] // thứ Tư
    [InlineData("2026-11-29", "2026-11-23")] // Chủ nhật vẫn thuộc tuần của thứ Hai trước đó
    [InlineData("2026-11-30", "2026-11-30")] // thứ Hai tuần sau
    public void Tuan_lich_bat_dau_tu_thu_hai(string date, string monday)
    {
        Assert.Equal(DateTime.Parse(monday), ReportWeek.StartOfWeek(DateTime.Parse(date).AddHours(15)));
    }

    [Fact]
    public void Da_nop_bao_cao_tuan_nay_chi_khi_co_bao_cao_tu_thu_hai_gan_nhat()
    {
        var thisWeek = new Application { WeeklyReports = { new WeeklyReport { SubmittedAt = ReportWeek.StartOfWeek(DateTime.Now).AddMinutes(1) } } };
        var lastWeek = new Application { WeeklyReports = { new WeeklyReport { SubmittedAt = ReportWeek.StartOfWeek(DateTime.Now).AddMinutes(-1) } } };
        Assert.True(ReportWeek.HasSubmittedThisWeek(thisWeek));
        Assert.False(ReportWeek.HasSubmittedThisWeek(lastWeek));
        Assert.False(ReportWeek.HasSubmittedThisWeek(new Application()));
        Assert.Equal(DayOfWeek.Monday, ReportWeek.NextWindowOpens().DayOfWeek);
        Assert.True(ReportWeek.NextWindowOpens() > DateTime.Now);
    }

    [Fact]
    public void Co_dung_15_theme_ma_khong_trung_va_mac_dinh_hop_le()
    {
        Assert.Equal(15, ThemeCatalog.All.Count);
        Assert.Equal(15, ThemeCatalog.All.Select(t => t.Id).Distinct().Count());
        Assert.True(ThemeCatalog.IsValid(ThemeCatalog.DefaultId));
        Assert.False(ThemeCatalog.IsValid("khong-co"));
        Assert.Equal(ThemeCatalog.DefaultId, ThemeCatalog.Find("khong-co").Id);
        Assert.Contains(ThemeCatalog.All, t => t.Id == "tet");
        Assert.Equal(2, ThemeCatalog.All.Count(t => t.Dark));
    }

    // ---------- SiteSettingsStore ----------
    private sealed class FakeEnv : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "t";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = "";
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static SiteSettingsStore NewStore(string path) => new(new FakeEnv(),
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["SiteSettings:Path"] = path }).Build());

    private static string TempFile() => Path.Combine(Path.GetTempPath(), "ip-unit-" + Guid.NewGuid().ToString("N")[..8], "s.json");

    [Fact]
    public void Cai_dat_mac_dinh_khi_chua_co_file()
    {
        var s = NewStore(TempFile()).Current;
        Assert.Equal(SiteSettings.DefaultOrganizationName, s.OrganizationName);
        Assert.True(s.AllowStudentRegistration);
        Assert.False(s.AnnouncementEnabled);
        Assert.Equal(4, s.MinReportsForEvaluation);
    }

    [Fact]
    public void Luu_roi_nap_lai_van_giu_gia_tri_va_ten_rong_ve_mac_dinh_va_so_tuan_duoc_gioi_han()
    {
        var path = TempFile();
        try
        {
            NewStore(path).Save(new SiteSettings { OrganizationName = "  ", AllowStudentRegistration = false, AnnouncementEnabled = true, AnnouncementText = "  hi  ", MinReportsForEvaluation = 999 });
            var reloaded = NewStore(path).Current;
            Assert.Equal(SiteSettings.DefaultOrganizationName, reloaded.OrganizationName);
            Assert.False(reloaded.AllowStudentRegistration);
            Assert.Equal("hi", reloaded.AnnouncementText);
            Assert.Equal(52, reloaded.MinReportsForEvaluation);
        }
        finally { try { Directory.Delete(Path.GetDirectoryName(path)!, true); } catch (Exception) { } }
    }

    [Fact]
    public void File_cai_dat_hong_thi_dung_mac_dinh_khong_lam_sap_ung_dung()
    {
        var path = TempFile();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{ day khong phai json");
        try { Assert.Equal(SiteSettings.DefaultOrganizationName, NewStore(path).Current.OrganizationName); }
        finally { try { Directory.Delete(Path.GetDirectoryName(path)!, true); } catch (Exception) { } }
    }

    [Fact]
    public void Current_tra_ban_sao_nen_sua_ngoai_khong_anh_huong_kho_luu()
    {
        var store = NewStore(TempFile());
        var copy = store.Current; copy.OrganizationName = "Doi ben ngoai";
        Assert.NotEqual("Doi ben ngoai", store.Current.OrganizationName);
    }
}
