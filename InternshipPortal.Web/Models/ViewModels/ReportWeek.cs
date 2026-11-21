namespace InternshipPortal.Web.Models.ViewModels;

/// <summary>Quy tắc "mỗi tuần lịch (Thứ Hai - Chủ Nhật) chỉ nộp 1 báo cáo cho mỗi đơn thực tập".</summary>
public static class ReportWeek
{
    /// <summary>Ngày Thứ Hai (00:00) của tuần chứa <paramref name="date"/>.</summary>
    public static DateTime StartOfWeek(DateTime date)
    {
        var daysSinceMonday = ((int)date.DayOfWeek + 6) % 7;
        return date.Date.AddDays(-daysSinceMonday);
    }

    /// <summary>Đã có báo cáo nào của đơn này nộp trong tuần lịch hiện tại chưa.</summary>
    public static bool HasSubmittedThisWeek(Application application) =>
        application.WeeklyReports.Any(r => r.SubmittedAt >= StartOfWeek(DateTime.Now));

    /// <summary>Thứ Hai tuần sau: lúc được nộp báo cáo kế tiếp.</summary>
    public static DateTime NextWindowOpens() => StartOfWeek(DateTime.Now).AddDays(7);
}
