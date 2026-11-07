using System.ComponentModel.DataAnnotations;

namespace InternshipPortal.Web.Models;

public class Company
{
    public int Id { get; set; }

    [Required]
    public string CompanyName { get; set; } = string.Empty;

    public string? Address { get; set; }

    public string? Website { get; set; }

    public string? ContactEmail { get; set; }

    public bool IsApproved { get; set; } = true;

    public List<JobPost> JobPosts { get; set; } = new();
}

