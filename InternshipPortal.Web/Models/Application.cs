namespace InternshipPortal.Web.Models;

public enum ApplicationStatus
{
    Pending,
    Interview,
    Accepted,
    Rejected
}

public class Application
{
    public int Id { get; set; }

    public int JobPostId { get; set; }

    public int StudentProfileId { get; set; }

    public DateTime AppliedAt { get; set; } = DateTime.Now;

    public ApplicationStatus Status { get; set; } = ApplicationStatus.Pending;

    public JobPost JobPost { get; set; } = null!;

    public StudentProfile StudentProfile { get; set; } = null!;
}

