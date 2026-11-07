using System.ComponentModel.DataAnnotations;

namespace InternshipPortal.Web.Models;

public class JobPost
{
    public int Id { get; set; }

    public int CompanyId { get; set; }

    [Required]
    public string Title { get; set; } = string.Empty;

    public string Major { get; set; } = "CNTT";

    public string Location { get; set; } = "Hà Nội";

    public string? Description { get; set; }

    public string? Requirements { get; set; }

    public string SalaryRange { get; set; } = "3 - 5 triệu";

    public int Quantity { get; set; } = 3;

    public DateTime ExpireDate { get; set; } = DateTime.Now.AddDays(30);

    public bool IsActive { get; set; } = true;

    public Company Company { get; set; } = null!;

    public List<Application> Applications { get; set; } = new();
}

