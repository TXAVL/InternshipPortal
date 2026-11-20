using System.ComponentModel.DataAnnotations;

namespace InternshipPortal.Web.Models.ViewModels;

public class StudentPortalViewModel
{
    public StudentProfile? Student { get; set; }

    public List<Application> Applications { get; set; } = new();
}

public class WeeklyReportInput
{
    public int ApplicationId { get; set; }

    [Required(ErrorMessage = "Vui lòng mô tả công việc đã làm trong tuần")]
    [StringLength(4000, ErrorMessage = "Nội dung tối đa 4000 ký tự")]
    public string TasksDone { get; set; } = string.Empty;

    [StringLength(4000, ErrorMessage = "Nội dung tối đa 4000 ký tự")]
    public string? NextWeekPlan { get; set; }
}
