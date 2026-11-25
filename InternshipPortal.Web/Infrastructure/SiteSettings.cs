using System.Text.Json;

namespace InternshipPortal.Web.Infrastructure;

/// <summary>Một theme giao diện. Màu thật nằm trong site.css (khối [data-theme="..."]), ở đây chỉ để hiển thị mẫu màu.</summary>
public record ThemeInfo(string Id, string Name, bool Dark, string Ink, string Accent, string Paper);

/// <summary>Danh mục 15 theme dùng chung cho bảng cài đặt nổi và trang cài đặt của Admin.</summary>
public static class ThemeCatalog
{
    public const string DefaultId = "ngoc";

    public static readonly IReadOnlyList<ThemeInfo> All = new List<ThemeInfo>
    {
        new("ngoc",     "Ngọc lục bảo", false, "#10231f", "#0f766e", "#f6f5f1"),
        new("bien",     "Biển xanh",    false, "#0f2a43", "#0369a1", "#f3f6f9"),
        new("troi",     "Trời trong",   false, "#13324a", "#0284c7", "#f1f7fb"),
        new("bac-ha",   "Bạc hà",       false, "#12332b", "#059669", "#f2f8f5"),
        new("rung",     "Rừng sâu",     false, "#14281a", "#2f7d32", "#f4f6f1"),
        new("nang",     "Nắng vàng",    false, "#352a0b", "#b7791f", "#faf7ee"),
        new("hoang-hon","Hoàng hôn",    false, "#3b1f12", "#c2410c", "#faf5f0"),
        new("cafe",     "Cà phê",       false, "#2b1d14", "#92572d", "#f7f2ec"),
        new("sen",      "Hồng sen",     false, "#40182a", "#be185d", "#fbf3f6"),
        new("hoa-dao",  "Hoa đào",      false, "#44212a", "#e11d63", "#fdf3f4"),
        new("ruou",     "Đỏ rượu vang", false, "#3a1015", "#9f1239", "#f9f3f3"),
        new("than",     "Than chì",     false, "#1c1f24", "#475569", "#f3f4f6"),
        new("dem",      "Đêm xanh",     true,  "#0a1514", "#2dd4bf", "#0e1b1a"),
        new("nua-dem",  "Nửa đêm",      true,  "#0b1220", "#60a5fa", "#0f172a"),
        new("tet",      "Tết cổ truyền",false, "#7a0c14", "#c8102e", "#fff6e5")
    };

    public static ThemeInfo Find(string? id) =>
        All.FirstOrDefault(t => t.Id == id) ?? All[0];

    public static bool IsValid(string? id) => All.Any(t => t.Id == id);
}

/// <summary>Cài đặt hệ thống do Admin quản lý (khác với cài đặt giao diện cá nhân ở nút bánh răng nổi).</summary>
public class SiteSettings
{
    public const string DefaultOrganizationName = "Intern Portal";

    /// <summary>Tên đơn vị hiển thị trên thanh menu, tiêu đề tab và chân trang.</summary>
    public string OrganizationName { get; set; } = DefaultOrganizationName;

    /// <summary>Cho phép sinh viên tự đăng ký tài khoản.</summary>
    public bool AllowStudentRegistration { get; set; } = true;

    /// <summary>Thông báo hiển thị đầu mọi trang khi được bật.</summary>
    public bool AnnouncementEnabled { get; set; }

    public string AnnouncementText { get; set; } = string.Empty;

    /// <summary>Số tuần báo cáo tối thiểu sinh viên phải nộp thì mới được chấm điểm cuối kỳ (0 = không yêu cầu).</summary>
    public int MinReportsForEvaluation { get; set; } = 4;
}

/// <summary>Lưu SiteSettings vào App_Data/site-settings.json (không cần đổi cấu trúc CSDL).</summary>
public class SiteSettingsStore
{
    private readonly string _path;
    private readonly object _lock = new();
    private SiteSettings _current;

    public SiteSettingsStore(IWebHostEnvironment env, IConfiguration config)
    {
        // SiteSettings:Path cho phép đổi nơi lưu (dùng trong test để không đụng cài đặt thật)
        _path = config["SiteSettings:Path"] ?? Path.Combine(env.ContentRootPath, "App_Data", "site-settings.json");
        _current = Load();
    }

    public SiteSettings Current
    {
        get
        {
            lock (_lock)
            {
                return new SiteSettings
                {
                    OrganizationName = _current.OrganizationName,
                    AllowStudentRegistration = _current.AllowStudentRegistration,
                    AnnouncementEnabled = _current.AnnouncementEnabled,
                    AnnouncementText = _current.AnnouncementText,
                    MinReportsForEvaluation = _current.MinReportsForEvaluation
                };
            }
        }
    }

    public void Save(SiteSettings settings)
    {
        settings.OrganizationName = string.IsNullOrWhiteSpace(settings.OrganizationName)
            ? SiteSettings.DefaultOrganizationName
            : settings.OrganizationName.Trim();
        settings.AnnouncementText = (settings.AnnouncementText ?? string.Empty).Trim();
        settings.MinReportsForEvaluation = Math.Clamp(settings.MinReportsForEvaluation, 0, 52);

        lock (_lock)
        {
            _current = settings;
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    private SiteSettings Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                var loaded = JsonSerializer.Deserialize<SiteSettings>(File.ReadAllText(_path));
                if (loaded != null && !string.IsNullOrWhiteSpace(loaded.OrganizationName))
                {
                    return loaded;
                }
            }
        }
        catch (Exception)
        {
            // File hỏng: dùng mặc định, lần lưu sau sẽ ghi đè
        }

        return new SiteSettings();
    }
}
