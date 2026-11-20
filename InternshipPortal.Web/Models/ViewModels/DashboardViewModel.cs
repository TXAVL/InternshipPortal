namespace InternshipPortal.Web.Models.ViewModels;

public class DashboardViewModel
{
    public int TotalCompanies { get; set; }
    public int ApprovedCompanies { get; set; }
    public int TotalJobPosts { get; set; }
    public int ActiveJobPosts { get; set; }
    public int TotalApplications { get; set; }

    /// <summary>Số sinh viên (không trùng) đã nộp ít nhất 1 hồ sơ.</summary>
    public int TotalStudentsApplied { get; set; }

    /// <summary>Số sinh viên (không trùng) có ít nhất 1 đơn Trúng tuyển.</summary>
    public int AcceptedStudents { get; set; }

    /// <summary>Tỷ lệ có nơi thực tập = SV trúng tuyển / SV đã ứng tuyển.</summary>
    public double PlacementRate => TotalStudentsApplied == 0
        ? 0
        : Math.Round(AcceptedStudents * 100.0 / TotalStudentsApplied, 1);

    public Dictionary<ApplicationStatus, int> StatusCounts { get; set; } = new();

    public List<CompanyStat> CompanyStats { get; set; } = new();

    public int ReportsAwaitingFeedback { get; set; }

    public List<WeeklyReport> RecentReports { get; set; } = new();

    public int CountOf(ApplicationStatus status) => StatusCounts.TryGetValue(status, out var n) ? n : 0;

    public double PercentOf(ApplicationStatus status) => TotalApplications == 0
        ? 0
        : CountOf(status) * 100.0 / TotalApplications;
}

public class CompanyStat
{
    public int CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public bool IsApproved { get; set; }
    public int JobPosts { get; set; }
    public int Applications { get; set; }
    public int Accepted { get; set; }
}
