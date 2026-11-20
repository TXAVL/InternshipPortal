using System.ComponentModel.DataAnnotations;

namespace InternshipPortal.Web.Models;

/// <summary>
/// Nhật ký thực tập hàng tuần của sinh viên đã trúng tuyển (Application.Status == Accepted).
/// </summary>
public class WeeklyReport
{
    public int Id { get; set; }

    public int ApplicationId { get; set; }

    [Range(1, 52, ErrorMessage = "Số tuần phải từ 1 đến 52")]
    [Display(Name = "Tuần")]
    public int WeekNumber { get; set; }

    [Required(ErrorMessage = "Vui lòng mô tả công việc đã làm trong tuần")]
    [Display(Name = "Công việc đã làm")]
    public string TasksDone { get; set; } = string.Empty;

    [Display(Name = "Kế hoạch tuần tới")]
    public string NextWeekPlan { get; set; } = string.Empty;

    /// <summary>Nhận xét của GVHD / Khoa (do tài khoản Admin nhập).</summary>
    [Display(Name = "Nhận xét của GVHD / Khoa")]
    public string? MentorFeedback { get; set; }

    /// <summary>Nhận xét của Doanh nghiệp (do tài khoản Company nhập). Tách riêng để 2 bên không ghi đè nhau.</summary>
    [Display(Name = "Nhận xét của Doanh nghiệp")]
    public string? CompanyFeedback { get; set; }

    public DateTime SubmittedAt { get; set; } = DateTime.Now;

    public Application Application { get; set; } = null!;
}
