using System.ComponentModel.DataAnnotations;

namespace InternshipPortal.Web.Models;

public class StudentProfile
{
    public int Id { get; set; }

    [Required]
    public string StudentCode { get; set; } = string.Empty;

    [Required]
    public string FullName { get; set; } = string.Empty;

    public string? Major { get; set; }

    public string? Email { get; set; }

    public string? PhoneNumber { get; set; }

    public string? CVPath { get; set; }

    public List<Application> Applications { get; set; } = new();
}

