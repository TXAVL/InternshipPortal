using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using InternshipPortal.Web.Data;
using InternshipPortal.Web.Models;

namespace InternshipPortal.Tests;

/// <summary>
/// Chạy cả ứng dụng thật (WebApplicationFactory) trên MỘT DATABASE RIÊNG được tạo mới cho mỗi lần chạy test
/// (InternshipDbTest_xxxx) và xóa khi xong; cài đặt hệ thống và thư mục upload cũng nằm ở thư mục tạm,
/// nên test không bao giờ đụng vào dữ liệu thật của InternshipDb.
/// Kết nối lấy từ biến môi trường TEST_SQL_CONNECTION, nếu không có thì lấy từ appsettings.json của dự án Web
/// (chỉ đổi tên database).
/// </summary>
public class TestApp : WebApplicationFactory<Program>
{
    public string DbName { get; } = "InternshipDbTest_" + Guid.NewGuid().ToString("N")[..10];
    public string TempDir { get; } = Path.Combine(Path.GetTempPath(), "ip-tests-" + Guid.NewGuid().ToString("N")[..10]);
    public string SettingsPath => Path.Combine(TempDir, "site-settings.json");
    private string _masterConnection = "";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        Directory.CreateDirectory(Path.Combine(TempDir, "wwwroot"));
        builder.UseWebRoot(Path.Combine(TempDir, "wwwroot"));

        var baseConn = Environment.GetEnvironmentVariable("TEST_SQL_CONNECTION") ?? ReadConnectionFromAppSettings();
        var sb = new SqlConnectionStringBuilder(baseConn) { InitialCatalog = DbName };
        _masterConnection = new SqlConnectionStringBuilder(baseConn) { InitialCatalog = "master" }.ConnectionString;

        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = sb.ConnectionString,
                ["SiteSettings:Path"] = SettingsPath
            });
        });
    }

    private static string ReadConnectionFromAppSettings()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "InternshipPortal.Web", "appsettings.json");
            if (File.Exists(candidate))
            {
                var cfg = new ConfigurationBuilder().AddJsonFile(candidate).Build();
                return cfg.GetConnectionString("DefaultConnection")
                       ?? throw new InvalidOperationException("appsettings.json thiếu ConnectionStrings:DefaultConnection");
            }
            dir = dir.Parent;
        }
        throw new InvalidOperationException(
            "Không tìm thấy InternshipPortal.Web/appsettings.json. Tạo file từ appsettings.example.json hoặc đặt biến môi trường TEST_SQL_CONNECTION.");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;
        try
        {
            if (!string.IsNullOrEmpty(_masterConnection))
            {
                SqlConnection.ClearAllPools();
                using var conn = new SqlConnection(_masterConnection);
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = $"IF DB_ID(N'{DbName}') IS NOT NULL BEGIN ALTER DATABASE [{DbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{DbName}]; END";
                cmd.ExecuteNonQuery();
            }
        }
        catch (Exception) { /* dọn dẹp thất bại không làm hỏng kết quả test */ }
        try { Directory.Delete(TempDir, true); } catch (Exception) { }
    }

    // ---------- Truy cập DB trực tiếp để dựng dữ liệu / kiểm tra kết quả ----------
    public T Query<T>(Func<ApplicationDbContext, T> read)
    {
        using var scope = Services.CreateScope();
        return read(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    public void Db(Action<ApplicationDbContext> change)
    {
        using var scope = Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        change(ctx);
        ctx.SaveChanges();
    }

    // ---------- Dữ liệu mẫu cho test (mỗi test tự tạo dữ liệu riêng, tên không trùng) ----------
    private static int _seq;
    public static string Uniq(string prefix) => $"{prefix}{Interlocked.Increment(ref _seq)}{Guid.NewGuid().ToString("N")[..4]}";

    public int CompanyIdByName(string name) => Query(c => c.Companies.Single(x => x.CompanyName == name).Id);
    public int TxaCompanyId => CompanyIdByName("TXA Studio");
    public int FptCompanyId => CompanyIdByName("FPT Software");

    public int NewJob(int companyId, bool active = true, DateTime? expire = null, string? title = null)
    {
        var job = new JobPost
        {
            CompanyId = companyId, Title = title ?? Uniq("Job "), IsActive = active,
            ExpireDate = expire ?? DateTime.Now.AddDays(30), Description = "mô tả", Requirements = "yêu cầu"
        };
        Db(c => c.JobPosts.Add(job));
        return job.Id;
    }

    /// <summary>Công ty mới + tài khoản HR (mật khẩu 123456) để thử phân quyền giữa các công ty.</summary>
    public (int CompanyId, string Username) NewCompanyWithHr(bool approved = true)
    {
        var company = new Company { CompanyName = Uniq("Cty "), IsApproved = approved };
        Db(c => c.Companies.Add(company));
        var username = Uniq("hr") + "@test.vn";
        AddAccount(username, Roles.Company, company.Id, "HR " + company.CompanyName);
        return (company.Id, username);
    }

    public (int StudentId, string Code) NewStudent(bool withAccount = true, string? email = null)
    {
        var code = Uniq("SV");
        var s = new StudentProfile { StudentCode = code, FullName = "Sinh viên " + code, Major = "CNTT", Email = email ?? code + "@test.vn", PhoneNumber = "0900000000" };
        Db(c => c.StudentProfiles.Add(s));
        if (withAccount) AddAccount(code, Roles.Student, s.Id, s.FullName);
        return (s.Id, code);
    }

    public void AddAccount(string username, string role, int? relatedId, string fullName)
    {
        var account = new UserAccount { Username = username, Role = role, RelatedId = relatedId, FullName = fullName };
        account.PasswordHash = new PasswordHasher<UserAccount>().HashPassword(account, "123456");
        Db(c => c.UserAccounts.Add(account));
    }

    public int NewApplication(int jobId, int studentId, ApplicationStatus status = ApplicationStatus.Accepted, int reports = 0)
    {
        var app = new Application { JobPostId = jobId, StudentProfileId = studentId, Status = status, AppliedAt = DateTime.Now.AddDays(-30) };
        Db(c => c.Applications.Add(app));
        for (var i = 1; i <= reports; i++)
        {
            var week = i;
            Db(c => c.WeeklyReports.Add(new WeeklyReport
            {
                ApplicationId = app.Id, WeekNumber = week, TasksDone = $"Tuần {week}", NextWeekPlan = "",
                // Luôn nằm trước tuần hiện tại để không dính luật "mỗi tuần 1 báo cáo"
                SubmittedAt = DateTime.Now.AddDays(-(8 + 7 * (reports - week)))
            }));
        }
        return app.Id;
    }

    /// <summary>Sinh viên (có tài khoản) đã trúng tuyển vào một tin của công ty, kèm số báo cáo tuần cho trước.</summary>
    public (int AppId, string Code, int JobId) NewAcceptedStudent(int companyId, int reports = 0)
    {
        var job = NewJob(companyId);
        var (sid, code) = NewStudent();
        return (NewApplication(job, sid, ApplicationStatus.Accepted, reports), code, job);
    }
}

/// <summary>Trình duyệt giả: giữ cookie đăng nhập, tự lấy token chống giả mạo (antiforgery) cho form POST.</summary>
public sealed class Client
{
    public HttpClient Http { get; }
    public bool LoggedIn { get; private set; }

    public Client(TestApp app)
    {
        Http = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
    }

    public static async Task<Client> As(TestApp app, string? username)
    {
        var c = new Client(app);
        if (username != null) await c.LoginAsync(username);
        return c;
    }

    public async Task<string> HtmlAsync(string url) => await (await Http.GetAsync(url)).Content.ReadAsStringAsync();

    public async Task<string> TokenAsync()
    {
        // Người đã đăng nhập luôn có form Đăng xuất trên mọi trang; khách thì dùng trang đăng nhập
        var html = await HtmlAsync(LoggedIn ? "/Job" : "/Account/Login");
        var m = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        if (!m.Success) throw new InvalidOperationException("Không tìm thấy token chống giả mạo trên trang.");
        return m.Groups[1].Value;
    }

    public async Task<HttpResponseMessage> PostAsync(string url, IDictionary<string, string>? form = null)
    {
        var fields = new Dictionary<string, string>(form ?? new Dictionary<string, string>()) { ["__RequestVerificationToken"] = await TokenAsync() };
        return await Http.PostAsync(url, new FormUrlEncodedContent(fields));
    }

    public async Task<HttpResponseMessage> PostFileAsync(string url, IDictionary<string, string> form, string fileField, string fileName, byte[] bytes)
    {
        var content = new MultipartFormDataContent { { new StringContent(await TokenAsync()), "__RequestVerificationToken" } };
        foreach (var kv in form) content.Add(new StringContent(kv.Value), kv.Key);
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        content.Add(file, fileField, fileName);
        return await Http.PostAsync(url, content);
    }

    public async Task LoginAsync(string username, string password = "123456")
    {
        var r = await PostAsync("/Account/Login", new Dictionary<string, string> { ["Username"] = username, ["Password"] = password });
        if (r.StatusCode != HttpStatusCode.Redirect)
            throw new InvalidOperationException($"Đăng nhập {username} thất bại: {(int)r.StatusCode}");
        LoggedIn = true;
    }

    public static string Location(HttpResponseMessage r) => r.Headers.Location?.OriginalString ?? "";
    public static bool IsLoginRedirect(HttpResponseMessage r) => r.StatusCode == HttpStatusCode.Redirect && Location(r).Contains("/Account/Login", StringComparison.OrdinalIgnoreCase);
    public static bool IsDenied(HttpResponseMessage r) => r.StatusCode == HttpStatusCode.Redirect && Location(r).Contains("/Account/AccessDenied", StringComparison.OrdinalIgnoreCase);
}

public static class Accounts
{
    public const string Admin = "admin@tdu.vn";
    public const string Hr = "hr@txastudio.click";
    public const string Student = "test";
}

[CollectionDefinition("app")]
public class AppCollection : ICollectionFixture<TestApp> { }
